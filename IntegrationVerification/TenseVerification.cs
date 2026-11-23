using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;

// Alla testformer nedan är uttryckligen syntetiska och finns bara i den tillfälliga testdatabasen.
internal static class TenseVerification
{
    public static async Task RunAsync(DbContextOptions<ApplicationDbContext> options, HttpClient client, HttpClient other, string userId, Action<bool, string> check, bool keepPreview = false)
    {
        using var guest = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = client.BaseAddress };
        async Task<string> Html(string path, HttpClient? target = null) => WebUtility.HtmlDecode(await (target ?? client).GetStringAsync(path));
        static string Input(string html, string name) => WebUtility.HtmlDecode(Regex.Match(Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>").Value, "value=\"([^\"]*)\"").Groups[1].Value);
        async Task<string> Snapshot(bool reviews)
        {
            using var db = new ApplicationDbContext(options);
            return reviews ? JsonSerializer.Serialize(await db.QuestionReviewStates.AsNoTracking().OrderBy(row => row.Id).ToListAsync())
                : JsonSerializer.Serialize(await db.UserProgresses.AsNoTracking().OrderBy(row => row.Id).ToListAsync());
        }
        var home = await Html("/Tenses", guest);
        check((await guest.GetAsync("/Tenses")).IsSuccessStatusCode && new[] { "Äta – jag", "Gå – jag", "Prata – jag" }.All(home.Contains)
            && !home.Contains("Tigrinjaexemplen förbereds"), "Tre verkliga verbgrupper visas offentligt");
        var progressBeforeLinks = await Snapshot(false);
        var reviewsBeforeLinks = await Snapshot(true);
        var learningPath = await Html("/Learn");
        var otherLearningPath = await Html("/Learn", other);
        check(learningPath.Contains("Använd tidsformerna i en situation") && !otherLearningPath.Contains("Använd tidsformerna i en situation"),
            "Lärvägen erbjuder situationsfortsättning först efter elevens egna klara tidsformslektioner");
        check((await Html("/Games")).Contains("Tidsformer i vardagen") && (await Html("/Games")).Contains("Välj tidsformsövning"),
            "Spelöversikten gör tidsformsträningen lätt att hitta");
        check(!(await Html("/Quiz/Result/1")).Contains("Använd tidsformerna i en situation"),
            "Vanliga lektionsresultat visar inga orelaterade verbgrupper");
        using (var seedDb = new ApplicationDbContext(options))
        {
            var seeds = await seedDb.Questions.Include(q => q.Choices).Where(q => q.Id >= 2001 && q.Id <= 2009).ToListAsync();
            check(seeds.Count == 9 && seeds.All(q => TenseGuide.IsReviewed(q) && q.AudioFileName == null
                && q.Choices.Count == 4 && q.Choices.Count(c => c.IsCorrect) == 1 && q.Choices.Select(c => c.Text).Distinct().Count() == 4),
                "Nio källkontrollerade tidsformer har sammanhang, fyra unika svar och inga ljudkopplingar");
            foreach (var group in seeds.GroupBy(q => q.TenseGroup))
            {
                check(group.Select(q => q.TimeReference).Distinct().Count() == 3, "Varje startverb har en form för nutid, dåtid och framtid");
                var groupIdForLinks = group.Min(q => q.Id);
                var past = group.Single(q => q.TimeReference == TimeReference.Past);
                var present = group.Single(q => q.TimeReference == TimeReference.Present);
                var writing = await Html("/Tenses/Write/" + groupIdForLinks, guest);
                async Task<string> WriteAnswer(string answer)
                {
                    var response = await guest.PostAsync("/Tenses/CheckWriting/" + groupIdForLinks,
                        new FormUrlEncodedContent(new Dictionary<string,string> { ["step"]="1", ["answer"]=answer,
                            ["__RequestVerificationToken"]=Input(writing,"__RequestVerificationToken") }));
                    response.EnsureSuccessStatusCode();
                    return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
                }
                check(writing.Contains(past.TenseContext!) && (await WriteAnswer(present.LatinText)).Contains("Här behövs dåtid")
                    && (await WriteAnswer(past.LatinText)).Contains("Nästa situation"), "Skrivsituationen ger tidsformsspecifik rättning och är öppen för gäster");
                var resultForLinks = await Html("/Quiz/Result/" + group.First().LessonId);
                check(resultForLinks.Contains("Använd tidsformerna i en situation") && resultForLinks.Contains("/Tenses/Play/" + groupIdForLinks)
                    && resultForLinks.Contains("/Tenses/Details/" + groupIdForLinks),
                    "En klar tidsformslektion länkar direkt till sin egen situationsträning och jämförelse");
                check(learningPath.Contains("/Tenses/Play/" + groupIdForLinks) && !otherLearningPath.Contains("/Tenses/Play/" + groupIdForLinks),
                    "Lärvägens träningslänkar följer den inloggade elevens upplåsningar");
                var page = await Html("/Tenses/Details/" + group.Min(q => q.Id), guest);
                check(group.All(q => page.Contains(q.LatinText) && page.Contains(q.TenseContext!)) && !Regex.IsMatch(page, @"[\u1200-\u139F]"),
                    "Verkliga jämförelsekort visar alla tre former och sammanhang med enbart latinsk tigrinja");
                check((await client.GetAsync("/Tenses/Play/" + group.Min(q => q.Id))).IsSuccessStatusCode,
                    "En färdigläst verbgrupp öppnar situationsträning för den inloggade eleven");
                check((await other.GetAsync("/Tenses/Play/" + group.Min(q => q.Id))).StatusCode == HttpStatusCode.Forbidden,
                    "Verkliga tidsformsövningar låses tills motsvarande lektion är klar");
            }
        }
        check(await Snapshot(false) == progressBeforeLinks && await Snapshot(true) == reviewsBeforeLinks,
            "Att visa fortsättningskort ändrar varken XP, lektionsstatus eller repetition");
        foreach (var path in new[] { "/Tenses/Play/1", "/Tenses/Result" })
            check((await guest.GetAsync(path)).StatusCode == HttpStatusCode.Redirect, "Tidsformsträning och personliga resultat kräver konto");
        var formCount = 0;
        using (var db = new ApplicationDbContext(options)) formCount = await db.Questions.CountAsync();
        async Task<Dictionary<string, string>> NewForm(int lesson, int index)
        {
            var html = await Html("/PhraseCreation?lessonId=" + lesson);
            return new() {
                ["CreationToken"] = Input(html, "CreationToken"), ["__RequestVerificationToken"] = Input(html, "__RequestVerificationToken"),
                ["LessonId"] = lesson.ToString(), ["CourseId"] = "1", ["NewLessonTitle"] = "Verifiering – tidsformer",
                ["LatinText"] = new[] { "testform-nu", "testform-tidigare", "testform-senare" }[index], ["CorrectChoiceIndex"] = "0", ["Audience"] = "4",
                ["Choices[0].Text"] = new[] { "Test – nutid", "Test – dåtid", "Test – framtid" }[index],
                ["Choices[1].Text"] = "Annat testsvar ett", ["Choices[2].Text"] = "Annat testsvar två", ["Choices[3].Text"] = "Annat testsvar tre",
                ["TenseGroup"] = "Verifieringsverb – jag", ["TimeReference"] = (index + 1).ToString(),
                ["TenseContext"] = new[] { "Verifieringssituation: något händer nu.", "Verifieringssituation: något hände tidigare.", "Verifieringssituation: något ska hända senare." }[index],
                ["TenseNotes"] = "Syntetisk verifieringsform, inte en tigrinjaöversättning.", ["TenseReviewed"] = "false"
            };
        }
        var invalid = await NewForm(0, 0); invalid["TimeReference"] = "999";
        check((await client.PostAsync("/PhraseCreation", new FormUrlEncodedContent(invalid))).StatusCode == HttpStatusCode.OK,
            "Okända tidsformer avvisas av skapaformuläret");
        invalid = await NewForm(0, 0); invalid["TenseReviewed"] = "true"; invalid["TenseContext"] = " ";
        check((await client.PostAsync("/PhraseCreation", new FormUrlEncodedContent(invalid))).StatusCode == HttpStatusCode.OK,
            "Språkgranskning kräver ett faktiskt användningssammanhang");
        invalid = await NewForm(0, 0); invalid["TenseGroup"] = "";
        check((await client.PostAsync("/PhraseCreation", new FormUrlEncodedContent(invalid))).StatusCode == HttpStatusCode.OK,
            "Tidsformsuppgifter kan inte sparas utan en namngiven grupp");
        using (var db = new ApplicationDbContext(options)) check(await db.Questions.CountAsync() == formCount, "Ogiltiga tidsformsformulär lämnar inga nya fraser eller tomma lektioner");
        var ids = new List<int>(); var lessonId = 0;
        for (var index = 0; index < 3; index++)
        {
            var response = await client.PostAsync("/PhraseCreation", new FormUrlEncodedContent(await NewForm(lessonId, index)));
            check(response.StatusCode == HttpStatusCode.Redirect, "En tidsform kan sparas lokalt som utkast utan SQL-kommandon");
            var id = int.Parse(response.Headers.Location!.ToString().Split('/').Last()); ids.Add(id);
            using var db = new ApplicationDbContext(options); lessonId = (await db.Questions.SingleAsync(row => row.Id == id)).LessonId;
        }
        var groupId = ids.Min();
        check(!(await Html("/Tenses", guest)).Contains("Verifieringsverb") && (await guest.GetAsync("/Tenses/Details/" + groupId)).StatusCode == HttpStatusCode.NotFound
            && (await Html("/Tenses")).Contains("Verifieringsverb") && !(await Html("/Lessons/Details/" + lessonId, guest)).Contains("testform-"),
            "Utkast syns för den lokala redigeraren men inte på offentliga tidsformskort eller ordkort");
        async Task<Dictionary<string, string>> EditForm(int id)
        {
            var html = await Html("/ContentEditor/Edit/" + id); using var db = new ApplicationDbContext(options);
            var row = await db.Questions.Include(row => row.Choices).SingleAsync(row => row.Id == id);
            var form = new Dictionary<string, string> { ["EditStamp"] = Input(html, "EditStamp"), ["__RequestVerificationToken"] = Input(html, "__RequestVerificationToken"),
                ["LatinText"] = row.LatinText, ["Audience"] = ((int)row.Audience).ToString(), ["CorrectChoiceId"] = row.Choices.Single(choice => choice.IsCorrect).Id.ToString(),
                ["TenseGroup"] = row.TenseGroup ?? "", ["TimeReference"] = ((int)row.TimeReference).ToString(), ["TenseContext"] = row.TenseContext ?? "", ["TenseNotes"] = row.TenseNotes ?? "", ["TenseReviewed"] = row.TenseReviewed.ToString() };
            var choices = row.Choices.OrderBy(choice => choice.Id).ToList();
            for (var i = 0; i < choices.Count; i++) { form[$"Choices[{i}].Id"] = choices[i].Id.ToString(); form[$"Choices[{i}].Text"] = choices[i].Text; }
            return form;
        }
        foreach (var id in ids)
        {
            var form = await EditForm(id); form["TenseReviewed"] = "true";
            check((await client.PostAsync("/ContentEditor/Edit/" + id, new FormUrlEncodedContent(form))).StatusCode == HttpStatusCode.Redirect,
                "Granskade tidsformsuppgifter sparas genom samma skyddade frasredigerare");
        }
        var details = await Html("/Tenses/Details/" + groupId, guest);
        check(details.Contains("Nutid") && details.Contains("Dåtid") && details.Contains("Framtid") && new[] { "testform-nu", "testform-tidigare", "testform-senare" }.All(details.Contains),
            "En komplett granskad grupp visar alla tre former och deras sammanhang");
        check((await client.GetAsync("/Tenses/Play/" + groupId)).StatusCode == HttpStatusCode.Forbidden,
            "Tidsformsträning kräver att gruppens lektioner är klara");
        using (var db = new ApplicationDbContext(options)) { db.UserProgresses.Add(new UserProgress { UserId = userId, LessonId = lessonId, Score = 3, CompletedAt = DateTime.UtcNow }); await db.SaveChangesAsync(); }
        var quizHtml = await Html("/Quiz/Index/" + lessonId);
        check(quizHtml.Contains("När används formen?") && quizHtml.Contains("Verifieringssituation") && !quizHtml.Contains("\u1230"),
            "Vanliga lektionskort visar språkgranskade tidsformstips");
        using (var db = new ApplicationDbContext(options))
        {
            var row = await db.Questions.Include(row => row.Choices).SingleAsync(row => row.Id == ids[0]);
            var feedback = await client.PostAsync("/Quiz/CheckAnswer/" + lessonId, new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = Input(quizHtml, "__RequestVerificationToken"), ["QuestionId"] = row.Id.ToString(), ["ChoiceId"] = row.Choices.Single(choice => choice.IsCorrect).Id.ToString(), ["Kind"] = "Read" }));
            using var json = JsonDocument.Parse(await feedback.Content.ReadAsStringAsync());
            check(feedback.IsSuccessStatusCode && json.RootElement.GetProperty("explanation").GetString()!.Contains("Nutid"),
                "Quizets direkträttning ger en tidsformsförklaring utan att spara något svar");
        }
        async Task<(string Token, string Csrf, ConversationBoardViewModel Board)> Open()
        {
            var html = await client.GetStringAsync("/Tenses/Play/" + groupId);
            var json = Regex.Match(html, "<script type=\"application/json\" id=\"conversation-board\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
            check(!html.Contains("CorrectChoiceKey") && !html.Contains("QuestionId") && !html.Contains("IsCorrect"), "Tidsformsomgången skickar inget facit till webbläsaren före rättning");
            return (Input(html, "SessionToken"), Input(html, "__RequestVerificationToken"), JsonSerializer.Deserialize<ConversationBoardViewModel>(json)!);
        }
        static string Expected(ConversationTurnViewModel turn) => turn.PartnerLine.Contains("händer nu") ? "testform-nu" : turn.PartnerLine.Contains("hände tidigare") ? "testform-tidigare" : "testform-senare";
        async Task<HttpResponseMessage> Answer(string token, string csrf, ConversationTurnViewModel turn, Guid choice, Guid? request = null, HttpClient? target = null) =>
            await (target ?? client).PostAsync("/Tenses/CheckAnswer", new FormUrlEncodedContent(new Dictionary<string, string> { ["SessionToken"] = token, ["__RequestVerificationToken"] = csrf, ["TurnKey"] = turn.Key.ToString(), ["ChoiceKey"] = choice.ToString(), ["RequestId"] = (request ?? Guid.NewGuid()).ToString() }));
        async Task<HttpResponseMessage> Finish(string token, string csrf) => await client.PostAsync("/Tenses/Finish", new FormUrlEncodedContent(new Dictionary<string, string> { ["SessionToken"] = token, ["__RequestVerificationToken"] = csrf }));
        var round = await Open();
        check(round.Board.Turns.Count == 3 && round.Board.Turns.All(turn => turn.Choices.Count == 4 && turn.Choices.Select(choice => choice.Text).Distinct().Count() == 4),
            "Tidsformsträningen har tre situationer med fyra olika alternativ");
        var first = round.Board.Turns[0]; var wrong = first.Choices.First(choice => choice.Text != Expected(first)); var requestId = Guid.NewGuid();
        check((await Finish(round.Token, round.Csrf)).StatusCode == HttpStatusCode.BadRequest
            && (await Answer(round.Token, "", first, wrong.Key)).StatusCode == HttpStatusCode.BadRequest
            && (await Answer(round.Token + "x", round.Csrf, first, wrong.Key)).StatusCode == HttpStatusCode.Conflict
            && (await Answer(round.Token, Input(await Html("/", other), "__RequestVerificationToken"), first, wrong.Key, target: other)).StatusCode == HttpStatusCode.Conflict,
            "Tidsformsflödet skyddar konto, session, antiforgery och ofärdiga omgångar");
        var beforeReviews = await Snapshot(true); var beforeXp = await Snapshot(false);
        var responseWrong = await Answer(round.Token, round.Csrf, first, wrong.Key, requestId); var payload = await responseWrong.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(payload)) check(!json.RootElement.GetProperty("isCorrect").GetBoolean() && json.RootElement.GetProperty("explanation").GetString()!.Contains("Verifieringssituation"), "Fel tidsform ger rätt svar med ett användningssammanhang");
        check(await (await Answer(round.Token, round.Csrf, first, wrong.Key, requestId)).Content.ReadAsStringAsync() == payload, "Omskickad tidsformsrättning ger inte dubbla poäng");
        foreach (var turn in round.Board.Turns.Skip(1)) check((await Answer(round.Token, round.Csrf, turn, turn.Choices.Single(choice => choice.Text == Expected(turn)).Key)).IsSuccessStatusCode, "Rätt tidsform godkänns av servern");
        check(await Snapshot(true) == beforeReviews && await Snapshot(false) == beforeXp, "Tidsformernas direkträttning sparar inga ofärdiga framsteg");
        await Finish(round.Token, round.Csrf); var result = await Html("/Tenses/Result");
        check(result.Contains("Bra jobbat med tidsformerna!") && result.Contains("2 av 3") && result.Contains("200") && result.Contains("2 av 3 stjärnor") && await Snapshot(false) == beforeXp,
            "Tidsformsresultat visar poäng och stjärnor utan extra XP eller ändrad lektionsstatus");
        var after = await Snapshot(true); await Finish(round.Token, round.Csrf);
        check(await Snapshot(true) == after, "Dubbelt avslut ändrar inte tidsformsrepetition igen");
        var stale = await Open(); foreach (var turn in stale.Board.Turns) await Answer(stale.Token, stale.Csrf, turn, turn.Choices.Single(choice => choice.Text == Expected(turn)).Key);
        var oldEditor = await EditForm(ids[0]); var newEditor = new Dictionary<string, string>(oldEditor) { ["TenseContext"] = oldEditor["TenseContext"] + " Ny förklaring." };
        await client.PostAsync("/ContentEditor/Edit/" + ids[0], new FormUrlEncodedContent(newEditor));
        check((await client.PostAsync("/ContentEditor/Edit/" + ids[0], new FormUrlEncodedContent(oldEditor))).StatusCode == HttpStatusCode.Conflict,
            "En gammal redigeringsflik kan inte skriva över en ny tidsformsförklaring");
        var staleBefore = await Snapshot(true); await Finish(stale.Token, stale.Csrf);
        check(await Snapshot(true) == staleBefore && (await Html("/Tenses/Result")).Contains("Din nyare repetition har behållits"),
            "Ändrat tidsformssammanhang hindrar äldre omgångar från att skriva över repetition");
        var revoke = await EditForm(ids[0]); revoke["TenseReviewed"] = "false"; await client.PostAsync("/ContentEditor/Edit/" + ids[0], new FormUrlEncodedContent(revoke));
        check((await guest.GetAsync("/Tenses/Details/" + groupId)).StatusCode == HttpStatusCode.NotFound
            && !(await Html("/Lessons/Details/" + lessonId, guest)).Contains("testform-nu")
            && !(await Html("/Tenses", guest)).Contains("Verifieringsverb"), "Återkallad granskning döljer gruppen och dess ogranskade fras direkt");
        using (var export = JsonDocument.Parse(await client.GetStringAsync("/ContentEditor/Export")))
            check(export.RootElement.GetProperty("Phrases").EnumerateArray().Single(row => row.GetProperty("Id").GetInt32() == ids[0]).GetProperty("TenseGroup").GetString() == "Verifieringsverb – jag",
                "Innehållsexporten bevarar tidsformsgrupper och granskningsuppgifter");
        if (keepPreview)
        {
            var restoreReview = await EditForm(ids[0]); restoreReview["TenseReviewed"] = "true";
            await client.PostAsync("/ContentEditor/Edit/" + ids[0], new FormUrlEncodedContent(restoreReview));
            return;
        }
        // Ta bort enbart dessa syntetiska fixturedata före den frivilliga förhandsvisningen.
        using (var db = new ApplicationDbContext(options))
        {
            await db.QuestionReviewStates.Where(row => ids.Contains(row.QuestionId)).ExecuteDeleteAsync();
            await db.UserProgresses.Where(row => row.LessonId == lessonId).ExecuteDeleteAsync();
            await db.Lessons.Where(row => row.Id == lessonId).ExecuteDeleteAsync();
        }
    }
}
