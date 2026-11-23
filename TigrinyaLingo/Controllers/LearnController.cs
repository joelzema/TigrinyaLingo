using System.Security.Claims;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;
using TigrinyaLingo.Services;

namespace TigrinyaLingo.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class LearnController(ApplicationDbContext context, LearningPathService path, TenseLearningStore tenses, LearningPreferenceService preferences) : Controller
{
    public async Task<IActionResult> Index()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var preference = await preferences.FindAsync(userId);
        if (preference is null) return RedirectToAction("Index", "LearningPreferences", new { returnUrl = "/Learn" });
        return View(new LearnViewModel {
            Experience = preference.Experience,
            Courses = await path.CoursesAsync(userId),
            TensePractice = (await tenses.ModelsAsync(userId, false)).Where(group => group.CanPractice).ToList(),
            PendingReviewCount = await context.QuestionReviewStates.CountAsync(state => state.UserId == userId && state.NeedsReview),
            TotalXp = await context.UserProgresses.Where(saved => saved.UserId == userId)
                .Select(saved => saved.Score * LearningRewards.CorrectAnswerXp + (saved.CompletedAt != null ? LearningRewards.CompletionBonus : 0)).SumAsync()
        });
    }
}
