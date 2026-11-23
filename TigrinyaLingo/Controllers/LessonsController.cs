using Infrastructure.Data;
using System.Security.Claims;
using TigrinyaLingo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Controllers
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class LessonsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LearningPathService _path;
        private readonly LearningPreferenceService _preferences;
        public LessonsController(ApplicationDbContext context, LearningPathService path, LearningPreferenceService preferences) { _context = context; _path = path; _preferences = preferences; }

        public async Task<IActionResult> Details(int id)
        {
            LessonListItemViewModel? access = null;
            // Alla färdiga lektionsquiz är öppna. Fråga om startpunkt första gången.
            // Besökare kan fortfarande läsa ord utan konto.
            if (User.Identity?.IsAuthenticated == true)
            {
                var session = await _context.Lessons.AsNoTracking()
                    .Include(lesson => lesson.Questions).ThenInclude(question => question.Choices)
                    .SingleOrDefaultAsync(lesson => lesson.Id == id);
                if (session is null) return NotFound();
                if (await _preferences.FindAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!) is null)
                    return RedirectToAction("Index", "LearningPreferences", new { returnUrl = Url.Action("Details", "Lessons", new { id }) });
                access = await _path.LessonAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!, session.CourseId, id);
                if (access?.IsLocked != true)
                    return View("~/Views/Quiz/Index.cshtml", QuizController.BuildQuiz(session));
            }
            var lesson = await _context.Lessons.AsNoTracking()
                .Where(lesson => lesson.Id == id)
                .Select(lesson => new LessonDetailsViewModel
                {
                    Id = lesson.Id, Title = lesson.Title, CourseId = lesson.CourseId,
                    CourseTitle = lesson.Course!.Title,
                    Words = lesson.Questions.Where(question => (question.TenseGroup == null && question.TimeReference == Domain.Entities.TimeReference.None) || question.TenseReviewed).OrderBy(question => question.Id)
                        .Select(question => new StudyWordViewModel
                        {
                            LatinText = question.LatinText, Audience = question.Audience,
                            AudioFileName = question.AudioFileName,
                            TenseHint = question.TenseReviewed ? new TenseHint(question.TimeReference == Domain.Entities.TimeReference.Present ? "Nutid" : question.TimeReference == Domain.Entities.TimeReference.Past ? "Dåtid" : "Framtid", question.TenseContext ?? "", question.TenseNotes) : null,
                            Meaning = question.Choices.Where(choice => choice.IsCorrect)
                                .OrderBy(choice => choice.Id).Select(choice => choice.Text)
                                .FirstOrDefault() ?? "Översättning saknas"
                        }).ToList()
                }).SingleOrDefaultAsync();
            if (lesson is null) return NotFound();
            if (access?.IsLocked == true)
            {
                lesson.IsQuizLocked = true;
                lesson.RequiredLessonId = access.RequiredLessonId;
                lesson.RequiredLessonTitle = access.RequiredLessonTitle;
            }
            return View(lesson);
        }
    }
}
