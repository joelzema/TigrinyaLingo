using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TigrinyaLingo.Models;
using TigrinyaLingo.Services;

namespace TigrinyaLingo.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AddressPracticeController(ApplicationDbContext context, AddressPracticeStore content,
    QuizProgressStore progress, IDataProtectionProvider protection, IMemoryCache cache) : Controller
{
    private readonly IDataProtector protector = protection.CreateProtector("TigrinyaLingo.AddressPractice.v1");
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static string CacheKey(Guid id) => "TigrinyaLingo:AddressPractice:" + id.ToString("N");

    public async Task<IActionResult> Index() => View(new AddressPracticeHomeViewModel(
        (await content.LoadAsync(UserId)).Pairs.Count, TempData["AddressPracticeMessage"] as string));

    [HttpGet]
    public async Task<IActionResult> Play()
    {
        var session = await content.CreateAsync(UserId);
        if (session is null)
        {
            TempData["AddressPracticeMessage"] = "Gör först klart en lektion med tilltalsformer, till exempel Presentera dig. Då får du träna både manlig och kvinnlig form här.";
            return RedirectToAction(nameof(Index));
        }
        cache.Set(CacheKey(session.Id), session, TimeSpan.FromHours(1));
        return View("~/Views/Conversations/Play.cshtml", new ConversationPlayViewModel {
            IsAddressPractice = true,
            Scene = new ConversationSceneCard(session.SceneId, session.Title, "Välj rätt form efter vem du talar till. Du får träna båda formerna i din egen takt.",
                "Dina klara lektioner", 0, session.Turns.Count, true, true, null),
            SessionToken = protector.Protect(JsonSerializer.Serialize(new ConversationToken(session.Id, UserId, session.IssuedAt))),
            Board = new ConversationBoardViewModel(session.Title, session.Turns.Select(turn => new ConversationTurnViewModel(
                turn.Key, turn.Situation, turn.PartnerLine, turn.Choices)).ToList())
        });
    }

    private ConversationSession? Session(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16000) return null;
        try
        {
            var parsed = JsonSerializer.Deserialize<ConversationToken>(protector.Unprotect(token));
            return parsed?.UserId == UserId && parsed.IssuedAt >= DateTime.UtcNow.AddHours(-1) && parsed.IssuedAt <= DateTime.UtcNow
                && cache.TryGetValue<ConversationSession>(CacheKey(parsed.SessionId), out var state) && state?.UserId == UserId ? state : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException) { return null; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckAnswer(ConversationAnswerInput input)
    {
        if (!ModelState.IsValid || input.RequestId == Guid.Empty || input.TurnKey == Guid.Empty || input.ChoiceKey == Guid.Empty) return BadRequest();
        var state = Session(input.SessionToken);
        if (state is null) return Conflict(new { message = "Tilltalsomgången har gått ut. Starta en ny tilltalsomgång." });
        await state.Gate.WaitAsync();
        try
        {
            var previous = state.Answers.SingleOrDefault(answer => answer.RequestId == input.RequestId);
            if (previous is not null)
                return previous.TurnKey == input.TurnKey && previous.ChoiceKey == input.ChoiceKey ? Json(previous.Feedback) : BadRequest();
            if (state.Finalized || state.Complete) return BadRequest();
            var turn = state.Turns[state.Answers.Count];
            var choice = turn.Choices.SingleOrDefault(choice => choice.Key == input.ChoiceKey);
            if (turn.Key != input.TurnKey || choice is null) return BadRequest();
            var correct = input.ChoiceKey == turn.CorrectChoiceKey;
            var count = state.CorrectCount + (correct ? 1 : 0);
            var feedback = new ConversationFeedback(correct, turn.CorrectChoiceKey, turn.LatinText, turn.Meaning,
                count * 100, count, state.Answers.Count + 1, state.Turns.Count, state.Answers.Count + 1 == state.Turns.Count, PhraseUsageGuide.For(turn.LatinText, turn.Audience)?.Label, turn.Explanation);
            state.Answers.Add(new ConversationAnswerMemo(input.RequestId, input.TurnKey, input.ChoiceKey, choice.Text, correct, feedback, choice.UsageLabel));
            return Json(feedback);
        }
        finally { state.Gate.Release(); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish(string sessionToken)
    {
        var state = Session(sessionToken);
        if (state is null) return BadRequest("Tilltalsomgången har gått ut. Starta en ny tilltalsomgång.");
        await state.Gate.WaitAsync();
        try
        {
            if (!state.Complete) return BadRequest("Svara på hela omgången innan du avslutar.");
            if (!state.Finalized)
            {
                await using var transaction = await progress.BeginLessonsUpdateAsync(UserId, state.Sources.Select(source => source.LessonId));
                foreach (var id in state.Sources.Select(source => source.QuestionId).Order())
                {
                    var resource = "TigrinyaLingo:ContentEditing:Question:" + id;
                    await context.Database.ExecuteSqlInterpolatedAsync($"""
                        DECLARE @result int;
                        EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Shared', @LockOwner = 'Transaction', @LockTimeout = 10000;
                        IF @result < 0 THROW 50001, N'Frasen används just nu. Försök igen.', 1;
                        """);
                }
                var ids = state.Sources.Select(source => source.QuestionId).ToList();
                var questions = await context.Questions.AsNoTracking().Include(question => question.Choices)
                    .Where(question => ids.Contains(question.Id) && context.UserProgresses.Any(saved => saved.UserId == UserId
                        && saved.LessonId == question.LessonId && saved.CompletedAt != null)).ToDictionaryAsync(question => question.Id);
                var states = await context.QuestionReviewStates.AsNoTracking().Where(item => item.UserId == UserId && ids.Contains(item.QuestionId))
                    .ToDictionaryAsync(item => item.QuestionId);
                state.Outdated = state.Sources.Any(source => !questions.TryGetValue(source.QuestionId, out var question)
                    || ConversationContentStore.Fingerprint(question) != source.Fingerprint
                    || (source.Review && states.GetValueOrDefault(source.QuestionId)?.LastAnsweredAt.Ticks != source.LastAnsweredTicks));
                if (!state.Outdated)
                {
                    await progress.SaveReviewOutcomesAsync(UserId, state.Turns.GroupBy(turn => turn.QuestionId).ToDictionary(group => group.Key,
                        group => group.All(turn => state.Answers.Single(answer => answer.TurnKey == turn.Key).IsCorrect)));
                    await transaction.CommitAsync();
                    state.ReviewSaved = true;
                }
                state.Finalized = true;
            }
            TempData["AddressPracticeResultToken"] = sessionToken;
            return RedirectToAction(nameof(Result));
        }
        finally { state.Gate.Release(); }
    }

    public IActionResult Result()
    {
        var state = Session(TempData.Peek("AddressPracticeResultToken") as string ?? string.Empty);
        if (state is null || !state.Finalized)
        {
            TempData["AddressPracticeMessage"] = "Resultatet är inte tillgängligt längre. Starta en ny tilltalsomgång.";
            return RedirectToAction(nameof(Index));
        }
        return View("~/Views/Conversations/Result.cshtml", new ConversationResultViewModel {
            IsAddressPractice = true,
            SceneId = state.SceneId, Title = state.Title, Points = state.Points, CorrectCount = state.CorrectCount, TotalCount = state.Turns.Count,
            Stars = state.CorrectCount == state.Turns.Count ? 3 : state.CorrectCount * 4 >= state.Turns.Count * 3 ? 2 : 1,
            ReviewSaved = state.ReviewSaved, Outdated = state.Outdated,
            Turns = state.Turns.Select(turn => {
                var answer = state.Answers.Single(answer => answer.TurnKey == turn.Key);
                return new ConversationResultTurn(turn.Situation, turn.PartnerLine, answer.Answer, turn.LatinText, turn.Meaning, answer.IsCorrect, turn.Audience, answer.UsageLabel, turn.Explanation);
            }).ToList()
        });
    }
}
