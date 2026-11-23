using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Services;

internal static class LearningPreferenceVerification
{
    public static async Task RunAsync(DbContextOptions<ApplicationDbContext> options, HttpClient client, HttpClient other,
        string userId, string otherId, Action<bool,string> check)
    {
        async Task<string> Html(string path) => WebUtility.HtmlDecode(await client.GetStringAsync(path));
        async Task<string> Token()
        {
            var page = await Html("/LearningPreferences");
            return Regex.Match(Regex.Match(page,"<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value,"value=\"([^\"]*)\"").Groups[1].Value;
        }
        async Task<HttpResponseMessage> Save(string experience, string? returnUrl = null, bool csrf = true) =>
            await client.PostAsync("/LearningPreferences", new FormUrlEncodedContent(new Dictionary<string,string> {
                ["Experience"] = experience, ["ReturnUrl"] = returnUrl ?? "/Learn", ["UserId"] = otherId,
                ["__RequestVerificationToken"] = csrf ? await Token() : ""
            }));
        async Task<string> Snapshot()
        {
            using var db = new ApplicationDbContext(options);
            return JsonSerializer.Serialize(await db.UserProgresses.AsNoTracking().OrderBy(row=>row.Id).ToListAsync())
                + JsonSerializer.Serialize(await db.QuestionReviewStates.AsNoTracking().OrderBy(row=>row.Id).ToListAsync());
        }
        var before = await Snapshot();
        check((await Save("2", csrf:false)).StatusCode == HttpStatusCode.BadRequest, "Förkunskapsval skyddas av antiforgery");
        foreach(var invalid in new[]{"", "0", "999", "ogiltigt"})
            check((await Save(invalid)).StatusCode == HttpStatusCode.OK, "Saknade och okända förkunskapsval avvisas med formulärhjälp");
        var unsafePage = await Html("/LearningPreferences?returnUrl=https%3A%2F%2Fexample.invalid");
        var returnField = Regex.Match(unsafePage,"<input[^>]*name=\"ReturnUrl\"[^>]*>").Value;
        check(Regex.Match(returnField,"value=\"([^\"]*)\"").Groups[1].Value == "/Learn"
            && !unsafePage.Contains("https://example.invalid"), "Förkunskapsformuläret använder en säker lokal returadress");
        foreach(var experience in new[]{"2", "3", "1"})
        {
            var response = await Save(experience,"https://example.invalid");
            using var db = new ApplicationDbContext(options);
            check(response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location?.ToString() == "/Learn"
                && (int)(await db.UserLearningPreferences.SingleAsync(row=>row.UserId==userId)).Experience == int.Parse(experience)
                && (await db.UserLearningPreferences.SingleAsync(row=>row.UserId==otherId)).Experience == LearningExperience.Beginner,
                "Ändrat val sparas bara för eget konto och externa returadresser avvisas");
            check((await Html("/Learn")).Contains("Ändra förkunskaper"), "Förkunskaper går att ändra från lärvägen");
        }
        check((await client.GetAsync("/Learn")).IsSuccessStatusCode && (await client.GetAsync("/Quiz/Index/2")).IsSuccessStatusCode,
            "Ett sparat val kräver inte nytt förkunskapsformulär vid varje besök");
        check(await Snapshot() == before, "Förkunskapsändringar bevarar alla poäng, klar-statusar och repetitionssvar");

        // Separat konto utan resultat provar rekommendationerna utan att röra de två elevernas historik.
        var fixtureId = Guid.NewGuid().ToString();
        using(var db = new ApplicationDbContext(options))
        {
            db.Users.Add(new IdentityUser { Id=fixtureId, UserName="preference-fixture" });
            var preference = new UserLearningPreference { UserId=fixtureId, Experience=LearningExperience.Beginner };
            db.UserLearningPreferences.Add(preference); await db.SaveChangesAsync();
            foreach(var (experience, start) in new[]{(LearningExperience.Beginner,1),(LearningExperience.SomeUnderstanding,4),(LearningExperience.Familiar,14)})
            {
                preference.Experience=experience; await db.SaveChangesAsync();
                var course=(await new LearningPathService(db).CoursesAsync(fixtureId,1)).Single();
                check(course.NextLesson?.Order==start && course.Lessons.All(lesson=>!lesson.IsLocked && !lesson.IsCompleted && lesson.BestScore==null),
                    "Självskattning ändrar rekommenderad start men öppnar alla färdiga lektioner utan påhittade resultat");
            }
            check(!await db.UserProgresses.AnyAsync(row=>row.UserId==fixtureId), "Rekommendationer ger aldrig automatiska framsteg");
            db.Users.Remove(await db.Users.SingleAsync(row=>row.Id==fixtureId)); await db.SaveChangesAsync();
        }

        // Ett faktiskt senare quiz ska kunna klaras även om tidigare lektion är orörd.
        using(var db = new ApplicationDbContext(options))
        {
            var course=new Course { Title="Verifiering av fri lektionsordning", Description="Tillfällig testkurs", Category="Test" };
            db.Courses.Add(course); await db.SaveChangesAsync();
            Lesson Lesson(string title,int order) => new() { CourseId=course.Id, Title=title, Order=order,
                Questions=new List<Question>{new() {LatinText="selam", Choices=new List<Choice>{new(){Text="Hej",IsCorrect=true},new(){Text="Tack"},new(){Text="Nej"},new(){Text="Ja"}}}} };
            var first=Lesson("Tidigare testlektion",1); var later=Lesson("Fritt vald senare testlektion",2);
            db.Lessons.AddRange(first,later); await db.SaveChangesAsync();
            var q=later.Questions.Single();
            var response=await client.PostAsync("/Quiz/Submit/"+later.Id,new FormUrlEncodedContent(new Dictionary<string,string>{
                ["__RequestVerificationToken"]=await Token(),["Answers[0].QuestionId"]=q.Id.ToString(),
                ["Answers[0].ChoiceId"]=q.Choices.Single(c=>c.IsCorrect).Id.ToString(),["Answers[0].Kind"]="Read",["Score"]="999",["UserId"]=otherId
            }));
            check(response.StatusCode==HttpStatusCode.Redirect && await db.UserProgresses.AnyAsync(row=>row.UserId==userId && row.LessonId==later.Id && row.CompletedAt!=null && row.Score==1)
                && !await db.UserProgresses.AnyAsync(row=>row.LessonId==first.Id || (row.LessonId==later.Id && row.UserId==otherId)),
                "Ett senare quiz kan sparas med riktig rättning utan tidigare lektion eller förfalskade poäng och konto-ID");
            await db.QuestionReviewStates.Where(row=>row.Question!.Lesson!.CourseId==course.Id).ExecuteDeleteAsync();
            await db.UserProgresses.Where(row=>row.Lesson!.CourseId==course.Id).ExecuteDeleteAsync();
            db.Courses.Remove(course); await db.SaveChangesAsync();
        }
        check(await Snapshot()==before,"Temporärt quiz lämnar elevhistoriken oförändrad efter teststädning");
    }
}
