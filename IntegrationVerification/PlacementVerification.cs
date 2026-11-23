using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class PlacementVerification
{
    private record Round(string Html, Dictionary<string,string> Form, List<string> CorrectKeys);
    private static string Input(string html,string name) => Regex.Match(Regex.Match(html,"<input[^>]*name=\""+Regex.Escape(name)+"\"[^>]*>").Value,"value=\"([^\"]*)\"").Groups[1].Value;
    public static async Task RunAsync(DbContextOptions<ApplicationDbContext> options,HttpClient client,HttpClient other,Action<bool,string> check)
    {
        async Task<string> Html(string path,HttpClient? target=null)=>WebUtility.HtmlDecode(await (target??client).GetStringAsync(path));
        async Task<string> Snapshot(bool includePreferences=true)
        {
            using var db=new ApplicationDbContext(options);
            return JsonSerializer.Serialize(await db.UserProgresses.AsNoTracking().OrderBy(r=>r.Id).ToListAsync())
                +JsonSerializer.Serialize(await db.QuestionReviewStates.AsNoTracking().OrderBy(r=>r.Id).ToListAsync())
                +(includePreferences ? JsonSerializer.Serialize(await db.UserLearningPreferences.AsNoTracking().OrderBy(r=>r.UserId).ToListAsync()) : "");
        }
        var before=await Snapshot();
        async Task<Round> Open()
        {
            var html=await Html("/Placement/Start");
            check(html.Contains("Vad känner du igen?") && !html.Contains("CorrectKey") && !html.Contains("IsCorrect") && !Regex.IsMatch(html,@"[\u1200-\u139F]"),
                "Nivåtestet visar aktuella latinska uttryck utan facit i frågeformuläret");
            var sections=Regex.Matches(html,"<fieldset[^>]*data-placement-question[^>]*>[\\s\\S]*?</fieldset>").Select(m=>m.Value).ToList();
            check(sections.Count==9 && sections.All(s=>Regex.Matches(s,"type=\"radio\"").Count==5),"Nio nivåtestfrågor har fyra svar och ett separat alternativ för att hoppa över");
            var form=new Dictionary<string,string>{["Token"]=Input(html,"Token"),["__RequestVerificationToken"]=Input(html,"__RequestVerificationToken")};
            var correctKeys=new List<string>();
            using var db=new ApplicationDbContext(options);
            var ids=new[]{1,3,10,12,13,14,2001,2002,2003};
            var words=await db.Questions.Include(q=>q.Choices).Where(q=>ids.Contains(q.Id)).ToDictionaryAsync(q=>q.Id);
            for(var i=0;i<sections.Count;i++)
            {
                var word=words[ids[i]];
                var correct=ids[i] is 13 or 14 ? word.LatinText : word.Choices.Single(c=>c.IsCorrect).Text;
                var labels=Regex.Matches(sections[i],"<label[^>]*>[\\s\\S]*?</label>").Select(m=>m.Value).ToList();
                var correctLabel=labels.Single(l=>Regex.Match(l,"<span>([\\s\\S]*?)</span>").Groups[1].Value==correct);
                var choice=Regex.Match(correctLabel,"value=\"([^\"]*)\"").Groups[1].Value;
                correctKeys.Add(choice);
                form[$"Answers[{i}].QuestionKey"]=Input(sections[i],$"Answers[{i}].QuestionKey");
                form[$"Answers[{i}].ChoiceKey"]=choice;
            }
            return new(html,form,correctKeys);
        }
        async Task<HttpResponseMessage> Submit(Dictionary<string,string> form,HttpClient? target=null)=>await (target??client).PostAsync("/Placement/Submit",new FormUrlEncodedContent(form));
        check((await Html("/Placement")).Contains("ingen formell språknivå") && (await Html("/LearningPreferences")).Contains("/Placement")
            && (await Html("/Learn")).Contains("Gör ett frivilligt nivåtest"),"Nivåtestet förklarar begränsningarna och hittas från förkunskapsval och lärväg");
        var round=await Open();
        check((await Submit(new(round.Form){["__RequestVerificationToken"]=""})).StatusCode==HttpStatusCode.BadRequest,"Nivåtestets inskickning kräver antiforgery");
        var otherCsrf=Input(await Html("/LearningPreferences",other),"__RequestVerificationToken");
        check((await Submit(new(round.Form){["__RequestVerificationToken"]=otherCsrf},other)).StatusCode==HttpStatusCode.Conflict,
            "Ett annat konto kan inte använda nivåtestets session även med sin egen giltiga formulärtoken");
        check((await Submit(new(round.Form){["Token"]=round.Form["Token"]+"x"})).StatusCode==HttpStatusCode.Conflict,"Manipulerade testbiljetter avvisas med omstartshjälp");
        check((await client.GetAsync("/Placement/Result?token="+Uri.EscapeDataString(round.Form["Token"]))).StatusCode==HttpStatusCode.Conflict,
            "Ett ofärdigt test har inget resultat att hämta");
        var missing=new Dictionary<string,string>(round.Form);missing.Remove("Answers[0].ChoiceKey");
        var missingResponse=await Submit(missing);
        check(missingResponse.StatusCode==HttpStatusCode.OK && WebUtility.HtmlDecode(await missingResponse.Content.ReadAsStringAsync()).Contains("Besvara varje fråga"),
            "Obesvarade frågor får hjälptext utan ett påhittat resultat");
        check((await Submit(new(round.Form){["Answers[0].QuestionKey"]=round.Form["Answers[1].QuestionKey"]})).StatusCode==HttpStatusCode.BadRequest
            && (await Submit(new(round.Form){["Answers[0].ChoiceKey"]=Guid.NewGuid().ToString()})).StatusCode==HttpStatusCode.BadRequest,
            "Dubblettfrågor och främmande svarsalternativ kan inte påverka rättningen");
        round.Form["Score"]="999";round.Form["Experience"]="999";round.Form["UserId"]="forged";
        var submitted=await Submit(round.Form);
        var resultUrl=submitted.Headers.Location?.ToString()??"";
        check(submitted.StatusCode==HttpStatusCode.Redirect && resultUrl.StartsWith("/Placement/Result"),"Alla nio svar rättas på servern och leder till ett eget resultat");
        var result=await Html(resultUrl);
        check(result.Contains("9 av 9 rätt") && new[]{"Hälsningar","Vardagsfraser","Tilltal","Tidsformer"}.All(result.Contains)
            && Input(result,"Experience")=="3" && result.Contains("Fråga om språk"),
            "Resultatet visar alla fyra områden och föreslår fortsatt lärande för starka grundkunskaper");
        check((await other.GetAsync(resultUrl)).StatusCode==HttpStatusCode.Conflict,"Resultatet skyddas från andra konton");
        check((await Submit(round.Form)).Headers.Location?.ToString()==resultUrl
            && (await Submit(new(round.Form){["Answers[0].ChoiceKey"]=Guid.Empty.ToString()})).StatusCode==HttpStatusCode.BadRequest,
            "Dubbel inskickning återanvänder resultatet och kan inte ändra en avslutad omgång");
        check(await Snapshot()==before,"Nivåtest och omladdade resultat ändrar inga XP, lektionsresultat, repetitionssvar eller förkunskapsval");
        foreach(var (correctCount,experience) in new[]{(0,"1"),(4,"2")})
        {
            var mixed=await Open();
            for(var i=correctCount;i<9;i++)mixed.Form[$"Answers[{i}].ChoiceKey"]=Guid.Empty.ToString();
            var response=await Submit(mixed.Form); var page=await Html(response.Headers.Location!.ToString());
            foreach (var (area, count) in new[] { ("Tilltal", 2), ("Tidsformer", 3) })
            {
                check(page.Contains("Träna på " + area.ToLowerInvariant()), "Missade områden länkar till kort övning");
                var url = "/Placement/Practice?token=" + Uri.EscapeDataString(mixed.Form["Token"]) + "&area=" + area;
                for (int step = 1; step <= count; step++)
                {
                    var question = await Html(url + "&step=" + step);
                    var choices = Regex.Matches(question, "name=\"choice\" value=\"([^\"]+)\"");
                    check(choices.Count == 4, "Situationen visar fyra uttryck");
                    var feedback = await Html(url + "&step=" + step + "&choice=" + choices[0].Groups[1].Value);
                    check(feedback.Contains(step == count ? "Övningen är klar!" : "Nästa situation"), "Övningen ger återkoppling och visar nästa steg");
                }
            }
            check(page.Contains($"{correctCount} av 9 rätt") && Input(page,"Experience")==experience
                && page.Contains($"{9-correctCount} frågor hoppades över"),
                "Överhoppade frågor ger ärlig områdesåterkoppling och en försiktig startrekommendation");
        }
        var stale=await Open();
        using(var db=new ApplicationDbContext(options))
        {
            var word=await db.Questions.SingleAsync(q=>q.Id==10);var original=word.LatinText;
            word.LatinText+=" verifieringsändring";await db.SaveChangesAsync();
            check((await Submit(stale.Form)).StatusCode==HttpStatusCode.Conflict,"En frasändring efter teststart kräver en ny omgång före rättning");
            word.LatinText=original;await db.SaveChangesAsync();
            var audience=await db.Questions.SingleAsync(q=>q.Id==13);var originalAudience=audience.Audience;
            audience.Audience=PhraseAudience.Woman;await db.SaveChangesAsync();
            check((await client.GetAsync("/Placement/Start")).StatusCode==HttpStatusCode.Conflict,"Ändrat tilltalsunderlag pausar testet i stället för att ställa en felaktig könsfråga");
            audience.Audience=originalAudience;await db.SaveChangesAsync();
        }
        check(await Snapshot()==before,"Även blandade, överhoppade och inaktuella omgångar lämnar elevens historik orörd");
        // Eleven väljer uttryckligen att använda förslaget; denna ändring får bara röra självskattningen.
        var history=await Snapshot(false);
        var apply=await client.PostAsync("/LearningPreferences",new FormUrlEncodedContent(new Dictionary<string,string>{
            ["__RequestVerificationToken"]=Input(result,"__RequestVerificationToken"),["Experience"]=Input(result,"Experience"),["ReturnUrl"]=Input(result,"ReturnUrl")
        }));
        check(apply.StatusCode==HttpStatusCode.Redirect && apply.Headers.Location?.ToString()=="/Lessons/Details/1001" && await Snapshot(false)==history,
            "Använd förslaget öppnar startlektionen utan att ge XP eller klara lektioner");
        // Återställ endast testkontots självskattning till dess värde före testet.
        await client.PostAsync("/LearningPreferences",new FormUrlEncodedContent(new Dictionary<string,string>{
            ["__RequestVerificationToken"]=Input(await Html("/LearningPreferences"),"__RequestVerificationToken"),["Experience"]="1"
        }));
        check(await Snapshot()==before,"Testet återställer sitt uttryckliga förkunskapsval utan att lämna fixturedata");
    }
}
