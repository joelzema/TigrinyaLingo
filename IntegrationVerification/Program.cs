using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Domain.Entities;
using TigrinyaLingo.Models;

var testName = "TigrinyaLingo_Verification_" + Guid.NewGuid().ToString("N");
var sql = new SqlConnectionStringBuilder
{
    DataSource = Environment.GetEnvironmentVariable("TIGRINYALINGO_TEST_SERVER") ?? @"(localdb)\MSSQLLocalDB",
    InitialCatalog = testName,
    IntegratedSecurity = true,
    TrustServerCertificate = true
};
var services = new ServiceCollection();
services.AddIdentityCore<IdentityUser>(options => options.Stores.MaxLengthForKeys = 128);
using var provider = services.BuildServiceProvider();
var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(sql.ConnectionString, settings => settings.MigrationsAssembly("Infrastructure"))
    .UseApplicationServiceProvider(provider)
    .Options;

void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    Console.WriteLine("PASS: " + message);
}

async Task<string[]> IdentitySignature(string connectionString)
{
    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();
    var signatures = new List<string>();
    foreach (var query in new[]
    {
        """
        SELECT 'COLUMN', s.name, t.name, c.name, ty.name, c.max_length,
            c.precision, c.scale, c.is_nullable, c.is_identity, ISNULL(dc.definition, '')
        FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id
        JOIN sys.columns c ON c.object_id = t.object_id
        JOIN sys.types ty ON c.user_type_id = ty.user_type_id
        LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
        WHERE t.name LIKE 'AspNet%'
        """,
        """
        SELECT 'INDEX', t.name, i.name, i.is_unique, i.is_primary_key,
            ISNULL(i.filter_definition, ''), c.name, ic.key_ordinal, ic.is_descending_key, ic.is_included_column
        FROM sys.tables t JOIN sys.indexes i ON i.object_id = t.object_id
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE t.name LIKE 'AspNet%'
        """,
        """
        SELECT 'FK', t.name, c.name, rt.name, rc.name, fk.delete_referential_action, fk.update_referential_action
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fc ON fc.constraint_object_id = fk.object_id
        JOIN sys.tables t ON t.object_id = fc.parent_object_id
        JOIN sys.columns c ON c.object_id = t.object_id AND c.column_id = fc.parent_column_id
        JOIN sys.tables rt ON rt.object_id = fc.referenced_object_id
        JOIN sys.columns rc ON rc.object_id = rt.object_id AND rc.column_id = fc.referenced_column_id
        WHERE t.name LIKE 'AspNet%'
        """
    })
    {
        using var command = new SqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);
            signatures.Add(string.Join("|", values));
        }
    }
    return signatures.Order(StringComparer.Ordinal).ToArray();
}

