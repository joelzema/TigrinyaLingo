using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;
using TigrinyaLingo.Services;

namespace TigrinyaLingo.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DailyTrainingController(ApplicationDbContext context, QuizProgressStore progress, IDataProtectionProvider protection) : Controller
{
    private readonly IDataProtector protector = protection.CreateProtector("TigrinyaLingo.DailyTraining.v1");
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private Task<List<Lesson>> LessonsAsync() => context.Lessons.AsNoTracking()
        .Where(lesson => context.UserProgresses.Any(saved => saved.UserId == UserId && saved.LessonId == lesson.Id))
        .Include(lesson => lesson.Questions).ThenInclude(question => question.Choices).ToListAsync();

    private Task<Dictionary<int, QuestionReviewState>> StatesAsync(IEnumerable<int> ids) => context.QuestionReviewStates.AsNoTracking()
        .Where(state => state.UserId == UserId && ids.Contains(state.QuestionId)).ToDictionaryAsync(state => state.QuestionId);

    public async Task<IActionResult> Index()
    {
        var lessons = (await LessonsAsync()).Where(lesson => QuizController.BuildQuiz(lesson, isReview: true).IsAvailable).ToList();
        var questions = lessons.SelectMany(lesson => lesson.Questions).ToList();
        var states = await StatesAsync(questions.Select(question => question.Id).ToList());
        var today = DateTime.UtcNow;
        // Fel prioriteras först, därefter tiden sedan senaste svar. Daglig, stabil brytning av lika värden.
        var selected = questions.OrderByDescending(question => states.GetValueOrDefault(question.Id)?.NeedsReview ?? false)
            .ThenBy(question => states.GetValueOrDefault(question.Id)?.LastAnsweredAt ?? DateTime.MinValue)
            .ThenBy(question => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{today:yyyy-MM-dd}:{UserId}:{question.Id}"))), StringComparer.Ordinal)
            .Take(10).ToList();
        var models = lessons.SelectMany(lesson => QuizController.BuildQuiz(lesson, isReview: true).Questions).ToDictionary(question => question.Id);
        var session = new DailySession(UserId, today, selected.Select(question => new DailyQuestionSelection(
            question.Id, models[question.Id].Kind, states.GetValueOrDefault(question.Id)?.LastAnsweredAt.Ticks)).ToList());
        var model = new QuizViewModel
        {
            IsDaily = true, IsReview = true, LessonTitle = "Dagens träning", IsAvailable = selected.Count > 0,
            Questions = selected.Select(question => models[question.Id]).ToList(),
            SessionToken = protector.Protect(JsonSerializer.Serialize(session)),
            StatusMessage = TempData["DailyMessage"] as string
        };
        return View("~/Views/Quiz/Index.cshtml", model);
    }

    private DailySession? ReadSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16000) return null;
        try
        {
            var session = JsonSerializer.Deserialize<DailySession>(protector.Unprotect(token));
            return session is not null && session.UserId == UserId && session.IssuedAt <= DateTime.UtcNow
                && session.IssuedAt >= DateTime.UtcNow.AddHours(-2) && session.Questions is { Count: > 0 and <= 10 }
                && session.Questions.All(question => question is not null)
                && session.Questions.Select(question => question.QuestionId).Distinct().Count() == session.Questions.Count
                ? session : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException) { return null; }
    }

    private static bool ValidAnswer(DailySession session, List<Lesson> lessons, QuizAnswerInput answer)
    {
        var selection = session.Questions.SingleOrDefault(question => question.QuestionId == answer.QuestionId);
        var lesson = lessons.SingleOrDefault(lesson => lesson.Questions.Any(question => question.Id == answer.QuestionId));
        if (selection is null || lesson is null || selection.Kind != answer.Kind || answer.ChoiceId is null) return false;
        var question = lesson.Questions.Single(question => question.Id == answer.QuestionId);
        var model = QuizController.BuildQuiz(lesson, isReview: true);
        return model.IsAvailable && model.Questions.Single(item => item.Id == question.Id).Kind == selection.Kind
            && question.Choices.Any(choice => choice.Id == answer.ChoiceId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckAnswer(string sessionToken, QuizAnswerInput input)
    {
        var session = ReadSession(sessionToken);
        var lessons = await LessonsAsync();
        if (!ModelState.IsValid || session is null || !ValidAnswer(session, lessons, input)) return BadRequest();
        var question = lessons.SelectMany(lesson => lesson.Questions).Single(question => question.Id == input.QuestionId);
        var correct = question.Choices.Single(choice => choice.IsCorrect);
        return Json(new { isCorrect = correct.Id == input.ChoiceId, correctChoiceId = correct.Id,
            correctAnswer = input.Kind == ExerciseKind.Translate ? question.LatinText : correct.Text, explanation = TenseGuide.Explanation(question) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(DailySubmissionInput input)
    {
        var session = ReadSession(input.SessionToken);
        if (session is null) return BadRequest("Övningen har gått ut eller är ogiltig. Öppna Dagens träning igen.");
        var lessons = await LessonsAsync();
        if (!ModelState.IsValid || input.Answers is null || input.Answers.Any(answer => answer is null)
            || input.Answers.Count != session.Questions.Count
            || input.Answers.Select(answer => answer.QuestionId).Distinct().Count() != input.Answers.Count
            || input.Answers.Any(answer => !ValidAnswer(session, lessons, answer)))
            return BadRequest("Svara på alla frågor i den valda övningen.");
        var ids = session.Questions.Select(question => question.QuestionId).ToHashSet();
        var affected = lessons.Where(lesson => lesson.Questions.Any(question => ids.Contains(question.Id))).OrderBy(lesson => lesson.Id).ToList();
        await using var transaction = await progress.BeginLessonsUpdateAsync(UserId, affected.Select(lesson => lesson.Id));
        var states = await StatesAsync(ids);
        // Ett gammalt formulär eller en dubbel inskickning får inte skriva över nyare svar.
        if (session.Questions.Any(question => states.GetValueOrDefault(question.QuestionId)?.LastAnsweredAt.Ticks != question.LastAnsweredTicks))
        {
            TempData["DailyMessage"] = "Dina svar har ändrats i en annan övning, eller så är den här träningen redan sparad. Här är en uppdaterad träning.";
            return RedirectToAction(nameof(Index));
        }
        foreach (var lesson in affected)
            await progress.SaveAnswersAsync(UserId, lesson, input.Answers.Where(answer => lesson.Questions.Any(question => question.Id == answer.QuestionId)).ToList(), fullQuiz: false);
        await transaction.CommitAsync();
        TempData["DailyAttempt"] = JsonSerializer.Serialize(new DailyAttempt(UserId, input.Answers));
        return RedirectToAction(nameof(Result));
    }

    public async Task<IActionResult> Result()
    {
        var model = new DailyTrainingResultViewModel
        {
            PendingCount = await context.QuestionReviewStates.CountAsync(state => state.UserId == UserId && state.NeedsReview)
        };
        if (TempData["DailyAttempt"] is string serialized)
        {
            try
            {
                var attempt = JsonSerializer.Deserialize<DailyAttempt>(serialized);
                if (attempt?.UserId == UserId && attempt.Answers is { Count: > 0 and <= 10 }
                    && attempt.Answers.All(answer => answer is not null)
                    && attempt.Answers.Select(answer => answer.QuestionId).Distinct().Count() == attempt.Answers.Count)
                {
                    var lessons = await LessonsAsync();
                    foreach (var answer in attempt.Answers)
                    {
                        var lesson = lessons.SingleOrDefault(lesson => lesson.Questions.Any(question => question.Id == answer.QuestionId));
                        var question = lesson?.Questions.Single(question => question.Id == answer.QuestionId);
                        var choices = lesson is null ? null : QuizController.BuildQuiz(lesson, isReview: true).Questions.Single(item => item.Id == answer.QuestionId).Choices;
                        if (question is null || !TenseGuide.IsPublished(question) || choices is null || !question.Choices.Any(choice => choice.Id == answer.ChoiceId)) continue;
                        var correct = question.Choices.Single(choice => choice.IsCorrect);
                        model.Answers.Add(new DailyAnswerResult
                        {
                            LessonId = lesson!.Id, LessonTitle = lesson.Title, LatinText = question.LatinText, Audience = question.Audience, TenseHint = TenseGuide.For(question), Meaning = correct.Text,
                            IsCorrect = correct.Id == answer.ChoiceId, IsLatinAnswer = answer.Kind == ExerciseKind.Translate,
                            SelectedAnswer = choices.Single(choice => choice.Id == answer.ChoiceId).Text,
                            CorrectAnswer = choices.Single(choice => choice.Id == correct.Id).Text
                        });
                    }
                }
            }
            catch (JsonException) { /* Sparade repetitionsord kan visas även utan försöksdata. */ }
        }
        return View(model);
    }
}
