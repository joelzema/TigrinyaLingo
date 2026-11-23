using System.Security.Claims;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Controllers
{
    [Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public class ProgressController : Controller
    {
        private readonly ApplicationDbContext _context;
        public ProgressController(ApplicationDbContext context) { _context = context; }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Challenge();
            var lessons = await _context.UserProgresses.AsNoTracking()
                .Where(progress => progress.UserId == userId)
                .OrderByDescending(progress => progress.CompletedAt).ThenBy(progress => progress.Lesson!.Title)
                .Select(progress => new LessonProgressViewModel
                {
                    LessonId = progress.LessonId, LessonTitle = progress.Lesson!.Title,
                    CourseTitle = progress.Lesson.Course!.Title, QuestionCount = progress.Lesson.Questions.Count,
                    BestScore = progress.Score, IsCompleted = progress.CompletedAt.HasValue,
                    PendingReviewCount = _context.QuestionReviewStates.Count(state => state.UserId == userId
                        && state.Question!.LessonId == progress.LessonId && state.NeedsReview)
                }).ToListAsync();
            return View(new ProgressViewModel { Lessons = lessons });
        }
    }
}