Process? app = null;
Process? productionApp = null;
var createdDatabase = false;
var emptyCourseId = 0;
var emptyLessonId = 0;
var audioFixturePaths = new List<string>();
try
{
    async Task<bool> CanEditContent(bool development, string? remoteIp, bool signedIn)
    {
        var policyServices = new ServiceCollection();
        policyServices.AddLogging();
        policyServices.AddAuthorization(settings => TigrinyaLingo.Authorization.LocalContentEditing.Configure(settings, development));
        using var policyProvider = policyServices.BuildServiceProvider();
        var authorization = policyProvider.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        var identity = new System.Security.Claims.ClaimsIdentity(signedIn ? "test" : null);
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        http.Connection.RemoteIpAddress = remoteIp is null ? null : System.Net.IPAddress.Parse(remoteIp);
        return (await authorization.AuthorizeAsync(
            new System.Security.Claims.ClaimsPrincipal(identity), http, TigrinyaLingo.Authorization.LocalContentEditing.Policy)).Succeeded;
    }
    Assert(await CanEditContent(true, "127.0.0.1", true)
        && !await CanEditContent(true, "192.0.2.1", true)
        && !await CanEditContent(true, null, true)
        && !await CanEditContent(false, "127.0.0.1", true)
        && !await CanEditContent(true, "127.0.0.1", false),
        "Innehållsredigering kräver inloggning, utvecklingsläge och verklig loopback-adress");
    // Kontrollera servern först. En trasig anslutning betyder inte att databasen är ny.
    var masterSql = new SqlConnectionStringBuilder(sql.ConnectionString) { InitialCatalog = "master" };
    using (var availability = new SqlConnection(masterSql.ConnectionString))
        await availability.OpenAsync();
    using (var context = new ApplicationDbContext(options))
    {
        Assert(!await context.Database.CanConnectAsync(), "Testdatabasen är ny och fristående");
        createdDatabase = true;
        await context.GetService<IMigrator>().MigrateAsync("20260121112847_Init");
        var existingSql = new SqlConnectionStringBuilder(sql.ConnectionString) { InitialCatalog = "TigrinyaLingoDb" };
        var existing = await IdentitySignature(existingSql.ConnectionString);
        var baseline = await IdentitySignature(sql.ConnectionString);
        Assert(existing.Length > 0 && existing.SequenceEqual(baseline),
            "Återskapad Identity-baslinje matchar befintliga kolumner, index och främmande nycklar");

        var migrationScript = context.GetService<IMigrator>().GenerateScript("20260121112847_Init");
        Assert(!Regex.IsMatch(migrationScript, @"^\s*(DROP|DELETE|TRUNCATE)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline),
            "Den nya migrationen innehåller inga raderingskommandon");
        Assert(!Regex.IsMatch(migrationScript, @"CREATE TABLE \[AspNet", RegexOptions.IgnoreCase),
            "Den nya migrationen återskapar inga Identity-tabeller");
        await context.GetService<IMigrator>().MigrateAsync("20261113225238_SavePhraseCorrections");
        var originalLatin = await context.Questions.Where(question => question.Id == 2).Select(question => question.LatinText).SingleAsync();
        await context.Database.ExecuteSqlRawAsync("UPDATE Questions SET AudioFileName = 'my-teacher-recording.mp3' WHERE Id = 1; UPDATE Questions SET AudioFileName = 'selam.mp3' WHERE Id = 33; UPDATE Questions SET LatinText = LatinText + N' (egen ändring)' WHERE Id = 2;");
        var unchangedBefore = System.Text.Json.JsonSerializer.Serialize(await context.Questions.AsNoTracking().Where(question => question.Id < 1000).OrderBy(question => question.Id)
            .Select(question => new { question.Id, question.Text, question.LatinText, question.LessonId }).ToListAsync());
        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();
        Assert((await context.Questions.SingleAsync(question => question.Id == 1)).AudioFileName == "my-teacher-recording.mp3"
            && (await context.Questions.SingleAsync(question => question.Id == 33)).AudioFileName is null
            && !await context.Questions.AnyAsync(question => question.AudioFileName != null && question.AudioFileName != "my-teacher-recording.mp3")
            && unchangedBefore == System.Text.Json.JsonSerializer.Serialize(await context.Questions.AsNoTracking().Where(question => question.Id < 1000).OrderBy(question => question.Id)
                .Select(question => new { question.Id, question.Text, question.LatinText, question.LessonId }).ToListAsync()),
            "Ljudmigrationen tar bara bort kända Omniglot-kopplingar och bevarar egna inspelningar och frasändringar");
        (await context.Questions.SingleAsync(question => question.Id == 1)).AudioFileName = null;
        (await context.Questions.SingleAsync(question => question.Id == 2)).LatinText = originalLatin;
        await context.SaveChangesAsync();

        Assert(!(await context.Database.GetPendingMigrationsAsync()).Any(), "Alla migrationer kan köras i SQL Server LocalDB");
        Assert(!context.Database.HasPendingModelChanges(), "Modellsnapshot matchar appens datamodell");
        Assert(await context.Questions.Where(question => question.Id < 1000).AllAsync(question => question.Audience == PhraseAudience.Automatic),
            "Tilltalsmigrationen bevarar alla fraser och ger även befintliga rader automatiskt tilltal");
        foreach (var pair in new[] {
            ("menyu shimka", "menyu shimki"), ("kemey aleka", "kemey aleki"), ("kabey adi metsika", "kabey adi metsiki"),
            ("kemey hadirka", "kemey hadirki"), ("kemey wu’elka", "kemey wu’elki"), ("kemey amsika", "kemey amsiki"),
            ("kes elka tezareb bejaka", "kes elki tezarebi bejaki"), ("eske digemeley", "eske digemiley"),
            ("tsehafo bejaka", "tsehafiyo bejaki"), ("bejaka", "bejaki"), ("genzebka", "genzebki") })
            Assert(PhraseUsageGuide.For(pair.Item1) is { Label: "(m) Till en man" } man && man.OtherForm == pair.Item2
                && PhraseUsageGuide.For(pair.Item2) is { Label: "(f) Till en kvinna" } woman && woman.OtherForm == pair.Item1,
                "Tilltalspar har rätt mottagare och motsvarande form: " + pair.Item1);
        Assert(PhraseUsageGuide.For("  KEMEY 'ALEKI?  ")?.Label == "(f) Till en kvinna"
            && PhraseUsageGuide.For("kemey wu'elka")?.OtherForm == "kemey wu’elki"
            && PhraseUsageGuide.For("okänt ord med ka") is null && PhraseUsageGuide.For("selam") is null,
            "Stavningsnotation normaliseras men okända uttryck och neutrala hälsningar får inget påhittat kön");
        Assert(PhraseUsageGuide.For("eget uttryck", PhraseAudience.Woman)?.Label == "(f) Till en kvinna"
            && PhraseUsageGuide.For("kemey aleka", PhraseAudience.SameForBoth)?.OtherForm is null
            && PhraseUsageGuide.For("dehan w’alu")?.Label == "Till flera / artigt tilltal"
            && PhraseUsageGuide.For("bdehan eto")?.Label == "(m) Till en man",
            "Egna tilltal och artigt tilltal fungerar utan felaktiga motformer");
        var genderedIds = new[] { 6, 7, 13, 14, 15, 16, 17, 18, 19, 21, 22, 23, 25, 26, 28, 32 };
        var gendered = await context.Questions.Where(question => genderedIds.Contains(question.Id)).ToListAsync();
        Assert(gendered.All(question => PhraseUsageGuide.For(question.LatinText, question.Audience) is not null),
            "Alla befintliga tilltalsberoende grundfraser har en uttrycklig markering");
        foreach (var table in new[] { "Courses", "Lessons", "Questions", "Choices", "UserProgresses", "QuestionReviewStates" })
        {
            using var connection = new SqlConnection(sql.ConnectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand("SELECT COUNT(*) FROM sys.tables WHERE name = @name", connection);
            command.Parameters.AddWithValue("@name", table);
            Assert(Convert.ToInt32(await command.ExecuteScalarAsync()) == 1, table + " skapas av migrationen");
        }

        await context.Database.MigrateAsync();
        Assert(await context.Courses.CountAsync() == 1 && await context.Lessons.CountAsync() == 19
            && await context.Questions.CountAsync() == 77 && await context.Choices.CountAsync() == 308,
            "Grundkursens nitton lektioner läggs in en gång och dubbleras inte vid ny migrationskörning");
        var questions = await context.Questions.Where(question => question.LessonId == 1).Include(question => question.Choices).OrderBy(question => question.Id).ToListAsync();
        Assert(questions.All(question => question.Choices.Count == 4 && question.Choices.Count(choice => choice.IsCorrect) == 1),
            "Varje ord har fyra alternativ och exakt en svensk betydelse");
        Assert(questions.Select(question => question.Text).SequenceEqual(new[] { "ሰላም", "መርሓባ", "የቐንየለይ", "ሰላማት" }),
            "Tigrinjatecknen lagras korrekt i SQL Server");
        Assert(questions.Select(question => question.LatinText).SequenceEqual(new[] { "selam", "merhaba", "yekenyeley", "selamat" }),
            "Tigrinja med latinska bokstäver lagras för alla fyra ord");
        Assert(questions.All(question => question.AudioFileName is null),
            "Grundorden har inga borttagna Omniglot-kopplingar");

        var fixtureCourse = new Course { Title = "Tom testkurs", Description = "Test av tomma sidor", Category = "Test" };
        context.Courses.Add(fixtureCourse);
        await context.SaveChangesAsync();
        emptyCourseId = fixtureCourse.Id;
    }

    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    var webRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../TigrinyaLingo"));
    var start = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = webRoot,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    start.ArgumentList.Add(Path.Combine(webRoot, "bin/Debug/net9.0/TigrinyaLingo.dll"));
    start.ArgumentList.Add("--urls");
    start.ArgumentList.Add("http://127.0.0.1:" + port);
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["ConnectionStrings__DefaultConnection"] = sql.ConnectionString;
    app = Process.Start(start)!;
    app.OutputDataReceived += (_, _) => { };
    app.ErrorDataReceived += (_, _) => { };
    app.BeginOutputReadLine();
    app.BeginErrorReadLine();
    var handler = new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() };
    using var client = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:" + port), Timeout = TimeSpan.FromSeconds(10) };
    var ready = false;
    for (var attempt = 0; attempt < 50; attempt++)
    {
        if (app.HasExited) throw new InvalidOperationException("Webbappen kunde inte starta.");
        try { ready = (await client.GetAsync("/")).IsSuccessStatusCode; } catch (HttpRequestException) { }
        if (ready) break;
        await Task.Delay(200);
    }
    Assert(ready, "MVC-startsidan svarar");
    foreach (var path in new[] { "/Placement", "/Placement/Start", "/Placement/Result", "/LearningPreferences", "/AddressPractice", "/AddressPractice/Play", "/AddressPractice/Result", "/Conversations", "/Conversations/Play/first-meeting", "/Conversations/Result", "/Learn", "/Games", "/Games/Match", "/Games/Result", "/PhraseCreation", "/ContentEditor", "/ContentEditor/Edit/1", "/ContentEditor/Export", "/DailyTraining", "/DailyTraining/Result", "/Quiz/Index/1", "/Quiz/Result/1", "/Quiz/Review/1", "/Quiz/ReviewResult/1", "/Progress" })
    {
        var protectedPage = await client.GetAsync(path);
        Assert(protectedPage.StatusCode == HttpStatusCode.Redirect
            && protectedPage.Headers.Location!.ToString().Contains("/Identity/Account/Login"),
            path + " kräver inloggning");
    }

    var homeHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/"));
    Assert(homeHtml.Contains("Utforska kurser") && homeHtml.Contains("href=\"/Courses\""),
        "Startsidan länkar till kurslistan");
    var coursesHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Courses"));
    Assert(coursesHtml.Contains("Tigrinja för nybörjare") && coursesHtml.Contains("/Courses/Details/1"),
        "Kurslistan hämtar kursen från databasen och länkar till lektionerna");
    var courseHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Courses/Details/1"));
    Assert(courseHtml.Contains("Dina första hälsningar") && courseHtml.Contains("/Lessons/Details/1"),
        "Kurssidan visar den första lektionen");
    Assert(courseHtml.Contains("Börja med: Dina första hälsningar") && courseHtml.Contains("Börja här")
        && !courseHtml.Contains("id=\"course-progress\""),
        "Utloggad kursbesökare får en startlektion utan personliga framsteg");
    var lessonHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Lessons/Details/1"));
    Assert(new[] { "Hej", "Välkommen", "Tack", "Hej då", "selam", "merhaba", "yekenyeley", "selamat" }.All(lessonHtml.Contains)
        && lessonHtml.Contains("lang=\"ti-Latn\"") && Regex.IsMatch(lessonHtml, "<h2[^>]*>Hej</h2>")
        && !lessonHtml.Contains("Visa tigrinjatecken"),
        "Naturliga svenska betydelser visas med tigrinja enbart i latinska bokstäver");
    foreach (var page in new[] { homeHtml, coursesHtml, courseHtml, lessonHtml })
        Assert(!Regex.IsMatch(page, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"),
            "Ingen tigrinjaskrift finns i HTML för start, kurser eller hälsningslektionen");
    Assert(!lessonHtml.Contains("<audio") && !lessonHtml.Contains("Asmeret Hagos") && lessonHtml.Contains("ännu inga uttalsinspelningar"), "Lektionen fungerar utan spelare eller inaktuell ljudkälla");
    var introductionsHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Lessons/Details/2"));
    Assert(courseHtml.Contains("/Lessons/Details/2") && courseHtml.IndexOf("/Lessons/Details/1") < courseHtml.IndexOf("/Lessons/Details/2")
        && introductionsHtml.Contains("ersätter du punkterna med ditt eget namn") && introductionsHtml.Contains("frågar en man")
        && introductionsHtml.Contains("frågar en kvinna") && Regex.Matches(introductionsHtml, "<audio").Count == 0,
        "Presentationer visas som lektion 2 utan ljud och med tips om tilltalsformer");
    Assert(!Regex.IsMatch(introductionsHtml, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]")
        && Regex.Matches(introductionsHtml, "<h2[^>]*>Vad heter du\\?</h2>").Count == 2
        && !Regex.IsMatch(introductionsHtml, "<h2[^>]*>[^<]*\\(till en"),
        "Båda tilltalsformerna översätts till Vad heter du utan att blanda in förklaring i översättningen");
    using (var context = new ApplicationDbContext(options))
    {
        var presentationQuestions = await context.Questions.Include(question => question.Choices).Where(question => question.LessonId == 2).ToListAsync();
        Assert(presentationQuestions.All(question => question.Choices.Select(choice => choice.Text).Distinct().Count() == 4
            && question.Choices.Count(choice => choice.IsCorrect) == 1),
            "Förenklade quizalternativ har fortfarande fyra olika svar och exakt ett facit");
        Assert(presentationQuestions.Where(question => question.Id == 6 || question.Id == 7)
            .All(question => question.Choices.Single(choice => choice.IsCorrect).Text == "Vad heter du?"),
            "Menyu shimka och menyu shimki delar samma naturliga svenska betydelse");
    }
    var everydayHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Lessons/Details/3"));
    Assert(courseHtml.Contains("/Lessons/Details/3") && courseHtml.IndexOf("/Lessons/Details/2") < courseHtml.IndexOf("/Lessons/Details/3")
        && new[] { "Jag förstår", "Jag förstår inte", "Jag vet inte", "Förlåt", "teredi’uni", "aytered’anin", "ayfeletikun", "yiqreta" }.All(everydayHtml.Contains)
        && Regex.Matches(everydayHtml, "<audio").Count == 0
        && !everydayHtml.Contains("Tigrinja med latinska bokstäver")
        && !Regex.IsMatch(everydayHtml, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"),
        "Lektion 3 har fyra naturliga vardagsfraser utan ljud och med latinsk skrivning");
    foreach (var name in new[] { "selam.mp3", "merhaba.mp3", "yekenyeley.mp3", "selamat.mp3",
        "simey-yebehal.mp3", "menyu-shimka.mp3", "menyu-shimki.mp3", "ane-kab-metsiye.mp3",
        "terediuni.mp3", "ayteredanin.mp3", "ayfeletikun.mp3", "yiqreta.mp3" }.Concat(Enumerable.Range(13, 20).Select(id => $"phrase-{id}.mp3")))
    {
        using var recording = await client.GetAsync("/audio/tigrinya/" + name);
        Assert(recording.StatusCode == HttpStatusCode.NotFound && !File.Exists(Path.Combine(webRoot, "wwwroot/audio/tigrinya", name)),
            name + " är borttagen och kan inte hämtas från appen");
    }
    Assert((await client.GetAsync("/Courses/Details/2147483647")).StatusCode == HttpStatusCode.NotFound
        && (await client.GetAsync("/Lessons/Details/2147483647")).StatusCode == HttpStatusCode.NotFound,
        "Saknade kurser och lektioner ger 404");
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Courses/Details/" + emptyCourseId)).Contains("Lektioner kommer snart"),
        "En tom kurs visar ett begripligt meddelande");
    using (var fixtureContext = new ApplicationDbContext(options))
    {
        var later = new Lesson { CourseId = emptyCourseId, Order = 10, Title = "Ordning10" };
        var earlier = new Lesson { CourseId = emptyCourseId, Order = 2, Title = "Ordning2" };
        fixtureContext.Lessons.AddRange(later, earlier);
        await fixtureContext.SaveChangesAsync();
        emptyLessonId = later.Id;
    }
    var orderedHtml = await client.GetStringAsync("/Courses/Details/" + emptyCourseId);
    Assert(orderedHtml.IndexOf("Ordning2", StringComparison.Ordinal) < orderedHtml.IndexOf("Ordning10", StringComparison.Ordinal),
        "Lektion 2 visas före lektion 10 oavsett insättningsordning");
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Lessons/Details/" + emptyLessonId)).Contains("Innehåll kommer snart"),
        "En tom lektion visar ett begripligt meddelande");
    async Task<string> Token(string path, HttpClient? target = null)
    {
        var response = await (target ?? client).GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var input = Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;
        var value = Regex.Match(input, "value=\"([^\"]+)\"").Groups[1].Value;
        Assert(value.Length > 0, "Formuläret har en antiforgery-token: " + path);
        return WebUtility.HtmlDecode(value);
    }

    var registerHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Identity/Account/Register"));
    Assert(registerHtml.Contains("Skapa konto") && registerHtml.Contains("Minst 6 tecken")
        && registerHtml.Contains("Inget bekräftelsemejl skickas") && registerHtml.Contains("specialtecken"),
        "Registreringen visar svenska lösenordskrav och förklarar den lokala bekräftelsen");
    var noRegistrationToken = await client.PostAsync("/Identity/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>()));
    Assert(noRegistrationToken.StatusCode == HttpStatusCode.BadRequest, "Registreringen kräver antiforgery-token");
    var weakRegistration = new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = await Token("/Identity/Account/Register"),
        ["Input.DisplayName"] = "TestElev", ["Input.Email"] = "weak-verification@example.invalid", ["Input.Password"] = "abc", ["Input.ConfirmPassword"] = "abc"
    };
    var weakResponse = await client.PostAsync("/Identity/Account/Register", new FormUrlEncodedContent(weakRegistration));
    var weakHtml = WebUtility.HtmlDecode(await weakResponse.Content.ReadAsStringAsync());
    Assert(weakResponse.StatusCode == HttpStatusCode.OK && weakHtml.Contains("minst 6 tecken")
        && weakHtml.Contains("stor bokstav") && weakHtml.Contains("siffra") && weakHtml.Contains("specialtecken"),
        "Svaga lösenord avvisas med konkreta svenska felmeddelanden");
    weakRegistration["Input.Password"] = "Verification!4820x";
    weakRegistration["Input.ConfirmPassword"] = "Annan!4820x";
    weakRegistration["__RequestVerificationToken"] = await Token("/Identity/Account/Register");
    var mismatchResponse = await client.PostAsync("/Identity/Account/Register", new FormUrlEncodedContent(weakRegistration));
    Assert(WebUtility.HtmlDecode(await mismatchResponse.Content.ReadAsStringAsync()).Contains("Lösenorden stämmer inte överens"),
        "Olika lösenord ger svensk hjälptext");
    using (var context = new ApplicationDbContext(options))
        Assert(!await context.Users.AnyAsync(), "Misslyckad registrering skapar inga konton");
    var email = "verification@example.invalid";
    var password = "Verification!4820x";
    var registration = new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = await Token("/Identity/Account/Register"),
        ["Input.DisplayName"] = "TestElev", ["Input.Email"] = email,
        ["Input.Password"] = password,
        ["Input.ConfirmPassword"] = password
    };
    var registered = await client.PostAsync("/Identity/Account/Register?returnUrl=%2FQuiz%2FIndex%2F1", new FormUrlEncodedContent(registration));
    Assert(registered.StatusCode == HttpStatusCode.Redirect && registered.Headers.Location!.ToString().Contains("RegisterConfirmation"),
        "Registrering leder till kontobekräftelse");

    var immediateConfirmation = await client.GetStringAsync(registered.Headers.Location);
    Assert(immediateConfirmation.Contains("id=\"confirm-link\""), "Nyregistrerat konto har en lokal bekräftelseknapp direkt efter registrering");
    async Task<HttpResponseMessage> Login(string candidatePassword, string? accountEmail = null, HttpClient? target = null, string returnUrl = "/", bool rememberMe = false)
    {
        return await (target ?? client).PostAsync("/Identity/Account/Login?returnUrl=" + Uri.EscapeDataString(returnUrl), new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token("/Identity/Account/Login", target),
            ["Input.Email"] = accountEmail ?? email,
            ["Input.Password"] = candidatePassword,
            ["Input.RememberMe"] = rememberMe.ToString()
        }));
    }
    var beforeConfirmation = await Login(password);
    Assert(beforeConfirmation.StatusCode == HttpStatusCode.OK
        && !(await client.GetStringAsync("/")).Contains(">TestElev</button>"), "Obekräftat konto kan inte logga in");
    using (var stranger = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() }) { BaseAddress = client.BaseAddress })
    {
        var foreignConfirmation = await stranger.GetStringAsync(registered.Headers.Location);
        Assert(!foreignConfirmation.Contains("id=\"confirm-link\""),
            "En annan webbläsare kan inte hämta lokal bekräftelse för kontot med bara mejladressen");
    }
    var mismatchedConfirmation = await client.GetStringAsync("/Identity/Account/RegisterConfirmation?email=annan%40example.invalid");
    Assert(!mismatchedConfirmation.Contains("id=\"confirm-link\""), "Lokal bekräftelse matchar kontot som just registrerades");
    var confirmationPage = await client.GetStringAsync(registered.Headers.Location);
    Assert(WebUtility.HtmlDecode(confirmationPage).Contains("Bekräfta mitt konto")
        && WebUtility.HtmlDecode(confirmationPage).Contains("Inget mejl skickas"),
        "Bekräftelsesidan visar en svensk knapp utan att utlova ett mejl");
    // Samma kontoflöde får aldrig exponera den lokala knappen i Production.
    var productionListener = new TcpListener(IPAddress.Loopback, 0);
    productionListener.Start();
    var productionPort = ((IPEndPoint)productionListener.LocalEndpoint).Port;
    productionListener.Stop();
    var productionStart = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = webRoot, UseShellExecute = false, CreateNoWindow = true,
        RedirectStandardOutput = true, RedirectStandardError = true
    };
    productionStart.ArgumentList.Add(Path.Combine(webRoot, "bin/Debug/net9.0/TigrinyaLingo.dll"));
    productionStart.ArgumentList.Add("--urls");
    productionStart.ArgumentList.Add("http://127.0.0.1:" + productionPort);
    productionStart.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
    productionStart.Environment["ConnectionStrings__DefaultConnection"] = sql.ConnectionString;
    productionApp = Process.Start(productionStart)!;
    productionApp.OutputDataReceived += (_, _) => { };
    productionApp.ErrorDataReceived += (_, _) => { };
    productionApp.BeginOutputReadLine();
    productionApp.BeginErrorReadLine();
    using (var productionClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = handler.CookieContainer })
        { BaseAddress = new Uri("http://127.0.0.1:" + productionPort), Timeout = TimeSpan.FromSeconds(10) })
    {
        var productionReady = false;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (productionApp.HasExited) throw new InvalidOperationException("Production-testappen kunde inte starta.");
            try { productionReady = (await productionClient.GetAsync("/")).IsSuccessStatusCode; } catch (HttpRequestException) { }
            if (productionReady) break;
            await Task.Delay(200);
        }
        Assert(productionReady, "Separat Production-testapp svarar");
        var productionConfirmation = await productionClient.GetStringAsync(registered.Headers.Location);
        Assert(!productionConfirmation.Contains("id=\"confirm-link\""),
            "Production visar ingen lokal bekräftelselänk även med registreringswebbläsarens skyddade cookie");
        var productionForm = new Dictionary<string, string>(registration)
        {
            ["Input.Email"] = "production-verification@example.invalid",
            ["__RequestVerificationToken"] = await Token("/Identity/Account/Login", productionClient)
        };
        var productionRegistration = await productionClient.PostAsync("/Identity/Account/Register", new FormUrlEncodedContent(productionForm));
        Assert(productionRegistration.StatusCode == HttpStatusCode.OK
            && WebUtility.HtmlDecode(await productionRegistration.Content.ReadAsStringAsync()).Contains("Registrering är inte öppnad"),
            "Production tillåter inte nyregistrering med lokal bekräftelse");
        using var productionContext = new ApplicationDbContext(options);
        Assert(!await productionContext.Users.AnyAsync(user => user.Email == "production-verification@example.invalid"),
            "Avvisad Production-registrering skapar inget konto");
    }
    productionApp.Kill(entireProcessTree: true);
    await productionApp.WaitForExitAsync();
    productionApp.Dispose();
    productionApp = null;
    var confirmationInput = Regex.Match(confirmationPage, "<a[^>]*id=\"confirm-link\"[^>]*>").Value;
    var confirmationLink = WebUtility.HtmlDecode(Regex.Match(confirmationInput, "href=\"([^\"]+)\"").Groups[1].Value);
    Assert(confirmationLink.Length > 0, "Utvecklingsflödet erbjuder en kontobekräftelselänk");
    Assert(confirmationLink.Contains("returnUrl="), "Quizets returadress följer med bekräftelselänken");
    var badConfirmationLink = Regex.Replace(confirmationLink, @"([?&]code=)[^&]+", "${1}%25%25%25");
    var badConfirmation = WebUtility.HtmlDecode(await client.GetStringAsync(badConfirmationLink));
    Assert(badConfirmation.Contains("Kontot kunde inte bekräftas"), "Trasig bekräftelsekod ger svensk hjälptext utan serverfel");
    var confirmed = await client.GetAsync(confirmationLink);
    confirmed.EnsureSuccessStatusCode();
    Assert(WebUtility.HtmlDecode(await confirmed.Content.ReadAsStringAsync()).Contains("Logga in och fortsätt"),
        "Bekräftat konto får ett tydligt nästa steg till inloggningen");
    Assert(!(await client.GetStringAsync(registered.Headers.Location)).Contains("id=\"confirm-link\""),
        "Den lokala bekräftelselänken tas bort efter lyckad bekräftelse");
    var wrongPassword = await Login("Incorrect!12345x");
    Assert(wrongPassword.StatusCode == HttpStatusCode.OK
        && !(await client.GetStringAsync("/")).Contains(">TestElev</button>"), "Fel lösenord ger ingen inloggning");
    Assert((await client.GetAsync("/Home/Privacy")).StatusCode == HttpStatusCode.NotFound, "Den borttagna Privacy-sidan saknar route");
    var unsafeReturn = await client.GetStringAsync("/Identity/Account/Login?returnUrl=https%3A%2F%2Fexample.invalid%2F");
    Assert(!unsafeReturn.Contains("example.invalid"), "Inloggningens formulär släpper inte igenom en extern returadress");
    var loggedIn = await Login(password, returnUrl: "/Quiz/Index/1", rememberMe: true);
    Assert(loggedIn.Headers.Location?.ToString() == "/Quiz/Index/1", "Inloggningen återvänder till det quiz besökaren valde");
    Assert(loggedIn.Headers.TryGetValues("Set-Cookie", out var loginCookies) && loginCookies.Any(cookie => cookie.StartsWith(".AspNetCore.Identity.Application=") && cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase)), "Kom ihåg mig skapar en beständig inloggningscookie när det väljs");
    Assert(loggedIn.StatusCode == HttpStatusCode.Redirect
        && (await client.GetStringAsync("/")).Contains(">TestElev</button>"), "Bekräftat konto kan logga in och känns igen av MVC");

    using (var context = new ApplicationDbContext(options))
        Assert(await context.UserProgresses.CountAsync() == 0, "Läsning av lektioner markerar inga framsteg");
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Games/Match")).Contains("Snart kan du spela!"), "Nya användare får en väg till första lektionen före spelet");
    Assert((await client.GetAsync("/Conversations/Play/first-meeting")).StatusCode == HttpStatusCode.Forbidden
        && WebUtility.HtmlDecode(await client.GetStringAsync("/Conversations")).Contains("Gör klart lektionen"),
        "Ett nytt konto behöver klara grunderna innan vardagssamtalen öppnas");
    Assert((await client.GetAsync("/Learn")).Headers.Location?.ToString().StartsWith("/LearningPreferences") == true
        && (await client.GetAsync("/Lessons/Details/2")).Headers.Location?.ToString().StartsWith("/LearningPreferences") == true
        && (await client.GetAsync("/Quiz/Index/2")).Headers.Location?.ToString().StartsWith("/LearningPreferences") == true,
        "Nytt konto får välja förkunskaper före första lektionsstarten");
    var nav = Regex.Match(WebUtility.HtmlDecode(await client.GetStringAsync("/")), "<nav\\b[\\s\\S]*?</nav>").Value;
    Assert(nav.Contains(">Kurser</a>") && nav.Contains(">Fortsätt lära</a>") && nav.Contains(">Träna</a>")
        && !nav.Contains(">Start</a>") && !nav.Contains(">Tidsformer</a>") && nav.Contains("dropdown-menu-end"), "Inloggad toppmeny samlar lärande, träning och konto");
    Assert((await client.GetStringAsync("/Courses")).Contains("Utforska tidsformer") && (await client.GetStringAsync("/Games")).Contains("Starta skrivdialogen"), "Kursmodulen och skrivdialogen finns på samlingssidorna");
    var profilePath = "/Identity/Account/Manage/Index";
    var profileHome = await client.GetStringAsync("/");
    Assert(!profileHome.Contains(email) && !profileHome.Contains("Home/Privacy"), "Navigeringen visar valt namn utan mejladress eller Privacy");
    async Task<HttpResponseMessage> SaveName(string name) => await client.PostAsync(profilePath, new FormUrlEncodedContent(new Dictionary<string,string> {
        ["__RequestVerificationToken"] = await Token(profilePath), ["Input.DisplayName"] = name
    }));
    Assert((await SaveName(email)).StatusCode == HttpStatusCode.OK && (await client.GetStringAsync("/")).Contains(">TestElev</button>"),
        "En mejladress godtas inte som visat användarnamn");
    Assert((await SaveName("AndraNamnet")).StatusCode == HttpStatusCode.Redirect && (await client.GetStringAsync("/")).Contains(">AndraNamnet</button>"),
        "Namnbyte uppdaterar den inloggade menyn direkt");
    await SaveName("TestElev");
    var choiceResponse = await client.PostAsync("/LearningPreferences", new FormUrlEncodedContent(new Dictionary<string,string> {
        ["__RequestVerificationToken"] = await Token("/LearningPreferences"), ["Experience"] = "1", ["ReturnUrl"] = "/Learn"
    }));
    Assert(choiceResponse.StatusCode == HttpStatusCode.Redirect && choiceResponse.Headers.Location?.ToString() == "/Learn", "Förkunskapsvalet sparas och leder vidare till lärvägen");
    var firstPath = WebUtility.HtmlDecode(await client.GetStringAsync("/Learn"));
    Assert(firstPath.Contains("Fortsätt lära") && firstPath.Contains("Börja med: Dina första hälsningar")
        && firstPath.Contains("data-lesson-id=\"1\" data-lesson-state=\"available\"")
        && firstPath.Contains("data-lesson-id=\"2\" data-lesson-state=\"available\"") && !firstPath.Contains("<audio") && !firstPath.Contains("@if"),
        "Ett nytt konto får en rekommenderad start och öppna senare lektionsquiz utan ljud");
    var laterQuiz = await client.GetAsync("/Quiz/Index/2");
    Assert(laterQuiz.StatusCode == HttpStatusCode.OK && Regex.Matches(await laterQuiz.Content.ReadAsStringAsync(), "type=\"radio\"").Count == 16,
        "En elev kan öppna ett senare quiz utan tidigare klara lektioner");
    var laterCards = WebUtility.HtmlDecode(await client.GetStringAsync("/Lessons/Details/2"));
    Assert(laterCards.Contains("ane shimey ... behal") && laterCards.Contains("data-quiz-run") && laterCards.Contains("type=\"radio\""),
        "Lektionslänken erbjuder hela lär- och quizflödet oavsett ordning");
    Assert((await client.PostAsync("/Quiz/CheckAnswer/2", new FormUrlEncodedContent(new Dictionary<string,string> {
        ["__RequestVerificationToken"] = await Token("/"), ["QuestionId"] = "5", ["ChoiceId"] = "17", ["Kind"] = "Read"
    }))).IsSuccessStatusCode, "Direkträttning fungerar även i fritt vald senare lektion");
    using (var db = new ApplicationDbContext(options))
        Assert(!await db.UserProgresses.AnyAsync() && !await db.QuestionReviewStates.AnyAsync(), "Val av förkunskaper och öppna quiz skapar inga konstgjorda framsteg");

    var emptyDaily = WebUtility.HtmlDecode(await client.GetStringAsync("/DailyTraining"));
    Assert(emptyDaily.Contains("Börja med en lektion") && !emptyDaily.Contains("data-question-id="),
        "Nytt konto får vägledning utan träningsfrågor från okända lektioner");
    var initialCourse = WebUtility.HtmlDecode(await client.GetStringAsync("/Courses/Details/1"));
    Assert(initialCourse.Contains("0 av 19 lektioner klara") && initialCourse.Contains("Fortsätt med: Dina första hälsningar"),
        "Nytt konto börjar på lektion 1 med noll klara lektioner");
    Assert(!WebUtility.HtmlDecode(await client.GetStringAsync("/Courses/Details/" + emptyCourseId)).Contains("id=\"course-progress\""),
        "Tomma lektioner får ingen missvisande framstegsmätare");
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Progress")).Contains("data-total-xp=\"0\""), "Nytt konto börjar på noll XP");
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Review/1")).Contains("Inga fel att öva på"),
        "Ett nytt konto får en tydlig tom repetitionslista");
    Assert((await client.GetAsync("/Quiz/Review/2147483647")).StatusCode == HttpStatusCode.NotFound,
        "Repetition av saknad lektion ger 404");
    var quizHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Index/1"));
    Assert(!Regex.IsMatch(quizHtml, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"), "Quizet visar inga tigrinjatecken");
    Assert(Regex.Matches(quizHtml, "type=\"radio\"").Count == 16
        && Regex.Matches(quizHtml, "<audio").Count == 0
        && !quizHtml.Contains("IsCorrect") && !quizHtml.Contains("name=\"Score\"") && !quizHtml.Contains("name=\"UserId\""),
        "Lektionen innehåller lärsteg och blandad träning med sexton alternativ utan poäng eller konto-ID");
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Progress")).Contains("Inga sparade framsteg ännu"),
        "Framstegssidan är tom före första quizet");
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Index/" + emptyLessonId)).Contains("Quizet kommer snart"),
        "Tomma lektioner får inget tomt quiz att skicka in");
    Assert((await client.GetAsync("/Quiz/Index/2147483647")).StatusCode == HttpStatusCode.NotFound,
        "Ett quiz för en saknad lektion ger 404");

    Assert(quizHtml.Contains("data-quiz-run") && quizHtml.Contains("data-quiz-progress")
        && !quizHtml.Contains("data-kind=\"Listen\"") && quizHtml.Contains("data-kind=\"Read\"")
        && quizHtml.Contains("data-kind=\"Translate\"") && quizHtml.Contains("quiz-run.js"),
        "Quizet har stegvis flöde, framstegsrad och läsning/översättning utan lyssningsfrågor");
    var unifiedLesson = WebUtility.HtmlDecode(await client.GetStringAsync("/Lessons/Details/1"));
    Assert(unifiedLesson.Contains("data-quiz-run") && unifiedLesson.Contains("data-learn-step")
        && Regex.Matches(unifiedLesson, "data-learn-step").Count == 4
        && unifiedLesson.Contains("data-speaking-finish") && unifiedLesson.Contains("action=\"/Quiz/Submit/1\"")
        && !unifiedLesson.Contains("Starta quiz") && !unifiedLesson.Contains("Logga in och börja lektionen"),
        "Öppna lektionen ger lärsteg, frågor och muntlig avslutning i samma vy med rätt sparadress");
    Assert(Regex.Matches(quizHtml, "data-learn-step").Count == 4
        && new[] { "Hej", "selam", "Tack", "yekenyeley" }.All(quizHtml.Contains),
        "Direktlänken till quiz öppnar samma lärflöde med svenska betydelser och latinska uttryck");
    async Task<HttpResponseMessage> Check(int lessonId, int questionId, int choiceId, string kind = "Read", bool token = true)
    {
        var form = new Dictionary<string, string> { ["QuestionId"] = questionId.ToString(), ["ChoiceId"] = choiceId.ToString(), ["Kind"] = kind };
        if (token) form["__RequestVerificationToken"] = await Token("/");
        return await client.PostAsync("/Quiz/CheckAnswer/" + lessonId, new FormUrlEncodedContent(form));
    }
    Assert((await Check(1, 1, 1, token: false)).StatusCode == HttpStatusCode.BadRequest, "Direkträttning kräver antiforgery-token");
    var checkedWrong = await Check(1, 1, 2);
    using (var feedback = System.Text.Json.JsonDocument.Parse(await checkedWrong.Content.ReadAsStringAsync()))
        Assert(checkedWrong.IsSuccessStatusCode && !feedback.RootElement.GetProperty("isCorrect").GetBoolean()
            && feedback.RootElement.GetProperty("correctChoiceId").GetInt32() == 1
            && feedback.RootElement.GetProperty("correctAnswer").GetString() == "Hej", "Fel val ger rätt svar från servern direkt");
    var checkedRead = await Check(1, 2, 6, "Read");
    using (var feedback = System.Text.Json.JsonDocument.Parse(await checkedRead.Content.ReadAsStringAsync()))
        Assert(feedback.RootElement.GetProperty("isCorrect").GetBoolean(), "Läsfrågor kan rättas direkt");
    var checkedTranslation = await Check(1, 3, 11, "Translate");
    using (var feedback = System.Text.Json.JsonDocument.Parse(await checkedTranslation.Content.ReadAsStringAsync()))
        Assert(feedback.RootElement.GetProperty("isCorrect").GetBoolean()
            && feedback.RootElement.GetProperty("correctAnswer").GetString() == "yekenyeley", "Omvänd översättning ger återkoppling på tigrinja med latinska bokstäver");
    Assert((await Check(1, 1, 6)).StatusCode == HttpStatusCode.BadRequest
        && (await Check(1, 5, 17)).StatusCode == HttpStatusCode.BadRequest
        && (await Check(1, 1, 1, "99")).StatusCode == HttpStatusCode.BadRequest,
        "Direkträttning avvisar främmande alternativ, främmande frågor och okända träningsformer");
    Assert((await Check(2, 6, 22, "Translate")).StatusCode == HttpStatusCode.BadRequest,
        "Direkträttning tillåter inte omvända alternativ med två likvärdiga betydelser");
    using (var context = new ApplicationDbContext(options))
        Assert(!await context.UserProgresses.AnyAsync() && !await context.QuestionReviewStates.AnyAsync(),
            "Direkträttning sparar inga framsteg eller XP före avslutat quiz");
    List<(int QuestionId, int Correct, int Wrong)> quizKeys;
    string firstUserId;
    using (var context = new ApplicationDbContext(options))
    {
        var questions = await context.Questions.Include(question => question.Choices).Where(question => question.LessonId == 1)
            .OrderBy(question => question.Id).ToListAsync();
        quizKeys = questions.Select(question => (question.Id, question.Choices.Single(choice => choice.IsCorrect).Id,
            question.Choices.First(choice => !choice.IsCorrect).Id)).ToList();
        firstUserId = (await context.Users.SingleAsync(user => user.Email == email)).Id;
    }
    Dictionary<string, string> Answers(int correctCount)
    {
        var form = new Dictionary<string, string>();
        for (var index = 0; index < quizKeys.Count; index++)
        {
            form[$"Answers[{index}].QuestionId"] = quizKeys[index].QuestionId.ToString();
            form[$"Answers[{index}].ChoiceId"] = (index < correctCount ? quizKeys[index].Correct : quizKeys[index].Wrong).ToString();
        }
        return form;
    }
    async Task<HttpResponseMessage> SubmitQuiz(Dictionary<string, string> form, HttpClient? target = null)
    {
        form["__RequestVerificationToken"] = await Token("/Quiz/Index/1", target);
        return await (target ?? client).PostAsync("/Quiz/Submit/1", new FormUrlEncodedContent(form));
    }
    async Task<Dictionary<string, string>> ReviewForm(int correctCount, string userId)
    {
        using var context = new ApplicationDbContext(options);
        var ids = await context.QuestionReviewStates.Where(state => state.UserId == userId && state.NeedsReview
            && state.Question!.LessonId == 1).Select(state => state.QuestionId).ToListAsync();
        var questions = await context.Questions.Include(question => question.Choices).Where(question => ids.Contains(question.Id))
            .OrderBy(question => question.Id).ToListAsync();
        var form = new Dictionary<string, string>();
        for (var i = 0; i < questions.Count; i++)
        {
            form[$"Answers[{i}].QuestionId"] = questions[i].Id.ToString();
            form[$"Answers[{i}].ChoiceId"] = questions[i].Choices.First(choice => choice.IsCorrect == (i < correctCount)).Id.ToString();
        }
        return form;
    }
    async Task<HttpResponseMessage> SubmitReview(Dictionary<string, string> form, HttpClient? target = null)
    {
        form["__RequestVerificationToken"] = await Token("/Quiz/Review/1", target);
        return await (target ?? client).PostAsync("/Quiz/SubmitReview/1", new FormUrlEncodedContent(form));
    }
    var noToken = await client.PostAsync("/Quiz/Submit/1", new FormUrlEncodedContent(Answers(4)));
    Assert(noToken.StatusCode == HttpStatusCode.BadRequest, "POST utan antiforgery-token avvisas");
    var incomplete = Answers(4);
    incomplete.Remove("Answers[3].ChoiceId");
    var missingAnswer = await SubmitQuiz(incomplete);
    Assert(missingAnswer.StatusCode == HttpStatusCode.OK
        && WebUtility.HtmlDecode(await missingAnswer.Content.ReadAsStringAsync()).Contains("Svara på alla frågor"),
        "Ofullständiga svar ger hjälptext och sparar inget resultat");
    var invalidChoice = Answers(4);
    invalidChoice["Answers[0].ChoiceId"] = quizKeys[1].Correct.ToString();
    Assert((await SubmitQuiz(invalidChoice)).StatusCode == HttpStatusCode.BadRequest,
        "Ett svarsalternativ från en annan fråga avvisas");
    var duplicateQuestion = Answers(4);
    duplicateQuestion["Answers[1].QuestionId"] = quizKeys[0].QuestionId.ToString();
    Assert((await SubmitQuiz(duplicateQuestion)).StatusCode == HttpStatusCode.BadRequest,
        "Dubbla fråge-ID:n avvisas");
    var unknownQuestion = Answers(4);
    unknownQuestion["Answers[0].QuestionId"] = "2147483647";
    Assert((await SubmitQuiz(unknownQuestion)).StatusCode == HttpStatusCode.BadRequest,
        "Frågor utanför lektionen avvisas");
    using (var context = new ApplicationDbContext(options))
        Assert(await context.UserProgresses.CountAsync() == 0, "Inga felaktiga inskickningar sparade framsteg");

    var forgedScore = Answers(1);
    forgedScore["Score"] = "999";
    forgedScore["EarnedXp"] = "999999";
    forgedScore["TotalXp"] = "999999";
    forgedScore["UserId"] = "ett-annat-konto";
    var firstAttempt = await SubmitQuiz(forgedScore);
    Assert(firstAttempt.StatusCode == HttpStatusCode.Redirect
        && firstAttempt.Headers.Location!.ToString().Contains("/Quiz/Result/1"),
        "Giltiga svar leder till resultatsidan via redirect");
    var firstResult = WebUtility.HtmlDecode(await client.GetStringAsync(firstAttempt.Headers.Location));
    Assert(!Regex.IsMatch(firstResult, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"), "Quizresultatet visar tigrinja enbart med latinska bokstäver");
    Assert(firstResult.Contains("1 av 4 rätt") && firstResult.Contains("Rätt betydelse") && firstResult.Contains("Inte riktigt rätt"),
        "Resultatet innehåller serverns poäng och återkoppling per fråga");
    Assert(firstResult.Contains("data-earned-xp=\"10\"") && firstResult.Contains("data-total-xp=\"10\""),
        "Första nya rätta svaret ger 10 serverberäknade XP; förfalskad XP ignoreras");
    using (var context = new ApplicationDbContext(options))
    {
        var progress = await context.UserProgresses.SingleAsync();
        Assert(progress.UserId == firstUserId && progress.Score == 1 && progress.CompletedAt is null,
            "Poäng och konto-ID från formuläret ignoreras; serverns resultat sparas för inloggad användare");
    }

    using (var context = new ApplicationDbContext(options))
        Assert(await context.QuestionReviewStates.CountAsync(state => state.UserId == firstUserId) == 4
            && await context.QuestionReviewStates.CountAsync(state => state.UserId == firstUserId && state.NeedsReview) == 3,
            "Ett rätt och tre fel sparar tre personliga repetitionsfrågor");
    Assert(firstResult.Contains("Öva på mina fel") && (await client.GetStringAsync("/Progress")).Contains("/Quiz/Review/1"),
        "Resultatsidan och Mina framsteg länkar till sparade fel");
    var reviewRecommendation = WebUtility.HtmlDecode(await client.GetStringAsync("/Learn"));
    Assert(reviewRecommendation.Contains("Låt de svåra orden fastna") && reviewRecommendation.Contains("Repetera mina uttryck")
        && (await client.GetAsync("/Quiz/Index/2")).StatusCode == HttpStatusCode.OK,
        "Repetition rekommenderas efter fel samtidigt som andra färdiga quiz förblir öppna");
    var reviewPage = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Review/1"));
    Assert(Regex.Matches(reviewPage, "type=\"radio\"").Count == 12 && Regex.Matches(reviewPage, "<audio").Count == 0
        && !reviewPage.Contains("question-1-choice-") && !reviewPage.Contains("IsCorrect") && !reviewPage.Contains("data-learn-step") && !reviewPage.Contains("data-speaking-finish")
        && !Regex.IsMatch(reviewPage, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"),
        "Repetitionen visar bara de tre felbesvarade frågorna med latin utan ljud eller facit");
    var savedReviewForm = await ReviewForm(3, firstUserId);
    Assert((await client.PostAsync("/Quiz/SubmitReview/1", new FormUrlEncodedContent(savedReviewForm))).StatusCode == HttpStatusCode.BadRequest,
        "Repetition kräver antiforgery-token");
    var incompleteReview = new Dictionary<string, string>(savedReviewForm);
    incompleteReview.Remove("Answers[0].ChoiceId");
    var incompleteReviewResponse = await SubmitReview(incompleteReview);
    Assert(incompleteReviewResponse.StatusCode == HttpStatusCode.OK
        && WebUtility.HtmlDecode(await incompleteReviewResponse.Content.ReadAsStringAsync()).Contains("Svara på alla frågor"),
        "Repetition utan alla svar ger hjälptext utan sparande");
    var duplicateReview = new Dictionary<string, string>(savedReviewForm);
    duplicateReview["Answers[1].QuestionId"] = duplicateReview["Answers[0].QuestionId"];
    Assert((await SubmitReview(duplicateReview)).StatusCode == HttpStatusCode.BadRequest, "Repetition avvisar dubbla fråge-ID:n");
    var foreignReview = new Dictionary<string, string>(savedReviewForm);
    foreignReview["Answers[0].QuestionId"] = "5";
    Assert((await SubmitReview(foreignReview)).StatusCode == HttpStatusCode.BadRequest, "Repetition avvisar fråga från en annan lektion");
    var foreignReviewChoice = new Dictionary<string, string>(savedReviewForm);
    foreignReviewChoice["Answers[0].ChoiceId"] = "1";
    Assert((await SubmitReview(foreignReviewChoice)).StatusCode == HttpStatusCode.BadRequest, "Repetition avvisar alternativ från en annan fråga");
    using (var context = new ApplicationDbContext(options))
        Assert(await context.QuestionReviewStates.CountAsync(state => state.NeedsReview) == 3,
            "Ogiltiga repetitionssvar ändrar inte listan");
    var mixedReview = await SubmitReview(await ReviewForm(1, firstUserId));
    Assert(mixedReview.StatusCode == HttpStatusCode.Redirect && mixedReview.Headers.Location!.ToString().Contains("/Quiz/ReviewResult/1"),
        "Repetitionssvar använder redirect till eget resultat");
    var mixedReviewResult = WebUtility.HtmlDecode(await client.GetStringAsync(mixedReview.Headers.Location));
    Assert(mixedReviewResult.Contains("1 av 3 rätt") && mixedReviewResult.Contains("2 frågor kvar")
        && mixedReviewResult.Contains("Rätt betydelse"), "Rätt svar försvinner medan fel svar ligger kvar med återkoppling");
    var staleReview = await SubmitReview(new Dictionary<string, string>(savedReviewForm));
    Assert(staleReview.StatusCode == HttpStatusCode.Redirect && staleReview.Headers.Location!.ToString().Contains("/Quiz/Review/1"),
        "Ett gammalt repetitionsformulär får öppna den aktuella listan utan att skriva över den");
    var remainingReview = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Review/1"));
    Assert(Regex.Matches(remainingReview, "type=\"radio\"").Count == 8 && remainingReview.Contains("repetitionsord har ändrats"),
        "Bara två kvarvarande frågor visas efter ett inaktuellt formulär");
    var correctedReview = await SubmitReview(await ReviewForm(2, firstUserId));
    var correctedReviewResult = WebUtility.HtmlDecode(await client.GetStringAsync(correctedReview.Headers.Location));
    Assert(correctedReviewResult.Contains("2 av 2 rätt") && correctedReviewResult.Contains("Inga repetitionsord kvar"),
        "Alla rätt i repetition tömmer repetitionslistan");
    using (var context = new ApplicationDbContext(options))
    {
        var progress = await context.UserProgresses.SingleAsync();
        Assert(progress.Score == 1 && progress.CompletedAt is null && !await context.QuestionReviewStates.AnyAsync(state => state.NeedsReview),
            "Repetition höjer inte lektionens bästa poäng och ger inte klar-status för ett delquiz");
    }
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Progress")).Contains("data-total-xp=\"10\""), "Repetition ger inga extra XP för delquiz");
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Review/1")).Contains("Inga fel att öva på")
        && WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/ReviewResult/1")).Contains("Inga repetitionsord kvar"),
        "Tom lista och omladdat repetitionsresultat kan visas utan nya inskickningar");
    Assert(!firstResult.Contains("id=\"next-step-title\""), "Ett ofärdigt quiz rekommenderar fortsatt repetition");
    var concurrentToken = await Token("/Quiz/Index/1");
    var correctForm = Answers(4);
    correctForm["__RequestVerificationToken"] = concurrentToken;
    var wrongForm = Answers(0);
    wrongForm["__RequestVerificationToken"] = concurrentToken;
    var concurrentResults = await Task.WhenAll(
        client.PostAsync("/Quiz/Submit/1", new FormUrlEncodedContent(correctForm)),
        client.PostAsync("/Quiz/Submit/1", new FormUrlEncodedContent(wrongForm)));
    Assert(concurrentResults.All(response => response.StatusCode == HttpStatusCode.Redirect),
        "Samtidiga inskickningar kan rättas");
    DateTime completedAt;
    using (var context = new ApplicationDbContext(options))
    {
        var progress = await context.UserProgresses.SingleAsync();
        Assert(progress.Score == 4 && progress.CompletedAt.HasValue, "Bästa resultatet och klar-status bevaras vid samtidiga försök");
        completedAt = progress.CompletedAt!.Value;
    }
    await SubmitQuiz(Answers(0));
    var repeatedHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Result/1"));
    Assert(repeatedHtml.Contains("0 av 4 rätt") && repeatedHtml.Contains("4 av 4 rätt") && repeatedHtml.Contains("Lektionen är klar"),
        "Ett sämre försök visar aktuella poängen men bevarar bästa resultatet");
    using (var context = new ApplicationDbContext(options))
    {
        var progress = await context.UserProgresses.SingleAsync();
        Assert(progress.Score == 4 && progress.CompletedAt == completedAt, "Repetition skapar inga dubbletter och ändrar inte första klar-tiden");
    }
    Assert(repeatedHtml.Contains("data-earned-xp=\"0\"") && repeatedHtml.Contains("data-total-xp=\"60\""),
        "Fyra bästa rätt plus första klar-bonus ger totalt 60 XP; sämre försök ger noll extra");
    var refreshedResult = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Result/1"));
    Assert(refreshedResult.Contains("Ditt sparade resultat") && refreshedResult.Contains("4 av 4 rätt"),
        "Resultatsidans omladdning läser sparade framsteg utan en ny inskickning");
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Progress")).Contains("4 av 4 rätt")
        && WebUtility.HtmlDecode(await client.GetStringAsync("/Courses/Details/1")).Contains("Klar"),
        "Framsteg och kurslista visar den klara lektionen");

    var halfwayCourse = WebUtility.HtmlDecode(await client.GetStringAsync("/Courses/Details/1"));
    Assert(halfwayCourse.Contains("1 av 19 lektioner klara") && halfwayCourse.Contains("Fortsätt med: Presentera dig")
        && refreshedResult.Contains("Fortsätt med: Presentera dig") && refreshedResult.Contains("/Lessons/Details/2"),
        "Avklarad första lektion flyttar kursens och resultatsidans rekommendation till lektion 2");
    var secondEmail = "second-verification@example.invalid";
    string secondUserId;
    using (var context = new ApplicationDbContext(options))
    {
        var secondUser = new IdentityUser
        {
            Id = Guid.NewGuid().ToString(), UserName = secondEmail, NormalizedUserName = secondEmail.ToUpperInvariant(),
            Email = secondEmail, NormalizedEmail = secondEmail.ToUpperInvariant(), EmailConfirmed = true,
            SecurityStamp = Guid.NewGuid().ToString()
        };
        secondUser.PasswordHash = new PasswordHasher<IdentityUser>().HashPassword(secondUser, password);
        context.Users.Add(secondUser);
        await context.SaveChangesAsync();
        secondUserId = secondUser.Id;
    }
    using var secondClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, CookieContainer = new CookieContainer() })
    { BaseAddress = client.BaseAddress, Timeout = TimeSpan.FromSeconds(10) };
    Assert((await Login(password, secondEmail, secondClient)).StatusCode == HttpStatusCode.Redirect, "En separat testanvändare kan logga in");
    var legacyHome = await secondClient.GetStringAsync("/");
    Assert(legacyHome.Contains(">Mitt konto</button>") && !legacyHome.Contains(secondEmail), "Äldre konton får en neutral etikett direkt");
    var legacyName = await secondClient.PostAsync(profilePath, new FormUrlEncodedContent(new Dictionary<string,string> {
        ["__RequestVerificationToken"] = await Token(profilePath, secondClient), ["Input.DisplayName"] = "ElevTvå"
    }));
    Assert(legacyName.StatusCode == HttpStatusCode.Redirect && WebUtility.HtmlDecode(await secondClient.GetStringAsync("/")).Contains(">ElevTvå</button>"),
        "Äldre konton kan välja namn via sin profil");
    Assert((await secondClient.PostAsync("/LearningPreferences", new FormUrlEncodedContent(new Dictionary<string,string> {
        ["__RequestVerificationToken"] = await Token("/LearningPreferences", secondClient), ["Experience"] = "1"
    }))).StatusCode == HttpStatusCode.Redirect, "Varje konto väljer sina egna förkunskaper");
    Assert(WebUtility.HtmlDecode(await secondClient.GetStringAsync("/Progress")).Contains("Inga sparade framsteg ännu"),
        "En annan användare ser inte den första användarens framsteg");
    Assert((await secondClient.GetAsync("/Quiz/Result/1")).StatusCode == HttpStatusCode.Redirect,
        "Direktlänk till resultat visar inte en annan användares resultat");
    Assert(WebUtility.HtmlDecode(await secondClient.GetStringAsync("/Quiz/Review/1")).Contains("Inga fel att öva på"),
        "En annan användare ser inte den första användarens sparade repetitionsord");
    var secondForm = Answers(0);
    secondForm["UserId"] = firstUserId;
    var secondToken = await Token("/Quiz/Index/1", secondClient);
    secondForm["__RequestVerificationToken"] = secondToken;
    var secondBetterForm = Answers(2);
    secondBetterForm["__RequestVerificationToken"] = secondToken;
    var concurrentFirstAttempts = await Task.WhenAll(
        secondClient.PostAsync("/Quiz/Submit/1", new FormUrlEncodedContent(secondForm)),
        secondClient.PostAsync("/Quiz/Submit/1", new FormUrlEncodedContent(secondBetterForm)));
    Assert(concurrentFirstAttempts.All(response => response.StatusCode == HttpStatusCode.Redirect),
        "Samtidiga första försök hanteras utan fel vid skapandet av framstegsraden");
    using (var context = new ApplicationDbContext(options))
    {
        Assert(await context.UserProgresses.CountAsync() == 2
            && (await context.UserProgresses.SingleAsync(progress => progress.UserId == firstUserId)).Score == 4
            && (await context.UserProgresses.SingleAsync(progress => progress.UserId == secondUserId)).Score == 2,
            "Samtidiga första försök sparar en rad med bästa poängen och ändrat konto-ID kan inte påverka ett annat konto");
    }
    Assert(!WebUtility.HtmlDecode(await secondClient.GetStringAsync("/Courses/Details/1")).Contains("Klar ·"),
        "Kursens klar-status visas bara för det egna kontot");
    var secondCourse = WebUtility.HtmlDecode(await secondClient.GetStringAsync("/Courses/Details/1"));
    Assert(secondCourse.Contains("0 av 19 lektioner klara") && secondCourse.Contains("Fortsätt med: Dina första hälsningar"),
        "Nästa steg beräknas enbart från det egna kontots framsteg");
    var separatePath = WebUtility.HtmlDecode(await secondClient.GetStringAsync("/Learn"));
    Assert(separatePath.Contains("data-lesson-id=\"1\" data-lesson-state=\"available\"")
        && separatePath.Contains("data-lesson-id=\"2\" data-lesson-state=\"available\"")
        && (await secondClient.GetAsync("/Quiz/Index/2")).StatusCode == HttpStatusCode.OK
        && (await client.GetAsync("/Quiz/Index/2")).StatusCode == HttpStatusCode.OK,
        "Öppna lektionsquiz är tillgängliga för båda kontona oavsett individuella framsteg");
    using (var db = new ApplicationDbContext(options))
    {
        var legacy = new UserProgress { UserId = secondUserId, LessonId = 4, Score = 4, CompletedAt = DateTime.UtcNow };
        db.UserProgresses.Add(legacy); await db.SaveChangesAsync();
        var legacyCourse = (await new TigrinyaLingo.Services.LearningPathService(db).CoursesAsync(secondUserId, 1)).Single();
        Assert(!legacyCourse.Lessons.Single(lesson => lesson.Id == 4).IsLocked
            && !legacyCourse.Lessons.Single(lesson => lesson.Id == 5).IsLocked
            && legacyCourse.Lessons.Single(lesson => lesson.Id == 5).RequiredLessonId == null
            && (await secondClient.GetAsync("/Quiz/Index/4")).StatusCode == HttpStatusCode.OK,
            "Tidigare framsteg bevaras och alla färdiga senare lektionsquiz är öppna");
        db.UserProgresses.Remove(legacy); await db.SaveChangesAsync();
        var fixtures = new[] { "Första färdiga testlektionen", "Nästa testlektion" }.Select(title => new Lesson {
            CourseId = emptyCourseId, Title = title, Order = 20,
            Questions = new List<Question> { new() { Text = "", LatinText = title,
                Choices = new List<Choice> { new() {Text="Rätt",IsCorrect=true},new() {Text="Fel ett"},new() {Text="Fel två"},new() {Text="Fel tre"} } } }
        }).ToList();
        db.Lessons.AddRange(fixtures); await db.SaveChangesAsync();
        var readyCourse = (await new TigrinyaLingo.Services.LearningPathService(db).CoursesAsync(secondUserId, emptyCourseId)).Single();
        Assert(readyCourse.NextLesson?.Id == fixtures[0].Id
            && !readyCourse.Lessons.Single(lesson => lesson.Id == fixtures[1].Id).IsLocked
            && (await secondClient.GetAsync($"/Quiz/Index/{fixtures[0].Id}")).StatusCode == HttpStatusCode.OK,
            "Tomma lektioner blockerar inte, lika ordning bryts med ID och varje kurs börjar självständigt");
        db.Lessons.RemoveRange(fixtures); await db.SaveChangesAsync();
    }

    var introductionQuiz = await client.GetStringAsync("/Quiz/Index/2");
    Assert(Regex.Matches(introductionQuiz, "type=\"radio\"").Count == 16 && Regex.Matches(introductionQuiz, "<audio").Count == 0,
        "Presentationsquizet blandar lyssning och läsning utan tvetydig omvänd översättning");
    var introductionAnswers = new Dictionary<string, string>();
    using (var context = new ApplicationDbContext(options))
    {
        var questions = await context.Questions.Include(question => question.Choices).Where(question => question.LessonId == 2)
            .OrderBy(question => question.Id).ToListAsync();
        Assert(questions.Count == 4 && questions.All(question => question.Choices.Count == 4 && question.Choices.Count(choice => choice.IsCorrect) == 1),
            "Presentationerna har exakt ett facit per fråga");
        for (var i = 0; i < questions.Count; i++)
        {
            introductionAnswers[$"Answers[{i}].QuestionId"] = questions[i].Id.ToString();
            introductionAnswers[$"Answers[{i}].ChoiceId"] = questions[i].Choices.Single(choice => choice.IsCorrect).Id.ToString();
        }
    }
    introductionAnswers["__RequestVerificationToken"] = await Token("/Quiz/Index/2");
    var introductionResult = await client.PostAsync("/Quiz/Submit/2", new FormUrlEncodedContent(introductionAnswers));
    Assert(introductionResult.StatusCode == HttpStatusCode.Redirect
        && WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Result/2")).Contains("4 av 4 rätt"),
        "Presentationsquizet rättas och visar fyra rätt");
    using (var context = new ApplicationDbContext(options))
        Assert((await context.UserProgresses.SingleAsync(progress => progress.UserId == firstUserId && progress.LessonId == 2)).CompletedAt.HasValue
            && (await context.UserProgresses.SingleAsync(progress => progress.UserId == firstUserId && progress.LessonId == 1)).Score == 4
            && !await context.UserProgresses.AnyAsync(progress => progress.UserId == secondUserId && progress.LessonId == 2),
            "Lektion 2 sparas separat utan att ändra lektion 1 eller andra användares framsteg");
    var almostCompletedCourse = WebUtility.HtmlDecode(await client.GetStringAsync("/Courses/Details/1"));
    var introductionNext = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Result/2"));
    Assert(almostCompletedCourse.Contains("2 av 19 lektioner klara")
        && almostCompletedCourse.Contains("Fortsätt med: Enkla vardagsfraser")
        && introductionNext.Contains("Fortsätt med: Enkla vardagsfraser") && introductionNext.Contains("/Lessons/Details/3"),
        "Lektionsvägen och resultatet leder vidare från presentationer till vardagsfraser");
    List<Question> everydayQuestions;
    using (var context = new ApplicationDbContext(options))
    {
        everydayQuestions = await context.Questions.Include(question => question.Choices).Where(question => question.LessonId == 3)
            .OrderBy(question => question.Id).ToListAsync();
        Assert(everydayQuestions.Count == 4 && everydayQuestions.All(question => question.Choices.Count == 4
            && question.Choices.Select(choice => choice.Text).Distinct().Count() == 4
            && question.Choices.Count(choice => choice.IsCorrect) == 1)
            && everydayQuestions.Select(question => question.Choices.Single(choice => choice.IsCorrect).Text)
                .SequenceEqual(new[] { "Jag förstår", "Jag förstår inte", "Jag vet inte", "Förlåt" }),
            "Vardagsfraserna har fyra olika alternativ per fråga och rätt naturlig betydelse som facit");
    }
    var everydayQuiz = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Index/3"));
    Assert(Regex.Matches(everydayQuiz, "type=\"radio\"").Count == 16 && Regex.Matches(everydayQuiz, "<audio").Count == 0
        && !everydayQuiz.Contains("IsCorrect") && !Regex.IsMatch(everydayQuiz, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"),
        "Vardagslektionen visar lärsteg och träning med latin utan ljud");
    Dictionary<string, string> EverydayAnswers(int correctCount)
    {
        var form = new Dictionary<string, string>();
        for (var i = 0; i < everydayQuestions.Count; i++)
        {
            form[$"Answers[{i}].QuestionId"] = everydayQuestions[i].Id.ToString();
            form[$"Answers[{i}].ChoiceId"] = everydayQuestions[i].Choices.First(choice => choice.IsCorrect == (i < correctCount)).Id.ToString();
        }
        return form;
    }
    async Task<HttpResponseMessage> SubmitEveryday(Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = await Token("/Quiz/Index/3");
        return await client.PostAsync("/Quiz/Submit/3", new FormUrlEncodedContent(form));
    }
    var everydayAttempt = await SubmitEveryday(EverydayAnswers(1));
    Assert(everydayAttempt.StatusCode == HttpStatusCode.Redirect
        && WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Result/3")).Contains("1 av 4 rätt"),
        "Vardagsquizet rättas och sparar serverns resultat");
    var everydayReview = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Review/3"));
    Assert(Regex.Matches(everydayReview, "type=\"radio\"").Count == 12
        && !everydayReview.Contains("question-9-choice-"), "Lektion 3 kan repetera enbart de tre felbesvarade fraserna");
    Assert(WebUtility.HtmlDecode(await secondClient.GetStringAsync("/Quiz/Review/3")).Contains("Inga fel att öva på"),
        "Den nya lektionens fel visas bara för det egna kontot");
    var everydayReviewAnswers = new Dictionary<string, string>();
    for (var i = 0; i < 3; i++)
    {
        var question = everydayQuestions[i + 1];
        everydayReviewAnswers[$"Answers[{i}].QuestionId"] = question.Id.ToString();
        everydayReviewAnswers[$"Answers[{i}].ChoiceId"] = question.Choices.Single(choice => choice.IsCorrect).Id.ToString();
    }
    everydayReviewAnswers["__RequestVerificationToken"] = await Token("/Quiz/Review/3");
    var everydayReviewResponse = await client.PostAsync("/Quiz/SubmitReview/3", new FormUrlEncodedContent(everydayReviewAnswers));
    Assert(everydayReviewResponse.StatusCode == HttpStatusCode.Redirect
        && WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/ReviewResult/3")).Contains("Inga repetitionsord kvar"),
        "Rättade vardagsfraser försvinner från repetitionslistan");
    using (var context = new ApplicationDbContext(options))
    {
        var progress = await context.UserProgresses.SingleAsync(progress => progress.UserId == firstUserId && progress.LessonId == 3);
        Assert(progress.Score == 1 && progress.CompletedAt is null
            && !await context.QuestionReviewStates.AnyAsync(state => state.UserId == firstUserId && state.Question!.LessonId == 3 && state.NeedsReview),
            "Vardagsrepetition påverkar inte hela lektionens bästa resultat eller klar-status");
    }
    Assert((await SubmitEveryday(EverydayAnswers(4))).StatusCode == HttpStatusCode.Redirect,
        "Alla rätt i hela vardagsquizet kan sparas");
    var unifiedResult = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Result/3"));
    Assert(unifiedResult.Contains("Gör lektionen igen") && !unifiedResult.Contains("Repetera ordkorten")
        && !unifiedResult.Contains("Gör quizet igen"),
        "Resultatsidan erbjuder en gemensam lektionsstart i stället för separata lär- och quizval");
    var completedCourse = WebUtility.HtmlDecode(await client.GetStringAsync("/Courses/Details/1"));
    var completedResult = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Result/3"));
    Assert(completedCourse.Contains("3 av 19 lektioner klara")
        && completedCourse.Contains("Fortsätt med: Småprata och lära känna")
        && completedResult.Contains("Fortsätt med: Småprata och lära känna")
        && !completedResult.Contains("Alla tillgängliga lektioner i kursen är klara"),
        "Tidigare slutförd grundkurs fortsätter med första nya lektionen");
    using (var context = new ApplicationDbContext(options))
        Assert((await context.UserProgresses.SingleAsync(progress => progress.UserId == firstUserId && progress.LessonId == 3)).CompletedAt.HasValue
            && (await context.UserProgresses.SingleAsync(progress => progress.UserId == firstUserId && progress.LessonId == 1)).Score == 4
            && !await context.UserProgresses.AnyAsync(progress => progress.UserId == secondUserId && progress.LessonId == 3),
            "Lektion 3 har egna framsteg och ändrar inte tidigare lektioner eller andra konton");
    Assert(completedResult.Contains("data-total-xp=\"180\"") && completedResult.Contains("Nivå 2")
        && WebUtility.HtmlDecode(await client.GetStringAsync("/Progress")).Contains("data-total-xp=\"180\""),
        "XP summeras mellan lektioner och visas på nivå 2 även under Mina framsteg");
    // Ett senare vanligt quiz kan lägga tillbaka ord, även om bästa resultatet redan är alla rätt.
    using (var context = new ApplicationDbContext(options))
        Assert(await context.QuestionReviewStates.CountAsync(state => state.UserId == firstUserId && state.NeedsReview) == 4
            && await context.QuestionReviewStates.CountAsync(state => state.UserId == firstUserId && state.Question!.LessonId == 2 && state.NeedsReview) == 0,
            "Senaste svar styr repetitionen per lektion medan bästa quizresultatet bevaras");
    int firstReviewCount;
    using (var context = new ApplicationDbContext(options))
        firstReviewCount = await context.QuestionReviewStates.CountAsync(state => state.UserId == firstUserId && state.NeedsReview);
    var secondReviewForm = await ReviewForm(4, secondUserId);
    secondReviewForm["UserId"] = firstUserId;
    secondReviewForm["Score"] = "999";
    Assert((await SubmitReview(secondReviewForm, secondClient)).StatusCode == HttpStatusCode.Redirect,
        "Den andra användaren kan rätta sina egna repetitionsord");
    using (var context = new ApplicationDbContext(options))
        Assert(!await context.QuestionReviewStates.AnyAsync(state => state.UserId == secondUserId && state.NeedsReview)
            && await context.QuestionReviewStates.CountAsync(state => state.UserId == firstUserId && state.NeedsReview) == firstReviewCount
            && (await context.UserProgresses.SingleAsync(progress => progress.UserId == secondUserId && progress.LessonId == 1)).Score == 2,
            "Förfalskat konto-ID och poäng i repetition påverkar varken andra konton eller bästa resultatet");
    // Utökad kurs: alla nya lektioner fungerar för gäster och i det ordinarie quizflödet.
    List<Lesson> expandedLessons;
    using (var context = new ApplicationDbContext(options))
        expandedLessons = await context.Lessons.Include(lesson => lesson.Questions).ThenInclude(question => question.Choices)
            .Where(lesson => lesson.CourseId == 1 && lesson.Id >= 4).OrderBy(lesson => lesson.Order).ToListAsync();
    Assert(expandedLessons.Count == 16 && expandedLessons.Sum(lesson => lesson.Questions.Count) == 65,
        "Sexton utökade lektioner innehåller 65 ord och fraser");
    Assert(expandedLessons.SelectMany(lesson => lesson.Questions).All(question => question.Choices.Count == 4
        && question.Choices.Select(choice => choice.Text).Distinct().Count() == 4
        && question.Choices.Count(choice => choice.IsCorrect) == 1),
        "Alla nya frågor har fyra olika alternativ och exakt ett rätt svar");
    Assert(expandedLessons.SelectMany(lesson => lesson.Questions).Count(question => question.AudioFileName is not null) == 0,
        "Alla utökade uttryck tränas nu utan Omniglot-ljud");
    foreach (var lesson in expandedLessons)
    {
        var lessonPage = WebUtility.HtmlDecode(await client.GetStringAsync($"/Lessons/Details/{lesson.Id}"));
        Assert(lessonPage.Contains(lesson.Title) && lessonPage.Contains("Din tur att prata")
            && lessonPage.Contains("Tips för den här lektionen")
            && lesson.Questions.All(question => lessonPage.Contains(question.LatinText)
                && lessonPage.Contains(question.Choices.Single(choice => choice.IsCorrect).Text))
            && !Regex.IsMatch(lessonPage, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"),
            $"Lektion {lesson.Id} visar alla ord, mål och muntlig övning utan originalskrift");
        using var quizResponse = await client.GetAsync($"/Quiz/Index/{lesson.Id}");
        var newQuiz = WebUtility.HtmlDecode(await quizResponse.Content.ReadAsStringAsync());
        Assert(quizResponse.StatusCode == HttpStatusCode.OK && Regex.Matches(newQuiz, "type=\"radio\"").Count == lesson.Questions.Count * 4,
            $"Lektion {lesson.Id} är öppen med fyra svarsalternativ utan krav på tidigare lektioner");
    }
    var numberLesson = expandedLessons.Single(lesson => lesson.Id == 9);
    Dictionary<string, string> ExpandedAnswers(Lesson lesson, bool oneWrong = false)
    {
        var form = new Dictionary<string, string>();
        var ordered = lesson.Questions.OrderBy(question => question.Id).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            form[$"Answers[{i}].QuestionId"] = ordered[i].Id.ToString();
            form[$"Answers[{i}].ChoiceId"] = ordered[i].Choices.First(choice => choice.IsCorrect != (oneWrong && i == 0)).Id.ToString();
            form[$"Answers[{i}].Kind"] = "Read";
        }
        return form;
    }
    async Task<HttpResponseMessage> SubmitExpanded(Lesson lesson, bool oneWrong = false)
    {
        var form = ExpandedAnswers(lesson, oneWrong);
        form["__RequestVerificationToken"] = await Token($"/Quiz/Index/{lesson.Id}");
        return await client.PostAsync($"/Quiz/Submit/{lesson.Id}", new FormUrlEncodedContent(form));
    }
    foreach (var basics in expandedLessons.Where(lesson => lesson.Id < 9))
        Assert((await SubmitExpanded(basics)).StatusCode == HttpStatusCode.Redirect, $"Lektion {basics.Id} klaras i ordning innan grundtalen");
    Assert((await SubmitExpanded(numberLesson, true)).StatusCode == HttpStatusCode.Redirect,
        "Ett quiz med fem frågor kan rättas och sparas med ett fel");
    var numberReview = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Review/9"));
    Assert(Regex.Matches(numberReview, "type=\"radio\"").Count == 4 && !numberReview.Contains("<audio"),
        "Ett felaktigt grundtal hamnar ensamt i repetition utan ljud");
    var numberQuestion = numberLesson.Questions.OrderBy(question => question.Id).First();
    var numberForm = new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = await Token("/Quiz/Review/9"),
        ["Answers[0].QuestionId"] = numberQuestion.Id.ToString(),
        ["Answers[0].ChoiceId"] = numberQuestion.Choices.Single(choice => choice.IsCorrect).Id.ToString(),
        ["Answers[0].Kind"] = "Read"
    };
    Assert((await client.PostAsync("/Quiz/SubmitReview/9", new FormUrlEncodedContent(numberForm))).StatusCode == HttpStatusCode.Redirect,
        "Grundtal kan rättas i repetition");
    using (var context = new ApplicationDbContext(options))
        Assert(!await context.QuestionReviewStates.AnyAsync(state => state.UserId == firstUserId && state.QuestionId == numberQuestion.Id && state.NeedsReview)
            && (await context.UserProgresses.SingleAsync(progress => progress.UserId == firstUserId && progress.LessonId == 9)).Score == 4
            && !(await context.UserProgresses.SingleAsync(progress => progress.UserId == firstUserId && progress.LessonId == 9)).CompletedAt.HasValue,
            "Repetition av grundtal bevarar bästa poäng och ger ingen klar-status");
    foreach (var lesson in expandedLessons)
    {
        Assert((await SubmitExpanded(lesson)).StatusCode == HttpStatusCode.Redirect, $"Alla rätt sparas för lektion {lesson.Id}");
        var result = WebUtility.HtmlDecode(await client.GetStringAsync($"/Quiz/Result/{lesson.Id}"));
        Assert(result.Contains("Lektionen är klar!") && result.Contains("data-earned-xp="), $"Lektion {lesson.Id} visar resultat och XP");
    }
    var expandedCourse = WebUtility.HtmlDecode(await client.GetStringAsync("/Courses/Details/1"));
    var finishedPath = WebUtility.HtmlDecode(await client.GetStringAsync("/Learn"));
    Assert(finishedPath.Contains("Repetera mina uttryck") && finishedPath.Contains("19 av 19 klara")
        && !finishedPath.Contains("data-lesson-state=\"locked\""), "En klar kurs behåller repetition som rekommendation när svåra ord finns kvar");
    using (var db = new ApplicationDbContext(options))
    {
        var remaining = await db.QuestionReviewStates.Where(state => state.UserId == firstUserId && state.NeedsReview).ToListAsync();
        foreach (var state in remaining) state.NeedsReview = false;
        await db.SaveChangesAsync();
        var cleanFinishedPath = WebUtility.HtmlDecode(await client.GetStringAsync("/Learn"));
        Assert(cleanFinishedPath.Contains("Du har klarat alla lektioner!") && cleanFinishedPath.Contains("Spela med mina ord")
            && !cleanFinishedPath.Contains("data-lesson-state=\"locked\""), "En helt klar lärväg utan svåra ord rekommenderar spel");
        foreach (var state in remaining) state.NeedsReview = true;
        await db.SaveChangesAsync();
    }
    var expandedResult = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Result/13"));
    Assert(expandedCourse.Contains("19 av 19 lektioner klara") && expandedCourse.Contains("Alla tillgängliga lektioner är klara")
        && expandedResult.Contains("data-total-xp=\"1150\"") && expandedResult.Contains("Nivå 12"),
        "Hela utökade kursen ger 1150 XP, nivå 12 och korrekt kursavslutning");
    Assert((await SubmitExpanded(numberLesson)).StatusCode == HttpStatusCode.Redirect
        && WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Result/9")).Contains("data-earned-xp=\"0\""),
        "Omtagning av femfrågorsquiz ger inga dubbla XP");
    using (var context = new ApplicationDbContext(options))
        Assert(await context.UserProgresses.CountAsync(progress => progress.UserId == firstUserId) == 19
            && !await context.UserProgresses.AnyAsync(progress => progress.UserId == secondUserId && progress.LessonId >= 4),
            "Alla nya framsteg tillhör rätt konto och andra konton påverkas inte");
    // Blandad repetition använder skyddad frågelista och bevarar bästa lektionsresultat.
    string DailySessionToken(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"SessionToken\" value=\"([^\"]+)\"").Groups[1].Value);
    List<(int Id, string Kind)> DailyQuestions(string html) => Regex.Matches(html, "data-question-id=\"(\\d+)\" data-kind=\"(\\w+)\"")
        .Select(match => (int.Parse(match.Groups[1].Value), match.Groups[2].Value)).ToList();
    var dailyHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/DailyTraining"));
    var dailyKeys = DailyQuestions(dailyHtml);
    var dailyToken = DailySessionToken(dailyHtml);
    Assert(dailyKeys.Count == 10 && dailyKeys.Select(key => key.Id).Distinct().Count() == 10
        && !dailyHtml.Contains("data-learn-step") && !dailyHtml.Contains("data-speaking-finish")
        && dailyHtml.Contains("action=\"/DailyTraining/Submit\"") && dailyHtml.Contains("/DailyTraining/CheckAnswer")
        && !Regex.IsMatch(dailyHtml, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"),
        "Dagens träning visar tio unika frågor i samma frågeflöde med rätt adresser och enbart latinsk tigrinja");
    var sameDaily = DailyQuestions(await client.GetStringAsync("/DailyTraining"));
    Assert(dailyKeys.SequenceEqual(sameDaily), "Samma dags urval är stabilt innan användaren svarar");
    List<Question> dailyQuestionData;
    string beforeDailyProgress;
    using (var context = new ApplicationDbContext(options))
    {
        dailyQuestionData = await context.Questions.Include(question => question.Choices).Where(question => dailyKeys.Select(key => key.Id).Contains(question.Id)).ToListAsync();
        var mistakes = await context.QuestionReviewStates.Where(state => state.UserId == firstUserId && state.NeedsReview).Select(state => state.QuestionId).ToListAsync();
        Assert(mistakes.Count > 0 && dailyKeys.Take(mistakes.Count).Select(key => key.Id).ToHashSet().SetEquals(mistakes)
            && dailyQuestionData.Select(question => question.LessonId).Distinct().Count() > 1,
            "Tidigare fel kommer först och träningen blandar flera kända lektioner");
        beforeDailyProgress = System.Text.Json.JsonSerializer.Serialize(await context.UserProgresses.OrderBy(progress => progress.Id).AsNoTracking().ToListAsync());
    }
    var secondDaily = await secondClient.GetStringAsync("/DailyTraining");
    Assert(DailyQuestions(secondDaily).Count == 4 && DailyQuestions(secondDaily).All(key => key.Id <= 4),
        "Ett konto med en tidigare lektion får fyra kända uttryck och inga andra kontons lektioner");
    var dailyForm = new Dictionary<string, string> { ["SessionToken"] = dailyToken, ["UserId"] = secondUserId, ["Score"] = "999" };
    for (var i = 0; i < dailyKeys.Count; i++)
    {
        var key = dailyKeys[i];
        var question = dailyQuestionData.Single(question => question.Id == key.Id);
        dailyForm[$"Answers[{i}].QuestionId"] = key.Id.ToString();
        dailyForm[$"Answers[{i}].Kind"] = key.Kind;
        dailyForm[$"Answers[{i}].ChoiceId"] = question.Choices.First(choice => choice.IsCorrect == (i < 5)).Id.ToString();
    }
    async Task<HttpResponseMessage> SubmitDaily(Dictionary<string, string> form, HttpClient? target = null)
    {
        form["__RequestVerificationToken"] = await Token("/DailyTraining", target);
        return await (target ?? client).PostAsync("/DailyTraining/Submit", new FormUrlEncodedContent(form));
    }
    Assert((await client.PostAsync("/DailyTraining/Submit", new FormUrlEncodedContent(dailyForm))).StatusCode == HttpStatusCode.BadRequest,
        "Blandad träning kräver antiforgery-token");
    var brokenDaily = new Dictionary<string, string>(dailyForm) { ["SessionToken"] = "tampered" };
    Assert((await SubmitDaily(brokenDaily)).StatusCode == HttpStatusCode.BadRequest,
        "Ändrad skyddad frågelista avvisas");
    Assert((await SubmitDaily(new Dictionary<string, string>(dailyForm), secondClient)).StatusCode == HttpStatusCode.BadRequest,
        "Ett annat konto kan inte använda träningsformuläret");
    var duplicateDaily = new Dictionary<string, string>(dailyForm) { ["Answers[1].QuestionId"] = dailyKeys[0].Id.ToString() };
    var missingDaily = new Dictionary<string, string>(dailyForm); missingDaily.Remove("Answers[0].ChoiceId");
    var foreignDaily = new Dictionary<string, string>(dailyForm) { ["Answers[0].ChoiceId"] = "2147483647" };
    Assert((await SubmitDaily(duplicateDaily)).StatusCode == HttpStatusCode.BadRequest
        && (await SubmitDaily(missingDaily)).StatusCode == HttpStatusCode.BadRequest
        && (await SubmitDaily(foreignDaily)).StatusCode == HttpStatusCode.BadRequest,
        "Dubbletter, saknade svar och främmande alternativ avvisas i blandad träning");
    var checkedDaily = await client.PostAsync("/DailyTraining/CheckAnswer", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = await Token("/DailyTraining"), ["SessionToken"] = dailyToken,
        ["QuestionId"] = dailyKeys[0].Id.ToString(), ["Kind"] = dailyKeys[0].Kind,
        ["ChoiceId"] = dailyForm["Answers[0].ChoiceId"]
    }));
    Assert(checkedDaily.IsSuccessStatusCode && (await checkedDaily.Content.ReadAsStringAsync()).Contains("\"isCorrect\":true"),
        "Blandad träning kan rättas direkt utan att spara före sista frågan");
    Assert((await SubmitDaily(dailyForm)).StatusCode == HttpStatusCode.Redirect,
        "Tio blandade svar sparas atomiskt över flera lektioner");
    var dailyResult = WebUtility.HtmlDecode(await client.GetStringAsync("/DailyTraining/Result"));
    Assert(dailyResult.Contains("5 av 10 rätt") && dailyResult.Contains("Det här behöver du öva mer på")
        && dailyResult.Contains("Träningen är sparad") && dailyResult.Contains("Öva på mina fel"),
        "Sammanfattningen visar rätt antal rätt och vilka uttryck som behöver övas");
    using (var context = new ApplicationDbContext(options))
    {
        Assert(beforeDailyProgress == System.Text.Json.JsonSerializer.Serialize(await context.UserProgresses.OrderBy(progress => progress.Id).AsNoTracking().ToListAsync()),
            "Blandad repetition ändrar inga bästa lektionsresultat, klar-tider eller XP för något konto");
        var dailyStates = await context.QuestionReviewStates.Where(state => state.UserId == firstUserId && dailyKeys.Select(key => key.Id).Contains(state.QuestionId)).ToDictionaryAsync(state => state.QuestionId);
        Assert(dailyKeys.Select((key, i) => dailyStates[key.Id].NeedsReview == (i >= 5)).All(value => value),
            "Varje blandat svar uppdaterar sin egen repetitionsstatus");
    }
    Assert((await SubmitDaily(dailyForm)).Headers.Location?.ToString() == "/DailyTraining",
        "Dubbel inskickning av en sparad omgång avvisas utan att skriva över nyare svar");
    var refreshedDaily = WebUtility.HtmlDecode(await client.GetStringAsync("/DailyTraining/Result"));
    Assert(!refreshedDaily.Contains("5 av 10 rätt") && refreshedDaily.Contains("Omgångens detaljer visas en gång"),
        "Omladdning visar repetitionsöversikt och sparar ingenting igen");
    var editorList = WebUtility.HtmlDecode(await client.GetStringAsync("/ContentEditor"));
    Assert(editorList.Contains("Redigera ord och fraser") && editorList.Contains("/ContentEditor/Edit/5")
        && editorList.Contains("Exportera fraser som JSON") && !Regex.IsMatch(editorList, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"),
        "Lokal innehållslista visar fraser och redigering utan originalskrift");
    var filteredEditor = await client.GetStringAsync("/ContentEditor?search=" + Uri.EscapeDataString("Presentera dig"));
    Assert(filteredEditor.Contains("/ContentEditor/Edit/5") && !filteredEditor.Contains("/ContentEditor/Edit/1\""),
        "Redigerarens sökning filtrerar efter lektion");
    // Enbart syntetiska filmarkörer för redigerarens framtida ljudstöd, inte tal eller Omniglot-material.
    var fixtureAudioA = "verification-only-a-" + Guid.NewGuid().ToString("N") + ".mp3";
    var fixtureAudioB = "verification-only-b-" + Guid.NewGuid().ToString("N") + ".mp3";
    foreach (var name in new[] { fixtureAudioA, fixtureAudioB })
    {
        var path = Path.Combine(webRoot, "wwwroot/audio/tigrinya", name);
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            await file.WriteAsync(new byte[64]);
        audioFixturePaths.Add(path);
    }
    using (var context = new ApplicationDbContext(options))
    {
        (await context.Questions.SingleAsync(question => question.Id == 5)).AudioFileName = fixtureAudioA;
        await context.SaveChangesAsync();
    }
    var phraseEditor = WebUtility.HtmlDecode(await client.GetStringAsync("/ContentEditor/Edit/5"));
    string EditStamp(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"EditStamp\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    Assert(phraseEditor.Contains("Spara ändringar") && Regex.Matches(phraseEditor, "name=\"CorrectChoiceId\"").Count == 4
        && EditStamp(phraseEditor).Length > 0 && phraseEditor.Contains(fixtureAudioA),
        "Frasformuläret visar fyra svar, rättningsval, ljud och skyddad versionskontroll");
    Question originalPhrase;
    string editorProgress;
    string editorReviews;
    using (var context = new ApplicationDbContext(options))
    {
        originalPhrase = await context.Questions.AsNoTracking().Include(question => question.Choices).SingleAsync(question => question.Id == 5);
        editorProgress = System.Text.Json.JsonSerializer.Serialize(await context.UserProgresses.AsNoTracking().OrderBy(progress => progress.Id).ToListAsync());
        editorReviews = System.Text.Json.JsonSerializer.Serialize(await context.QuestionReviewStates.AsNoTracking().OrderBy(state => state.Id).ToListAsync());
    }
    var editForm = new Dictionary<string, string>
    {
        ["Audience"] = "4", ["EditStamp"] = EditStamp(phraseEditor), ["LatinText"] = "ane shimey … behal",
        ["AudioFileName"] = originalPhrase.AudioFileName ?? "", ["CorrectChoiceId"] = originalPhrase.Choices.Single(choice => choice.IsCorrect).Id.ToString(),
        ["UserId"] = secondUserId, ["LessonId"] = "999", ["Text"] = "ska inte skrivas till källskriften"
    };
    var originalChoices = originalPhrase.Choices.OrderBy(choice => choice.Id).ToList();
    for (var i = 0; i < originalChoices.Count; i++)
    {
        editForm[$"Choices[{i}].Id"] = originalChoices[i].Id.ToString();
        editForm[$"Choices[{i}].Text"] = originalChoices[i].IsCorrect ? "Jag heter …" : originalChoices[i].Text;
    }
    async Task<HttpResponseMessage> SavePhrase(Dictionary<string, string> form, HttpClient? target = null)
    {
        form["__RequestVerificationToken"] = await Token("/ContentEditor/Edit/5", target);
        return await (target ?? client).PostAsync("/ContentEditor/Edit/5", new FormUrlEncodedContent(form));
    }
    Assert((await client.PostAsync("/ContentEditor/Edit/5", new FormUrlEncodedContent(editForm))).StatusCode == HttpStatusCode.BadRequest,
        "Redigering kräver antiforgery-token");
    Assert((await SavePhrase(new Dictionary<string, string>(editForm) { ["EditStamp"] = "tampered" })).StatusCode == HttpStatusCode.BadRequest
        && (await SavePhrase(new Dictionary<string, string>(editForm), secondClient)).StatusCode == HttpStatusCode.BadRequest,
        "Versionsbevis kan inte förfalskas eller användas av ett annat konto");
    var foreignEditChoice = new Dictionary<string, string>(editForm) { ["Choices[0].Id"] = "1" };
    Assert((await SavePhrase(foreignEditChoice)).StatusCode == HttpStatusCode.BadRequest,
        "Främmande svarsalternativ kan inte flyttas in i en annan fras");
    foreach (var invalidEdit in new[]
    {
        new Dictionary<string, string>(editForm) { ["Audience"] = "999" },
        new Dictionary<string, string>(editForm) { ["Audience"] = "ogiltigt" },
        new Dictionary<string, string>(editForm) { ["LatinText"] = " " },
        new Dictionary<string, string>(editForm) { ["LatinText"] = new string('a', 201) },
        new Dictionary<string, string>(editForm) { ["LatinText"] = "ሰላም" },
        new Dictionary<string, string>(editForm) { ["CorrectChoiceId"] = "2147483647" },
        new Dictionary<string, string>(editForm) { ["Choices[1].Text"] = editForm["Choices[0].Text"] },
        new Dictionary<string, string>(editForm) { ["AudioFileName"] = "../../appsettings.json" }
    })
    {
        var response = await SavePhrase(invalidEdit);
        Assert(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsStringAsync()).Contains("validation-summary-errors"),
            "Ogiltig text, rättning eller ljudfil ger formulärfel i stället för sparande");
    }
    using (var context = new ApplicationDbContext(options))
        Assert((await context.Questions.SingleAsync(question => question.Id == 5)).LatinText == originalPhrase.LatinText,
            "Avvisade redigeringar lämnar frasen oförändrad");
    Assert((await SavePhrase(editForm)).StatusCode == HttpStatusCode.Redirect,
        "Giltig frasändring sparas direkt utan migration");
    var savedEditPage = WebUtility.HtmlDecode(await client.GetStringAsync("/ContentEditor/Edit/5"));
    Assert(savedEditPage.Contains("Ändringen är sparad") && savedEditPage.Contains("ane shimey … behal") && savedEditPage.Contains("tidigare inspelningen kopplades bort"),
        "Redigeraren bekräftar sparandet och visar den nya frasen");
    var savedLessonPage = WebUtility.HtmlDecode(await client.GetStringAsync("/Lessons/Details/2"));
    var savedQuizPage = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Index/2"));
    Assert(savedLessonPage.Contains("ane shimey … behal") && savedQuizPage.Contains("ane shimey … behal"),
        "Ny fras visas direkt i både lektion och quiz");
    var staleEdit = new Dictionary<string, string>(editForm) { ["LatinText"] = "gammal flik" };
    var staleEditResponse = await SavePhrase(staleEdit);
    Assert(staleEditResponse.StatusCode == HttpStatusCode.Conflict
        && WebUtility.HtmlDecode(await staleEditResponse.Content.ReadAsStringAsync()).Contains("Versionen som är sparad just nu"),
        "Samtidig redigering visar versionskonflikt i stället för att skriva över sparad text");
    using (var context = new ApplicationDbContext(options))
    {
        var savedQuestion = await context.Questions.Include(question => question.Choices).SingleAsync(question => question.Id == 5);
        Assert(savedQuestion.LatinText == "ane shimey … behal" && savedQuestion.AudioFileName is null
            && savedQuestion.LessonId == originalPhrase.LessonId && savedQuestion.Text == originalPhrase.Text
            && savedQuestion.Choices.Select(choice => choice.Id).Order().SequenceEqual(originalPhrase.Choices.Select(choice => choice.Id).Order())
            && savedQuestion.Choices.Count(choice => choice.IsCorrect) == 1,
            "Ändrad tigrinjafras kopplar bort gammalt ljud och bevarar ID:n, lektion och källskrift");
        Assert(editorProgress == System.Text.Json.JsonSerializer.Serialize(await context.UserProgresses.AsNoTracking().OrderBy(progress => progress.Id).ToListAsync())
            && editorReviews == System.Text.Json.JsonSerializer.Serialize(await context.QuestionReviewStates.AsNoTracking().OrderBy(state => state.Id).ToListAsync())
            && !context.Database.HasPendingModelChanges(),
            "Sparade resultat och repetitionsstatus är oförändrade och ingen migration krävs");
    }
    using (var audienceDb = new ApplicationDbContext(options))
        Assert((await audienceDb.Questions.SingleAsync(question => question.Id == 5)).Audience == PhraseAudience.SameForBoth
            && WebUtility.HtmlDecode(await client.GetStringAsync("/Lessons/Details/2")).Contains("Samma form till man och kvinna"),
            "Redigerarens tilltal sparas i databasen och visas i lärandet");
    var export = await client.GetAsync("/ContentEditor/Export");
    using (var exportedContent = System.Text.Json.JsonDocument.Parse(await export.Content.ReadAsStringAsync()))
        Assert(export.IsSuccessStatusCode && export.Content.Headers.ContentDisposition?.DispositionType == "attachment"
            && exportedContent.RootElement.GetProperty("Phrases").GetArrayLength() == 77
            && exportedContent.RootElement.GetProperty("Phrases").EnumerateArray().Single(item => item.GetProperty("Id").GetInt32() == 5)
                .GetProperty("LatinText").GetString() == "ane shimey … behal"
            && !(await export.Content.ReadAsStringAsync()).Contains("UserId"),
            "JSON-export innehåller aktuella fraser och inga användarkonton eller framsteg");
    // Återställ bara vår teständring inför den frivilliga förhandsvisningen.
    var restoreForm = new Dictionary<string, string>(editForm)
    {
        ["Audience"] = "0", ["EditStamp"] = EditStamp(savedEditPage), ["LatinText"] = originalPhrase.LatinText,
        ["AudioFileName"] = originalPhrase.AudioFileName ?? ""
    };
    Assert((await SavePhrase(restoreForm)).StatusCode == HttpStatusCode.Redirect,
        "En tidigare frasversion kan sparas tillbaka genom samma validerade formulär");
    var restoredPage = WebUtility.HtmlDecode(await client.GetStringAsync("/ContentEditor/Edit/5"));
    var swedishOnlyEdit = new Dictionary<string, string>(restoreForm)
    {
        ["EditStamp"] = EditStamp(restoredPage), ["Choices[0].Text"] = "Jag heter ..."
    };
    Assert((await SavePhrase(swedishOnlyEdit)).StatusCode == HttpStatusCode.Redirect,
        "En svensk textkorrigering kan sparas separat");
    using (var context = new ApplicationDbContext(options))
        Assert((await context.Questions.SingleAsync(question => question.Id == 5)).AudioFileName == originalPhrase.AudioFileName,
            "Oförändrad tigrinjafras behåller sin inspelning när bara svenskan ändras");
    var newAudioPage = WebUtility.HtmlDecode(await client.GetStringAsync("/ContentEditor/Edit/5"));
    var chooseNewAudio = new Dictionary<string, string>(restoreForm)
    {
        ["EditStamp"] = EditStamp(newAudioPage), ["LatinText"] = "test av ny fras", ["AudioFileName"] = fixtureAudioB
    };
    Assert((await SavePhrase(chooseNewAudio)).StatusCode == HttpStatusCode.Redirect,
        "En annan uttryckligen vald inspelning kan sparas med en ändrad fras");
    using (var context = new ApplicationDbContext(options))
        Assert((await context.Questions.SingleAsync(question => question.Id == 5)).AudioFileName == fixtureAudioB,
            "En ny vald ljudfil tas inte bort av skyddet för den gamla filen");
    var finalRestorePage = WebUtility.HtmlDecode(await client.GetStringAsync("/ContentEditor/Edit/5"));
    restoreForm["EditStamp"] = EditStamp(finalRestorePage);
    Assert((await SavePhrase(restoreForm)).StatusCode == HttpStatusCode.Redirect,
        "Testets ursprungliga fras, svenska svar och ljud kan återställas");
    Assert(File.Exists(Path.Combine(webRoot, "wwwroot/audio/tigrinya", originalPhrase.AudioFileName!)),
        "Bortkoppling tar inte bort själva ljudfilen från ljudmappen");
    var createPage = WebUtility.HtmlDecode(await client.GetStringAsync("/PhraseCreation"));
    string CreationToken(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"CreationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    Assert(createPage.Contains("Spara ny fras") && createPage.Contains("Skapa en ny lektion")
        && Regex.Matches(createPage, "name=\"CorrectChoiceIndex\"").Count == 4 && CreationToken(createPage).Length > 0,
        "Nya fraser kan fyllas i med fyra alternativ, lektionsval och skyddat formulär");
    var createForm = new Dictionary<string, string>
    {
        ["CreationToken"] = CreationToken(createPage), ["LessonId"] = "0", ["CourseId"] = "1",
        ["Audience"] = "4", ["NewLessonTitle"] = "Mina egna fraser", ["LatinText"] = "selam", ["CorrectChoiceIndex"] = "0", ["AudioFileName"] = "",
        ["Choices[0].Text"] = "Hej", ["Choices[1].Text"] = "Välkommen", ["Choices[2].Text"] = "Tack", ["Choices[3].Text"] = "Hej då",
        ["Id"] = "1", ["Choices[0].Id"] = "1"
    };
    async Task<HttpResponseMessage> CreatePhrase(Dictionary<string, string> form, HttpClient? target = null)
    {
        form["__RequestVerificationToken"] = await Token("/PhraseCreation", target);
        return await (target ?? client).PostAsync("/PhraseCreation", new FormUrlEncodedContent(form));
    }
    int lessonCountBeforeCreate;
    int questionCountBeforeCreate;
    string progressBeforeCreate;
    using (var context = new ApplicationDbContext(options))
    {
        lessonCountBeforeCreate = await context.Lessons.CountAsync();
        questionCountBeforeCreate = await context.Questions.CountAsync();
        progressBeforeCreate = System.Text.Json.JsonSerializer.Serialize(await context.UserProgresses.AsNoTracking().OrderBy(progress => progress.Id).ToListAsync());
    }
    Assert((await client.PostAsync("/PhraseCreation", new FormUrlEncodedContent(createForm))).StatusCode == HttpStatusCode.BadRequest,
        "Skapande kräver antiforgery-token");
    Assert((await CreatePhrase(new Dictionary<string, string>(createForm) { ["CreationToken"] = "tampered" })).StatusCode == HttpStatusCode.BadRequest
        && (await CreatePhrase(new Dictionary<string, string>(createForm), secondClient)).StatusCode == HttpStatusCode.BadRequest,
        "Skapandeformulär kan inte förfalskas eller användas av ett annat konto");
    foreach (var invalidCreate in new[]
    {
        new Dictionary<string, string>(createForm) { ["Audience"] = "999" },
        new Dictionary<string, string>(createForm) { ["Audience"] = "ogiltigt" },
        new Dictionary<string, string>(createForm) { ["NewLessonTitle"] = "" },
        new Dictionary<string, string>(createForm) { ["LatinText"] = " " },
        new Dictionary<string, string>(createForm) { ["LatinText"] = "ሰላም" },
        new Dictionary<string, string>(createForm) { ["Choices[1].Text"] = "Hej" },
        new Dictionary<string, string>(createForm) { ["CorrectChoiceIndex"] = "4" },
        new Dictionary<string, string>(createForm) { ["CourseId"] = "2147483647" },
        new Dictionary<string, string>(createForm) { ["AudioFileName"] = "../appsettings.json" },
        new Dictionary<string, string>(createForm) { ["LessonId"] = "1" }
    })
    {
        var response = await CreatePhrase(invalidCreate);
        Assert(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsStringAsync()).Contains("validation-summary-errors"),
            "Ogiltig ny fras eller låst placering ger formulärfel utan skapande");
    }
    using (var context = new ApplicationDbContext(options))
        Assert(await context.Lessons.CountAsync() == lessonCountBeforeCreate && await context.Questions.CountAsync() == questionCountBeforeCreate,
            "Avvisade formulär lämnar inga tomma lektioner eller halvskapade frågor");
    var createdPhraseResponse = await CreatePhrase(createForm);
    var createdPhraseLocation = createdPhraseResponse.Headers.Location?.ToString() ?? "";
    Assert(createdPhraseResponse.StatusCode == HttpStatusCode.Redirect && createdPhraseLocation.StartsWith("/ContentEditor/Edit/", StringComparison.Ordinal),
        "Ny lektion och dess första fras sparas tillsammans och öppnas i redigeraren");
    var createdQuestionId = int.Parse(createdPhraseLocation.Split('/').Last());
    int createdLessonId;
    Question firstCreatedQuestion;
    using (var context = new ApplicationDbContext(options))
    {
        firstCreatedQuestion = await context.Questions.Include(question => question.Choices).Include(question => question.Lesson).SingleAsync(question => question.Id == createdQuestionId);
        createdLessonId = firstCreatedQuestion.LessonId;
        Assert(firstCreatedQuestion.LatinText == "selam" && firstCreatedQuestion.Audience == PhraseAudience.SameForBoth && firstCreatedQuestion.AudioFileName is null
            && firstCreatedQuestion.Choices.Count == 4 && firstCreatedQuestion.Choices.Count(choice => choice.IsCorrect) == 1
            && firstCreatedQuestion.Choices.Single(choice => choice.IsCorrect).Text == "Hej"
            && firstCreatedQuestion.Choices.All(choice => choice.Id > 8036)
            && firstCreatedQuestion.Lesson!.Title == "Mina egna fraser" && firstCreatedQuestion.Lesson.CourseId == 1
            && firstCreatedQuestion.Lesson.Order == 20,
            "Appen skapar unika ID:n och rätt lektionskoppling, alternativ och ordning");
    }
    Assert((await CreatePhrase(createForm)).Headers.Location?.ToString() == createdPhraseLocation,
        "Dubbel inskickning under samma appkörning öppnar den skapade frasen utan dubblett");
    var firstCreatedLessonPage = WebUtility.HtmlDecode(await client.GetStringAsync($"/Lessons/Details/{createdLessonId}"));
    Assert(firstCreatedLessonPage.Contains("Mina egna fraser") && firstCreatedLessonPage.Contains("selam")
        && Regex.Matches(firstCreatedLessonPage, "type=\"radio\"").Count == 4 && !firstCreatedLessonPage.Contains("<audio"),
        "Den egna lektionen fungerar omedelbart som lärande och quiz utan ljud");
    var oldSingleQuestionForm = new Dictionary<string, string>
    {
        ["Answers[0].QuestionId"] = createdQuestionId.ToString(),
        ["Answers[0].ChoiceId"] = firstCreatedQuestion.Choices.Single(choice => choice.IsCorrect).Id.ToString(),
        ["Answers[0].Kind"] = "Read"
    };
    var addMorePage = WebUtility.HtmlDecode(await client.GetStringAsync($"/PhraseCreation?lessonId={createdLessonId}"));
    var addMoreForm = new Dictionary<string, string>(createForm)
    {
        ["CreationToken"] = CreationToken(addMorePage), ["LessonId"] = createdLessonId.ToString(), ["NewLessonTitle"] = "",
        ["LatinText"] = "yekenyeley", ["AudioFileName"] = fixtureAudioB,
        ["Choices[0].Text"] = "Tack", ["Choices[1].Text"] = "Hej", ["Choices[2].Text"] = "Välkommen"
    };
    Assert((await CreatePhrase(addMoreForm)).StatusCode == HttpStatusCode.Redirect,
        "En till fras och vald inspelning kan läggas i en ännu inte påbörjad lektion");
    var duplicateCreatePage = await client.GetStringAsync($"/PhraseCreation?lessonId={createdLessonId}");
    var duplicatePhraseForm = new Dictionary<string, string>(addMoreForm) { ["CreationToken"] = CreationToken(duplicateCreatePage) };
    var duplicatePhraseResponse = await CreatePhrase(duplicatePhraseForm);
    Assert(duplicatePhraseResponse.StatusCode == HttpStatusCode.OK
        && WebUtility.HtmlDecode(await duplicatePhraseResponse.Content.ReadAsStringAsync()).Contains("Uttrycket finns redan"),
        "Samma tigrinjafras kan inte läggas till två gånger i samma lektion");
    oldSingleQuestionForm["__RequestVerificationToken"] = await Token($"/Quiz/Index/{createdLessonId}");
    Assert((await client.PostAsync($"/Quiz/Submit/{createdLessonId}", new FormUrlEncodedContent(oldSingleQuestionForm))).StatusCode == HttpStatusCode.BadRequest,
        "Ett äldre quiz med färre frågor kan inte felaktigt klara en utökad lektion");
    List<Question> createdQuestions;
    using (var context = new ApplicationDbContext(options))
    {
        createdQuestions = await context.Questions.Include(question => question.Choices).Where(question => question.LessonId == createdLessonId).OrderBy(question => question.Id).ToListAsync();
        Assert(createdQuestions.Count == 2 && createdQuestions[1].AudioFileName == fixtureAudioB
            && await context.Lessons.CountAsync() == lessonCountBeforeCreate + 1
            && await context.Questions.CountAsync() == questionCountBeforeCreate + 2
            && progressBeforeCreate == System.Text.Json.JsonSerializer.Serialize(await context.UserProgresses.AsNoTracking().OrderBy(progress => progress.Id).ToListAsync())
            && !context.Database.HasPendingModelChanges(),
            "Skapande ändrar inga gamla resultat och kräver ingen ny migration");
    }
    var ownLessonAnswers = new Dictionary<string, string> { ["__RequestVerificationToken"] = await Token($"/Quiz/Index/{createdLessonId}") };
    for (var i = 0; i < createdQuestions.Count; i++)
    {
        ownLessonAnswers[$"Answers[{i}].QuestionId"] = createdQuestions[i].Id.ToString();
        ownLessonAnswers[$"Answers[{i}].ChoiceId"] = createdQuestions[i].Choices.Single(choice => choice.IsCorrect).Id.ToString();
        ownLessonAnswers[$"Answers[{i}].Kind"] = "Read";
    }
    Assert((await client.PostAsync($"/Quiz/Submit/{createdLessonId}", new FormUrlEncodedContent(ownLessonAnswers))).StatusCode == HttpStatusCode.Redirect
        && WebUtility.HtmlDecode(await client.GetStringAsync($"/Quiz/Result/{createdLessonId}")).Contains("2 av 2 rätt"),
        "Den egna lektionen rättas och sparar framsteg med vanliga quizflödet");
    var lockedCreatePage = await client.GetStringAsync($"/PhraseCreation?lessonId={createdLessonId}");
    var lockedCreateForm = new Dictionary<string, string>(addMoreForm)
    {
        ["CreationToken"] = CreationToken(lockedCreatePage), ["LatinText"] = "merhaba"
    };
    var lockedCreateResponse = await CreatePhrase(lockedCreateForm);
    Assert(lockedCreateResponse.StatusCode == HttpStatusCode.OK
        && WebUtility.HtmlDecode(await lockedCreateResponse.Content.ReadAsStringAsync()).Contains("Lektionen har sparade resultat"),
        "En påbörjad lektion skyddas mot nytt frågeantal även om ett gammalt formulär skickas in");
    using (var context = new ApplicationDbContext(options))
    {
        foreach (var question in await context.Questions.Where(question => question.AudioFileName == fixtureAudioA || question.AudioFileName == fixtureAudioB).ToListAsync())
            question.AudioFileName = null;
        await context.SaveChangesAsync();
    }
    foreach (var path in audioFixturePaths) File.Delete(path);
    // Riktiga HTTP-anrop verifierar spelets privata rättning och sparande.
    async Task<(string Token, string Csrf, TigrinyaLingo.Models.GameBoardViewModel Board)> OpenGame(string mode = "Pairs")
    {
        var html = await client.GetStringAsync("/Games/Match?mode=" + mode);
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"SessionToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var csrf = WebUtility.HtmlDecode(Regex.Match(Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value, "value=\"([^\"]+)\"").Groups[1].Value);
        var json = Regex.Match(html, "<script type=\"application/json\" id=\"match-game-board\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        return (token, csrf, System.Text.Json.JsonSerializer.Deserialize<TigrinyaLingo.Models.GameBoardViewModel>(json)!);
    }
    async Task<HttpResponseMessage> CheckGame(string token, string csrf, string left, string right, Guid? request = null, HttpClient? target = null) =>
        await (target ?? client).PostAsync("/Games/CheckPair", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SessionToken"] = token, ["__RequestVerificationToken"] = csrf, ["SwedishKey"] = left,
            ["TigrinyaKey"] = right, ["RequestId"] = (request ?? Guid.NewGuid()).ToString(), ["Points"] = "999999"
        }));
    async Task<HttpResponseMessage> FinishGame(string token, string csrf) => await client.PostAsync("/Games/Finish", new FormUrlEncodedContent(new Dictionary<string, string>
    { ["SessionToken"] = token, ["__RequestVerificationToken"] = csrf }));
    async Task<string> GameDatabaseSnapshot(bool reviews = true)
    {
        using var db = new ApplicationDbContext(options);
        return System.Text.Json.JsonSerializer.Serialize(new {
            Progress = await db.UserProgresses.AsNoTracking().OrderBy(item => item.Id).ToListAsync(),
            Reviews = reviews ? await db.QuestionReviewStates.AsNoTracking().OrderBy(item => item.Id).ToListAsync() : null
        });
    }
    List<Question> gameQuestions;
    using (var db = new ApplicationDbContext(options)) gameQuestions = await db.Questions.Include(question => question.Choices).ToListAsync();
    Question GameQuestion(TigrinyaLingo.Models.GameRoundViewModel boardRound, TigrinyaLingo.Models.GameCardViewModel card) =>
        gameQuestions.First(question => question.LatinText == card.Text && boardRound.Swedish.Any(left => left.Text == question.Choices.Single(choice => choice.IsCorrect).Text));
    async Task CompleteGame(string token, string csrf, TigrinyaLingo.Models.GameBoardViewModel board)
    {
        foreach (var boardRound in board.Rounds)
            foreach (var right in boardRound.Tigrinya)
            {
                var meaning = GameQuestion(boardRound, right).Choices.Single(choice => choice.IsCorrect).Text;
                var left = boardRound.Swedish.Single(card => card.Text == meaning);
                var matchRequest = Guid.NewGuid();
                var response = await CheckGame(token, csrf, left.Key, right.Key, matchRequest);
                if (right == boardRound.Tigrinya[0]) Assert(await (await CheckGame(token, csrf, left.Key, right.Key, matchRequest)).Content.ReadAsStringAsync() == await response.Content.ReadAsStringAsync(), "Dubbelklick på ett rätt par ger inte dubbla poäng");
                Assert(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsStringAsync()).Contains("\"isCorrect\":true"), "Servern rättar ett riktigt ordpar");
            }
    }
    var gameStart = await OpenGame();
    Assert(gameStart.Token.Length > 0 && gameStart.Board.Rounds.Count == 3
        && gameStart.Board.Rounds.All(item => item.Swedish.Count is >= 2 and <= 4)
        && gameStart.Board.Rounds.SelectMany(item => item.Swedish).Select(item => item.Key).Intersect(gameStart.Board.Rounds.SelectMany(item => item.Tigrinya).Select(item => item.Key)).Count() == 0,
        "Spelet har balanserade rundor och inga gemensamma kort-ID:n som avslöjar paren");
    Assert((await FinishGame(gameStart.Token, gameStart.Csrf)).StatusCode == HttpStatusCode.BadRequest, "Ett ofärdigt spel kan inte sparas");
    var firstGameRound = gameStart.Board.Rounds[0];
    var firstRight = firstGameRound.Tigrinya[0];
    var firstQuestion = GameQuestion(firstGameRound, firstRight);
    var firstMeaning = firstQuestion.Choices.Single(choice => choice.IsCorrect).Text;
    var firstLeft = firstGameRound.Swedish.Single(item => item.Text == firstMeaning);
    Assert((await CheckGame(gameStart.Token, await Token("/", secondClient), firstLeft.Key, firstRight.Key, target: secondClient)).StatusCode == HttpStatusCode.Conflict,
        "En annan användare kan inte använda spelomgångens token");
    Assert((await CheckGame(gameStart.Token + "x", gameStart.Csrf, firstLeft.Key, firstRight.Key)).StatusCode == HttpStatusCode.Conflict
        && (await CheckGame(gameStart.Token, gameStart.Csrf, Guid.NewGuid().ToString(), firstRight.Key)).StatusCode == HttpStatusCode.BadRequest
        && (await CheckGame(gameStart.Token, gameStart.Csrf, gameStart.Board.Rounds[1].Swedish[0].Key, firstRight.Key)).StatusCode == HttpStatusCode.BadRequest,
        "Manipulerade tokens, okända kort och kort från nästa runda avvisas");
    Assert((await CheckGame(gameStart.Token, "", firstLeft.Key, firstRight.Key)).StatusCode == HttpStatusCode.BadRequest,
        "Spelets rättning kräver antiforgery-token");
    var snapshotBeforeGame = await GameDatabaseSnapshot();
    var wrongRight = firstGameRound.Tigrinya.First(item => item.Key != firstRight.Key);
    var requestId = Guid.NewGuid();
    var wrongGame = await CheckGame(gameStart.Token, gameStart.Csrf, firstLeft.Key, wrongRight.Key, requestId);
    var wrongGameJson = await wrongGame.Content.ReadAsStringAsync();
    Assert(wrongGame.StatusCode == HttpStatusCode.OK && wrongGameJson.Contains("\"isCorrect\":false") && wrongGameJson.Contains("\"points\":0")
        && (await (await CheckGame(gameStart.Token, gameStart.Csrf, firstLeft.Key, wrongRight.Key, requestId)).Content.ReadAsStringAsync()) == wrongGameJson,
        "Fel par ger inga poäng och nätverksomskickning räknas en gång");
    Assert((await CheckGame(gameStart.Token, gameStart.Csrf, firstLeft.Key, firstRight.Key, requestId)).StatusCode == HttpStatusCode.BadRequest,
        "Ett request-ID kan inte återanvändas med ett annat par");
    await CompleteGame(gameStart.Token, gameStart.Csrf, gameStart.Board);
    Assert(await GameDatabaseSnapshot() == snapshotBeforeGame, "Kortval skriver inte ofärdiga framsteg till databasen");
    var scoresBeforeGame = await GameDatabaseSnapshot(false);
    Assert((await FinishGame(gameStart.Token, gameStart.Csrf)).StatusCode == HttpStatusCode.Redirect, "En färdig omgång kan sparas");
    var resultGameHtml = WebUtility.HtmlDecode(await client.GetStringAsync("/Games/Result"));
    using (var db = new ApplicationDbContext(options))
    {
        Assert((await db.QuestionReviewStates.SingleAsync(item => item.UserId == firstUserId && item.QuestionId == firstQuestion.Id)).NeedsReview,
            "Ett missat ord behålls i den egna repetitionen trots att det senare matchades");
    }
    Assert(await GameDatabaseSnapshot(false) == scoresBeforeGame && resultGameHtml.Contains("Din repetition är uppdaterad")
        && resultGameHtml.Contains("2 av 3 stjärnor") && !resultGameHtml.Contains("999999"),
        "Stjärnor och poäng rättas på servern utan att ändra lektionspoäng eller XP");
    var afterGame = await GameDatabaseSnapshot();
    Assert((await FinishGame(gameStart.Token, gameStart.Csrf)).StatusCode == HttpStatusCode.Redirect && await GameDatabaseSnapshot() == afterGame,
        "Dubbel inskickning sparar aldrig samma resultat en gång till");
    var memoryGame = await OpenGame("Memory");
    await CompleteGame(memoryGame.Token, memoryGame.Csrf, memoryGame.Board);
    await FinishGame(memoryGame.Token, memoryGame.Csrf);
    Assert(await GameDatabaseSnapshot() == afterGame && WebUtility.HtmlDecode(await client.GetStringAsync("/Games/Result")).Contains("Memory ändrar inte"),
        "Memory ger ett eget spelresultat och ändrar inga repetitionsflaggor eller framsteg");
    var staleGame = await OpenGame();
    var staleRight = staleGame.Board.Rounds[0].Tigrinya[0];
    var staleQuestion = GameQuestion(staleGame.Board.Rounds[0], staleRight);
    using (var db = new ApplicationDbContext(options))
    {
        var state = await db.QuestionReviewStates.SingleAsync(item => item.UserId == firstUserId && item.QuestionId == staleQuestion.Id);
        state.LastAnsweredAt = DateTime.UtcNow.AddMinutes(1); state.NeedsReview = true; await db.SaveChangesAsync();
    }
    var newerSnapshot = await GameDatabaseSnapshot();
    await CompleteGame(staleGame.Token, staleGame.Csrf, staleGame.Board);
    await FinishGame(staleGame.Token, staleGame.Csrf);
    Assert(await GameDatabaseSnapshot() == newerSnapshot && WebUtility.HtmlDecode(await client.GetStringAsync("/Games/Result")).Contains("din nyare repetition har behållits"),
        "En äldre spelomgång får inte skriva över nyare repetitionsresultat");
    var editedGame = await OpenGame();
    var editedQuestion = GameQuestion(editedGame.Board.Rounds[0], editedGame.Board.Rounds[0].Tigrinya[0]);
    using (var db = new ApplicationDbContext(options))
    {
        var question = await db.Questions.SingleAsync(item => item.Id == editedQuestion.Id);
        question.LatinText += " (teständring)"; await db.SaveChangesAsync();
    }
    await CompleteGame(editedGame.Token, editedGame.Csrf, editedGame.Board);
    await FinishGame(editedGame.Token, editedGame.Csrf);
    Assert(await GameDatabaseSnapshot() == newerSnapshot && WebUtility.HtmlDecode(await client.GetStringAsync("/Games/Result")).Contains("din nyare repetition har behållits"),
        "Fraser som ändrats under spelet skrivs inte in som gamla repetitionsresultat");
    using (var db = new ApplicationDbContext(options))
    {
        (await db.Questions.SingleAsync(item => item.Id == editedQuestion.Id)).LatinText = editedQuestion.LatinText; await db.SaveChangesAsync();
    }

    // Vardagssamtal: kända uttryck i sammanhang, med separat serverrättning.
    var dialogueHub = WebUtility.HtmlDecode(await client.GetStringAsync("/Conversations"));
    Assert(dialogueHub.Contains("Ett första möte") && dialogueHub.Contains("När du behöver hjälp") && dialogueHub.Contains("Hälsningar under dagen")
        && Regex.Matches(dialogueHub, "Starta samtalet").Count == 3, "Tre vardagssamtal öppnas efter sina klara lektioner");
    Assert((await secondClient.GetAsync("/Conversations/Play/first-meeting")).StatusCode == HttpStatusCode.Forbidden
        && (await client.GetAsync("/Conversations/Play/does-not-exist")).StatusCode == HttpStatusCode.NotFound,
        "Ett delvis klart quiz öppnar inte samtalet och okända samtal ger 404");
    async Task<(string Token, string Csrf, TigrinyaLingo.Models.ConversationBoardViewModel Board, string Html)> OpenConversation(string scene = "first-meeting")
    {
        var html = await client.GetStringAsync("/Conversations/Play/" + scene);
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"SessionToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var csrf = WebUtility.HtmlDecode(Regex.Match(Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value, "value=\"([^\"]+)\"").Groups[1].Value);
        var json = Regex.Match(html, "<script type=\"application/json\" id=\"conversation-board\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        return (token, csrf, System.Text.Json.JsonSerializer.Deserialize<TigrinyaLingo.Models.ConversationBoardViewModel>(json)!, html);
    }
    async Task<HttpResponseMessage> AnswerConversation(string token, string csrf, Guid turn, Guid choice, Guid? request = null, HttpClient? target = null) =>
        await (target ?? client).PostAsync("/Conversations/CheckAnswer", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["SessionToken"] = token, ["__RequestVerificationToken"] = csrf, ["TurnKey"] = turn.ToString(), ["ChoiceKey"] = choice.ToString(),
            ["RequestId"] = (request ?? Guid.NewGuid()).ToString(), ["Score"] = "99999", ["UserId"] = secondUserId
        }));
    async Task<HttpResponseMessage> FinishConversation(string token, string csrf) => await client.PostAsync("/Conversations/Finish",
        new FormUrlEncodedContent(new Dictionary<string, string> { ["SessionToken"] = token, ["__RequestVerificationToken"] = csrf }));
    async Task<List<Guid>> ConversationAnswers(TigrinyaLingo.Models.ConversationBoardViewModel board, int lessonId, string[] meanings)
    {
        using var db = new ApplicationDbContext(options);
        var rows = await db.Questions.Include(question => question.Choices).Where(question => question.LessonId == lessonId).ToListAsync();
        return board.Turns.Select((turn, index) => turn.Choices.Single(choice => choice.Text == rows.Single(question => question.Choices.Single(answer => answer.IsCorrect).Text == meanings[index]).LatinText).Key).ToList();
    }
    async Task CompleteConversation(string token, string csrf, TigrinyaLingo.Models.ConversationBoardViewModel board, List<Guid> correct, int start = 0)
    {
        for (var i = start; i < board.Turns.Count; i++)
        {
            var response = await AnswerConversation(token, csrf, board.Turns[i].Key, correct[i]);
            Assert(response.StatusCode == HttpStatusCode.OK && (await response.Content.ReadAsStringAsync()).Contains("\"isCorrect\":true"),
                "Ett aktuellt frassvar rättas som passande i samtalet");
        }
    }
    var conversation = await OpenConversation();
    var conversationKeys = await ConversationAnswers(conversation.Board, 1, new[] { "Hej", "Välkommen", "Tack", "Hej då" });
    Assert(conversation.Token.Length > 0 && conversation.Board.Turns.Count == 4 && conversation.Board.Turns.All(turn => turn.Choices.Count == 4
        && turn.Choices.Select(choice => choice.Key).Distinct().Count() == 4 && turn.Choices.Select(choice => choice.Text).Distinct().Count() == 4)
        && !conversation.Html.Contains("IsCorrect") && !conversation.Html.Contains("CorrectChoiceKey") && !conversation.Html.Contains("QuestionId")
        && !conversation.Html.Contains("<audio") && !Regex.IsMatch(conversation.Html, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"),
        "Samtalet har fyra repliker med fyra latinska svar utan facit, fråge-ID:n eller ljud i HTML");
    Assert((await FinishConversation(conversation.Token, conversation.Csrf)).StatusCode == HttpStatusCode.BadRequest,
        "Ett ofärdigt samtal kan inte sparas");
    Assert((await AnswerConversation(conversation.Token, "", conversation.Board.Turns[0].Key, conversationKeys[0])).StatusCode == HttpStatusCode.BadRequest
        && (await AnswerConversation(conversation.Token, await Token("/", secondClient), conversation.Board.Turns[0].Key, conversationKeys[0], target: secondClient)).StatusCode == HttpStatusCode.Conflict
        && (await AnswerConversation(conversation.Token + "x", conversation.Csrf, conversation.Board.Turns[0].Key, conversationKeys[0])).StatusCode == HttpStatusCode.Conflict,
        "Samtal kräver antiforgery och kan inte användas av andra konton eller med manipulerad token");
    Assert((await AnswerConversation(conversation.Token, conversation.Csrf, conversation.Board.Turns[1].Key, conversationKeys[1])).StatusCode == HttpStatusCode.BadRequest
        && (await AnswerConversation(conversation.Token, conversation.Csrf, conversation.Board.Turns[0].Key, Guid.NewGuid())).StatusCode == HttpStatusCode.BadRequest,
        "Repliker måste besvaras i ordning och främmande alternativ avvisas");
    var beforeConversation = await GameDatabaseSnapshot();
    var beforeConversationXp = await GameDatabaseSnapshot(false);
    var wrongConversationKey = conversation.Board.Turns[0].Choices.First(choice => choice.Key != conversationKeys[0]).Key;
    var conversationRequest = Guid.NewGuid();
    var wrongConversation = await AnswerConversation(conversation.Token, conversation.Csrf, conversation.Board.Turns[0].Key, wrongConversationKey, conversationRequest);
    var wrongConversationJson = await wrongConversation.Content.ReadAsStringAsync();
    Assert(wrongConversation.StatusCode == HttpStatusCode.OK && wrongConversationJson.Contains("\"isCorrect\":false") && wrongConversationJson.Contains("\"points\":0")
        && wrongConversationJson.Contains("selam") && wrongConversationJson.Contains("Hej")
        && await (await AnswerConversation(conversation.Token, conversation.Csrf, conversation.Board.Turns[0].Key, wrongConversationKey, conversationRequest)).Content.ReadAsStringAsync() == wrongConversationJson,
        "Fel svar ger förklarat facit och en omskickning räknas bara en gång");
    Assert((await AnswerConversation(conversation.Token, conversation.Csrf, conversation.Board.Turns[0].Key, conversationKeys[0], conversationRequest)).StatusCode == HttpStatusCode.BadRequest,
        "Ett svar kan inte bytas ut under samma request-ID");
    await CompleteConversation(conversation.Token, conversation.Csrf, conversation.Board, conversationKeys, start: 1);
    Assert(await GameDatabaseSnapshot() == beforeConversation, "Samtalets kortval sparar inga ofärdiga repetitionsresultat");
    Assert((await FinishConversation(conversation.Token, conversation.Csrf)).StatusCode == HttpStatusCode.Redirect,
        "Ett färdigt samtal kan avslutas och sparas");
    var conversationResult = WebUtility.HtmlDecode(await client.GetStringAsync("/Conversations/Result"));
    using (var db = new ApplicationDbContext(options))
    {
        var review = await db.QuestionReviewStates.Where(state => state.UserId == firstUserId && state.QuestionId <= 4).ToListAsync();
        Assert(review.Single(state => state.QuestionId == 1).NeedsReview && review.Where(state => state.QuestionId != 1).All(state => !state.NeedsReview),
            "Missat samtalsuttryck sparas för repetition medan rätta uttryck markeras rätt");
    }
    Assert(conversationResult.Contains("3 av 4") && conversationResult.Contains("300") && conversationResult.Contains("2 av 3 stjärnor")
        && !conversationResult.Contains("99999") && await GameDatabaseSnapshot(false) == beforeConversationXp,
        "Samtalspoäng och stjärnor räknas på servern utan lektions-XP eller upplåsning");
    var afterConversation = await GameDatabaseSnapshot();
    await FinishConversation(conversation.Token, conversation.Csrf);
    await client.GetStringAsync("/Conversations/Result");
    Assert(await GameDatabaseSnapshot() == afterConversation, "Dubbel avslutning och omladdning skriver inte samma samtalsresultat igen");
    // Ny latinsk text används omedelbart; en ändring under omgången skriver inte över repetition.
    string originalDialogueLatin;
    using (var db = new ApplicationDbContext(options))
    {
        var question = await db.Questions.SingleAsync(question => question.Id == 1);
        originalDialogueLatin = question.LatinText; question.LatinText += " (lokalt test)"; await db.SaveChangesAsync();
    }
    var editedConversation = await OpenConversation();
    var editedConversationKeys = await ConversationAnswers(editedConversation.Board, 1, new[] { "Hej", "Välkommen", "Tack", "Hej då" });
    Assert(editedConversation.Board.Turns[0].Choices.Any(choice => choice.Text == originalDialogueLatin + " (lokalt test)"),
        "Samtal hämtar den aktuella redigerade tigrinjafrasen från databasen");
    await CompleteConversation(editedConversation.Token, editedConversation.Csrf, editedConversation.Board, editedConversationKeys);
    using (var db = new ApplicationDbContext(options))
    {
        (await db.Questions.SingleAsync(question => question.Id == 1)).LatinText = originalDialogueLatin; await db.SaveChangesAsync();
    }
    await FinishConversation(editedConversation.Token, editedConversation.Csrf);
    Assert(await GameDatabaseSnapshot() == afterConversation && WebUtility.HtmlDecode(await client.GetStringAsync("/Conversations/Result")).Contains("Din nyare repetition har behållits"),
        "En frasändring under samtalet hindrar gamla svar från att uppdatera repetitionen");
    var staleConversation = await OpenConversation();
    var staleConversationKeys = await ConversationAnswers(staleConversation.Board, 1, new[] { "Hej", "Välkommen", "Tack", "Hej då" });
    await CompleteConversation(staleConversation.Token, staleConversation.Csrf, staleConversation.Board, staleConversationKeys);
    using (var db = new ApplicationDbContext(options))
    {
        var state = await db.QuestionReviewStates.SingleAsync(state => state.UserId == firstUserId && state.QuestionId == 1);
        state.NeedsReview = true; state.LastAnsweredAt = DateTime.UtcNow.AddMinutes(1); await db.SaveChangesAsync();
    }
    var newerConversationState = await GameDatabaseSnapshot();
    await FinishConversation(staleConversation.Token, staleConversation.Csrf);
    Assert(await GameDatabaseSnapshot() == newerConversationState && WebUtility.HtmlDecode(await client.GetStringAsync("/Conversations/Result")).Contains("Din nyare repetition har behållits"),
        "Ett äldre samtal skriver inte över senare repetitionssvar");
    using (var db = new ApplicationDbContext(options))
    {
        var meaning = await db.Choices.SingleAsync(choice => choice.QuestionId == 1 && choice.IsCorrect);
        var originalMeaning = meaning.Text; meaning.Text = "En annan betydelse"; await db.SaveChangesAsync();
        Assert((await client.GetAsync("/Conversations/Play/first-meeting")).StatusCode == HttpStatusCode.Conflict
            && WebUtility.HtmlDecode(await client.GetStringAsync("/Conversations")).Contains("betydelser behöver granskas"),
            "Ändrad svensk betydelse stoppar ett samtal som inte längre passar innehållet");
        meaning.Text = originalMeaning; await db.SaveChangesAsync();
    }
    foreach (var slug in new[] { "ask-for-help", "through-the-day" })
    {
        var availableConversation = await OpenConversation(slug);
        Assert(availableConversation.Board.Turns.Count == 4 && availableConversation.Board.Turns.All(turn => turn.Choices.Count == 4),
            "Hjälpfraser och dagshälsningar har kompletta samtal efter rätt lektion");
    }

    // Tilltal måste finnas även utanför lärkorten, utan att originalbetydelser ändras.
    var audienceLesson = WebUtility.HtmlDecode(await client.GetStringAsync("/Quiz/Index/4"));
    var genderFields = Regex.Matches(audienceLesson, @"<fieldset[^>]*data-question[^>]*>[\s\S]*?</fieldset>").Select(match => match.Value).ToList();
    Assert(genderFields.Count == 4 && genderFields.Count(field => field.Contains("(m) Till en man")) == 2
        && genderFields.Count(field => field.Contains("(f) Till en kvinna")) == 2
        && genderFields.All(field => !field.Contains("phrase-other-form")),
        "Quizfrågorna visar respektive tilltal men avslöjar inte motformen under rättningen");
    Assert(audienceLesson.Contains("personen du talar till") && audienceLesson.Contains("phrase-other-form")
        && audienceLesson.Contains("kemey aleka") && audienceLesson.Contains("kemey aleki"),
        "Lärdelen förklarar mottagaren och visar båda formerna med oförändrade fraser");
    var dayConversation = await OpenConversation("through-the-day");
    Assert(dayConversation.Board.Turns.All(turn => turn.Choices.Count(choice => choice.UsageLabel == "(m) Till en man") == 3),
        "Dagssamtalets alternativ visar tilltalet för varje böjd hälsning");
    using (var audienceDb = new ApplicationDbContext(options))
    {
        var morning = await audienceDb.Questions.SingleAsync(question => question.Id == 17);
        morning.LatinText = "kemey hadirki"; await audienceDb.SaveChangesAsync();
        Assert((await client.GetAsync("/Conversations/Play/through-the-day")).StatusCode == HttpStatusCode.Conflict,
            "En kvinnlig fras kan inte råka rättas som rätt i samtalets manliga sammanhang");
        morning.LatinText = "kemey hadirka"; await audienceDb.SaveChangesAsync();
    }
    using (var audienceDb = new ApplicationDbContext(options))
        Assert(!await audienceDb.Choices.AnyAsync(choice => choice.Text.Contains("(m)") || choice.Text.Contains("(f)")),
            "Tilltal ligger separat från svenska betydelser och påverkar inte facit");

    // Tilltal: samma betydelse med manlig och kvinnlig form, utan facitmarkering före svaret.
    Assert((await secondClient.GetAsync("/AddressPractice/Play")).StatusCode == HttpStatusCode.Redirect
        && WebUtility.HtmlDecode(await secondClient.GetStringAsync("/AddressPractice")).Contains("Lär dig de första frasparen"),
        "Ett konto utan klar tilltalslektion får inga fraspar från ett annat konto");
    var allAddressHub = WebUtility.HtmlDecode(await client.GetStringAsync("/AddressPractice"));
    Assert(allAddressHub.Contains("15 fraspar") && allAddressHub.Contains("Starta tilltalsträningen"),
        "Alla femton granskade fraspar blir tillgängliga efter sina lektioner");
    Dictionary<int, DateTime?> addressCompletionTimes;
    using (var db = new ApplicationDbContext(options))
        addressCompletionTimes = await db.UserProgresses.Where(saved => saved.UserId == firstUserId).ToDictionaryAsync(saved => saved.LessonId, saved => saved.CompletedAt);
    async Task AddressLessons(params int[] ids)
    {
        using var db = new ApplicationDbContext(options);
        foreach (var saved in await db.UserProgresses.Where(saved => saved.UserId == firstUserId).ToListAsync())
            saved.CompletedAt = ids.Contains(saved.LessonId) ? addressCompletionTimes[saved.LessonId] : null;
        await db.SaveChangesAsync();
    }
    async Task<(string Token, string Csrf, ConversationBoardViewModel Board, string Html)> OpenAddress()
    {
        var html = await client.GetStringAsync("/AddressPractice/Play");
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"SessionToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var csrf = WebUtility.HtmlDecode(Regex.Match(Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value, "value=\"([^\"]+)\"").Groups[1].Value);
        var json = Regex.Match(html, "<script type=\"application/json\" id=\"conversation-board\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        return (token, csrf, System.Text.Json.JsonSerializer.Deserialize<ConversationBoardViewModel>(json)!, html);
    }
    async Task<HttpResponseMessage> AnswerAddress(string token, string csrf, Guid turn, Guid choice, Guid? request = null, HttpClient? target = null) =>
        await (target ?? client).PostAsync("/AddressPractice/CheckAnswer", new FormUrlEncodedContent(new Dictionary<string, string> {
            ["SessionToken"] = token, ["__RequestVerificationToken"] = csrf, ["TurnKey"] = turn.ToString(), ["ChoiceKey"] = choice.ToString(),
            ["RequestId"] = (request ?? Guid.NewGuid()).ToString(), ["Score"] = "99999", ["UserId"] = secondUserId
        }));
    async Task<HttpResponseMessage> FinishAddress(string token, string csrf) => await client.PostAsync("/AddressPractice/Finish",
        new FormUrlEncodedContent(new Dictionary<string, string> { ["SessionToken"] = token, ["__RequestVerificationToken"] = csrf }));
    string AddressExpected(ConversationTurnViewModel turn)
    {
        var man = turn.Situation.Contains("Fråga vad personen heter") ? "menyu shimka"
            : turn.Situation.Contains("Fråga hur personen mår") ? "kemey aleka"
            : turn.Situation.Contains("Fråga var personen kommer") ? "kabey adi metsika"
            : turn.Situation.Contains("morgonhälsning") ? "kemey hadirka"
            : turn.Situation.Contains("eftermiddagshälsning") ? "kemey wu’elka"
            : turn.Situation.Contains("kvällshälsning") ? "kemey amsika" : throw new InvalidOperationException("Oväntad testscen");
        return turn.Situation.StartsWith("Du pratar med en kvinna.") ? PhraseUsageGuide.For(man)!.OtherForm! : man;
    }
    async Task CompleteAddress(string token, string csrf, ConversationBoardViewModel board, int start = 0)
    {
        foreach (var turn in board.Turns.Skip(start))
        {
            var right = turn.Choices.Single(choice => PhraseUsageGuide.SameForm(choice.Text, AddressExpected(turn)));
            var response = await AnswerAddress(token, csrf, turn.Key, right.Key);
            var feedback = System.Text.Json.JsonSerializer.Deserialize<ConversationFeedback>(await response.Content.ReadAsStringAsync(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert(response.StatusCode == HttpStatusCode.OK && feedback!.IsCorrect && feedback.Explanation!.Contains("Du talar till en")
                && feedback.Explanation.Contains(PhraseUsageGuide.For(right.Text)!.OtherForm!),
                "Tilltalsövningen rättar på servern och förklarar båda formerna efter svaret");
        }
    }
    await AddressLessons(1, 2, 4);
    var address = await OpenAddress();
    Assert(address.Token.Length > 0 && WebUtility.HtmlDecode(address.Html).Contains("0 träningspoäng") && address.Board.Turns.Count == 6
        && address.Board.Turns.Count(turn => turn.Situation.StartsWith("Du pratar med en kvinna.")) == 3
        && address.Board.Turns.All(turn => turn.Choices.Count == 4 && turn.Choices.All(choice => choice.UsageLabel is null)
            && turn.Choices.Select(choice => PhraseUsageGuide.FormKey(choice.Text)).Distinct().Count() == 4)
        && !address.Html.Contains("CorrectChoiceKey") && !address.Html.Contains("QuestionId") && !address.Html.Contains("IsCorrect")
        && !address.Html.Contains("<audio") && !Regex.IsMatch(address.Html, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"),
        "Tre kända fraspar ger sex blandade situationer med fyra latinska alternativ utan facitmarkeringar");
    var firstAddressTurn = address.Board.Turns[0];
    var rightAddress = firstAddressTurn.Choices.Single(choice => PhraseUsageGuide.SameForm(choice.Text, AddressExpected(firstAddressTurn)));
    var wrongAddress = firstAddressTurn.Choices.Single(choice => PhraseUsageGuide.SameForm(choice.Text, PhraseUsageGuide.For(rightAddress.Text)!.OtherForm!));
    Assert((await FinishAddress(address.Token, address.Csrf)).StatusCode == HttpStatusCode.BadRequest
        && (await FinishAddress("tampered", address.Csrf)).StatusCode == HttpStatusCode.BadRequest,
        "En ofärdig eller manipulerad tilltalsomgång kan inte avslutas");
    Assert((await AnswerAddress(address.Token, "", firstAddressTurn.Key, rightAddress.Key)).StatusCode == HttpStatusCode.BadRequest
        && (await AnswerAddress(address.Token, await Token("/", secondClient), firstAddressTurn.Key, rightAddress.Key, target: secondClient)).StatusCode == HttpStatusCode.Conflict
        && (await AnswerAddress(address.Token + "x", address.Csrf, firstAddressTurn.Key, rightAddress.Key)).StatusCode == HttpStatusCode.Conflict
        && (await AnswerConversation(address.Token, address.Csrf, firstAddressTurn.Key, rightAddress.Key)).StatusCode == HttpStatusCode.Conflict,
        "Tilltalsrättningen kräver antiforgery, eget konto och rätt typ av skyddad session");
    Assert((await AnswerAddress(address.Token, address.Csrf, address.Board.Turns[1].Key, rightAddress.Key)).StatusCode == HttpStatusCode.BadRequest
        && (await AnswerAddress(address.Token, address.Csrf, firstAddressTurn.Key, Guid.NewGuid())).StatusCode == HttpStatusCode.BadRequest,
        "Tilltalssituationer kan inte hoppas över och främmande alternativ avvisas");
    var beforeAddress = await GameDatabaseSnapshot(); var addressXp = await GameDatabaseSnapshot(false);
    var requestAddress = Guid.NewGuid();
    var wrongAddressResponse = await AnswerAddress(address.Token, address.Csrf, firstAddressTurn.Key, wrongAddress.Key, requestAddress);
    var wrongAddressJson = await wrongAddressResponse.Content.ReadAsStringAsync();
    var wrongAddressFeedback = System.Text.Json.JsonSerializer.Deserialize<ConversationFeedback>(wrongAddressJson, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    Assert(!wrongAddressFeedback.IsCorrect && wrongAddressFeedback.Points == 0 && wrongAddressFeedback.CorrectText == rightAddress.Text
        && wrongAddressFeedback.Explanation!.Contains(rightAddress.Text) && wrongAddressFeedback.Explanation.Contains(wrongAddress.Text)
        && await (await AnswerAddress(address.Token, address.Csrf, firstAddressTurn.Key, wrongAddress.Key, requestAddress)).Content.ReadAsStringAsync() == wrongAddressJson
        && (await AnswerAddress(address.Token, address.Csrf, firstAddressTurn.Key, rightAddress.Key, requestAddress)).StatusCode == HttpStatusCode.BadRequest,
        "Fel tilltalsform ger rätt förklaring och samma svar kan skickas om utan poängmanipulation");
    await CompleteAddress(address.Token, address.Csrf, address.Board, 1);
    Assert(await GameDatabaseSnapshot() == beforeAddress, "Tilltalsövningen sparar inga ofärdiga resultat");
    Assert((await FinishAddress(address.Token, address.Csrf)).StatusCode == HttpStatusCode.Redirect,
        "En klar tilltalsomgång kan sparas");
    var addressResult = WebUtility.HtmlDecode(await client.GetStringAsync("/AddressPractice/Result"));
    Assert(addressResult.Contains("Bra jobbat med tilltalet!") && addressResult.Contains("5 av 6") && addressResult.Contains("500")
        && addressResult.Contains("2 av 3 stjärnor") && addressResult.Contains("Du talar till en") && await GameDatabaseSnapshot(false) == addressXp,
        "Tilltalsresultatet visar poäng, förklaringar och stjärnor utan att ändra XP eller lektionsresultat");
    using (var db = new ApplicationDbContext(options))
    {
        var failed = await db.Questions.Where(question => question.LessonId == 2 || question.LessonId == 4).SingleAsync(question => question.LatinText == rightAddress.Text);
        Assert((await db.QuestionReviewStates.SingleAsync(state => state.UserId == firstUserId && state.QuestionId == failed.Id)).NeedsReview,
            "Den missade tilltalsformen ligger kvar i repetitionen");
    }
    var afterAddress = await GameDatabaseSnapshot();
    await FinishAddress(address.Token, address.Csrf); await client.GetAsync("/AddressPractice/Result");
    Assert(await GameDatabaseSnapshot() == afterAddress, "Dubbelt avslut skriver inte tilltalsresultatet igen");
    var editedAddress = await OpenAddress();
    await CompleteAddress(editedAddress.Token, editedAddress.Csrf, editedAddress.Board);
    using (var db = new ApplicationDbContext(options))
    { (await db.Questions.SingleAsync(question => question.Id == 13)).Audience = PhraseAudience.Woman; await db.SaveChangesAsync(); }
    var editedAddressBefore = await GameDatabaseSnapshot();
    await FinishAddress(editedAddress.Token, editedAddress.Csrf);
    Assert(await GameDatabaseSnapshot() == editedAddressBefore && WebUtility.HtmlDecode(await client.GetStringAsync("/AddressPractice/Result")).Contains("Din nyare repetition har behållits"),
        "En tilltalsändring under omgången stoppar skrivning av äldre repetitionssvar");
    using (var db = new ApplicationDbContext(options))
    { (await db.Questions.SingleAsync(question => question.Id == 13)).Audience = PhraseAudience.Automatic; await db.SaveChangesAsync(); }
    var staleAddress = await OpenAddress(); await CompleteAddress(staleAddress.Token, staleAddress.Csrf, staleAddress.Board);
    using (var db = new ApplicationDbContext(options))
    { var state = await db.QuestionReviewStates.SingleAsync(state => state.UserId == firstUserId && state.QuestionId == 6); state.LastAnsweredAt = DateTime.UtcNow.AddMinutes(1); await db.SaveChangesAsync(); }
    var staleAddressBefore = await GameDatabaseSnapshot(); await FinishAddress(staleAddress.Token, staleAddress.Csrf);
    Assert(await GameDatabaseSnapshot() == staleAddressBefore, "Ett äldre tilltalsresultat skriver inte över nyare repetition");
    await AddressLessons(1, 2);
    var shortAddress = await OpenAddress();
    Assert(shortAddress.Board.Turns.Count == 2 && shortAddress.Board.Turns.All(turn => turn.Situation.Contains("Fråga vad personen heter")),
        "Tilltalsträningen börjar redan efter presentationslektionen utan senare lektioners fraser");
    await CompleteAddress(shortAddress.Token, shortAddress.Csrf, shortAddress.Board);
    var distractorText = shortAddress.Board.Turns[0].Choices.First(choice => choice.Text != "menyu shimka" && choice.Text != "menyu shimki").Text;
    using (var db = new ApplicationDbContext(options))
    { (await db.Questions.SingleAsync(question => (question.LessonId == 1 || question.LessonId == 2) && question.LatinText == distractorText)).LatinText += " (test)"; await db.SaveChangesAsync(); }
    var distractorBefore = await GameDatabaseSnapshot(); await FinishAddress(shortAddress.Token, shortAddress.Csrf);
    Assert(await GameDatabaseSnapshot() == distractorBefore && WebUtility.HtmlDecode(await client.GetStringAsync("/AddressPractice/Result")).Contains("Din nyare repetition har behållits"),
        "Även ändrade distraktionsalternativ skyddar mot gamla tilltalsresultat");
    using (var db = new ApplicationDbContext(options))
    { (await db.Questions.SingleAsync(question => (question.LessonId == 1 || question.LessonId == 2) && question.LatinText == distractorText + " (test)")).LatinText = distractorText; await db.SaveChangesAsync(); }
    await AddressLessons(1, 5);
    var guideAddress = await OpenAddress(); var guideTurn = guideAddress.Board.Turns[0];
    var guideRight = AddressExpected(guideTurn); var guideWrong = PhraseUsageGuide.For(guideRight)!.OtherForm!;
    await AnswerAddress(guideAddress.Token, guideAddress.Csrf, guideTurn.Key, guideTurn.Choices.Single(choice => choice.Text == guideWrong).Key);
    await CompleteAddress(guideAddress.Token, guideAddress.Csrf, guideAddress.Board, 1);
    await FinishAddress(guideAddress.Token, guideAddress.Csrf);
    using (var db = new ApplicationDbContext(options))
    {
        var guideMan = PhraseUsageGuide.For(guideRight)!.Label == "(m) Till en man" ? guideRight : guideWrong;
        var source = await db.Questions.SingleAsync(question => question.LatinText == guideMan);
        Assert(guideAddress.Board.Turns.Count == 6 && guideAddress.Board.Turns.Any(turn => turn.Choices.Any(choice => choice.Text == "kemey hadirki"))
            && (await db.QuestionReviewStates.SingleAsync(state => state.UserId == firstUserId && state.QuestionId == source.Id)).NeedsReview,
            "Granskade motformer fungerar utan nya databasfraser och ett fel består även efter rätt svar på samma fraspar");
    }
    await AddressLessons(addressCompletionTimes.Keys.ToArray());
    Assert(WebUtility.HtmlDecode(await client.GetStringAsync("/Games")).Contains("Träna tilltal")
        && WebUtility.HtmlDecode(await client.GetStringAsync("/Learn")).Contains("Träna tilltal"),
        "Tilltalsträningen nås från spel och den personliga lärvägen");

    await FirstMeetingVerification.RunAsync(secondClient, Assert);
    await PlacementVerification.RunAsync(options, client, secondClient, Assert);
    await LearningPreferenceVerification.RunAsync(options, client, secondClient, firstUserId, secondUserId, Assert);
    await TenseVerification.RunAsync(options, client, secondClient, firstUserId, Assert, args.Contains("--preview"));
    var logoutToken = await Token("/");
    var logout = await client.PostAsync("/Identity/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = logoutToken
    }));
    Assert(logout.StatusCode == HttpStatusCode.Redirect
        && !(await client.GetStringAsync("/")).Contains(">TestElev</button>"), "Utloggning avslutar sessionen");
    Assert((await client.GetAsync("/Progress")).StatusCode == HttpStatusCode.Redirect,
        "Utloggning stänger åtkomsten till personliga framsteg");
    Console.WriteLine("Migrationer, quiz, personliga framsteg och kontoflöde verifierade i separat testdatabas.");
    if (args.Contains("--preview"))
    {
        Console.WriteLine("PREVIEW_URL: " + client.BaseAddress);
        Console.WriteLine("Förhandsvisningen använder tillfälliga testkonton. Tryck Enter för att stänga och städa bort testdatabasen.");
        Console.ReadLine();
    }
}
finally
{
    foreach (var path in audioFixturePaths) if (File.Exists(path)) File.Delete(path);
    if (productionApp is not null)
    {
        if (!productionApp.HasExited) productionApp.Kill(entireProcessTree: true);
        await productionApp.WaitForExitAsync();
        productionApp.Dispose();
    }
    if (app is not null)
    {
        if (!app.HasExited) app.Kill(entireProcessTree: true);
        await app.WaitForExitAsync();
        app.Dispose();
    }
    if (createdDatabase)
    {
        SqlConnection.ClearAllPools();
        using var cleanup = new ApplicationDbContext(options);
        await cleanup.Database.EnsureDeletedAsync();
        Console.WriteLine("Den tillfälliga testdatabasen har städats bort.");
    }
}


