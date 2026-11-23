using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Entities;
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
public class GamesController(ApplicationDbContext context, QuizProgressStore progress, IDataProtectionProvider protection,
    IMemoryCache cache, IWebHostEnvironment environment) : Controller
{
    private readonly IDataProtector protector = protection.CreateProtector("TigrinyaLingo.MatchGame.v1");
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static string CacheKey(Guid id) => "TigrinyaLingo:MatchGame:" + id.ToString("N");
    private static string Canonical(string text) => Regex.Replace(text.Normalize(), @"\s+", " ").Trim().TrimEnd('.', '!', '?', '…');
    private static string Fingerprint(Question question) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { question.Id, question.LessonId, question.LatinText, question.Audience, question.AudioFileName, question.TenseGroup, question.TimeReference, question.TenseContext, question.TenseNotes, question.TenseReviewed,
            Choices = question.Choices.OrderBy(choice => choice.Id).Select(choice => new { choice.Id, choice.Text, choice.IsCorrect }) }))));

    private async Task<List<(Question Question, QuestionReviewState? Review)>> CandidatesAsync()
    {
        var questions = await context.Questions.AsNoTracking().Include(question => question.Choices)
            .Where(question => context.UserProgresses.Any(saved => saved.UserId == UserId && saved.LessonId == question.LessonId)).ToListAsync();
        var states = await context.QuestionReviewStates.AsNoTracking().Where(state => state.UserId == UserId)
            .ToDictionaryAsync(state => state.QuestionId);
        var meanings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var selected = new List<(Question, QuestionReviewState?)>();
        foreach (var question in questions.Where(question => TenseGuide.IsPublished(question) && question.Choices.Count == 4
            && question.Choices.Count(choice => choice.IsCorrect) == 1 && !string.IsNullOrWhiteSpace(question.LatinText))
            .OrderByDescending(question => states.GetValueOrDefault(question.Id)?.NeedsReview ?? false)
            .ThenBy(question => states.GetValueOrDefault(question.Id)?.LastAnsweredAt ?? DateTime.MinValue)
            .ThenBy(_ => Random.Shared.Next()))
        {
            var meaning = Canonical(question.Choices.Single(choice => choice.IsCorrect).Text);
            var word = Canonical(question.LatinText);
            // Två likvärdiga tilltalsformer får inte bli konkurrerande rätta par.
            if (string.IsNullOrWhiteSpace(meaning) || meanings.Contains(meaning) || words.Contains(word)) continue;
            meanings.Add(meaning); words.Add(word);
            selected.Add((question, states.GetValueOrDefault(question.Id)));
        }
        return selected;
    }

    public async Task<IActionResult> Index() => View(new GamesHomeViewModel((await CandidatesAsync()).Count, TempData["GameMessage"] as string));

    private string? Audio(string? filename) => filename is not null && Path.GetFileName(filename) == filename
        && filename.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
        && System.IO.File.Exists(Path.Combine(environment.WebRootPath, "audio", "tigrinya", filename)) ? filename : null;

    [HttpGet]
    public async Task<IActionResult> Match(MatchGameMode mode = MatchGameMode.Pairs)
    {
        if (!Enum.IsDefined(mode)) return BadRequest();
        var selected = (await CandidatesAsync()).Take(12).OrderBy(_ => Random.Shared.Next()).ToList();
        if (selected.Count < 2) return View(new MatchGameViewModel { Mode = mode });
        var rounds = (selected.Count + 3) / 4;
        var pairs = new List<MatchGamePair>();
        var offset = 0;
        for (var round = 0; round < rounds; round++)
        {
            var size = selected.Count / rounds + (round < selected.Count % rounds ? 1 : 0);
            foreach (var item in selected.Skip(offset).Take(size))
                pairs.Add(new MatchGamePair(item.Question.Id, item.Question.LessonId, item.Question.LatinText,
                    item.Question.Choices.Single(choice => choice.IsCorrect).Text, Audio(item.Question.AudioFileName),
                    Fingerprint(item.Question), item.Review?.LastAnsweredAt.Ticks, round, Guid.NewGuid(), Guid.NewGuid(), item.Question.Audience));
            offset += size;
        }
        var state = new MatchGameState { UserId = UserId, Mode = mode, Pairs = pairs };
        cache.Set(CacheKey(state.Id), state, TimeSpan.FromHours(1));
        var board = new GameBoardViewModel(mode == MatchGameMode.Memory, Enumerable.Range(0, rounds).Select(round =>
            new GameRoundViewModel(
                pairs.Where(pair => pair.Round == round).OrderBy(_ => Random.Shared.Next()).Select(pair => new GameCardViewModel(pair.SwedishKey.ToString(), pair.Meaning, UsageLabel: PhraseUsageGuide.For(pair.LatinText, pair.Audience)?.Label)).ToList(),
                pairs.Where(pair => pair.Round == round).OrderBy(_ => Random.Shared.Next()).Select(pair => new GameCardViewModel(pair.TigrinyaKey.ToString(), pair.LatinText, pair.AudioFileName, PhraseUsageGuide.For(pair.LatinText, pair.Audience)?.Label)).ToList())).ToList());
        return View(new MatchGameViewModel
        {
            Mode = mode, IsAvailable = true, PairCount = pairs.Count, Board = board,
            SessionToken = protector.Protect(JsonSerializer.Serialize(new MatchGameToken(state.Id, UserId, state.IssuedAt)))
        });
    }

    private MatchGameState? State(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16000) return null;
        try
        {
            var parsed = JsonSerializer.Deserialize<MatchGameToken>(protector.Unprotect(token));
            return parsed?.UserId == UserId && parsed.IssuedAt >= DateTime.UtcNow.AddHours(-1) && parsed.IssuedAt <= DateTime.UtcNow
                && cache.TryGetValue<MatchGameState>(CacheKey(parsed.SessionId), out var state) && state?.UserId == UserId ? state : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException) { return null; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckPair(GameMatchInput input)
    {
        if (!ModelState.IsValid || input.RequestId == Guid.Empty || input.SwedishKey == Guid.Empty || input.TigrinyaKey == Guid.Empty) return BadRequest();
        var state = State(input.SessionToken);
        if (state is null) return Conflict(new { message = "Spelomgången har gått ut. Starta en ny omgång." });
        await state.Gate.WaitAsync();
        try
        {
            if (state.Requests.TryGetValue(input.RequestId, out var previous))
                return previous.SwedishKey == input.SwedishKey && previous.TigrinyaKey == input.TigrinyaKey ? Json(previous.Response) : BadRequest();
            if (state.Finalized || state.Complete || state.Requests.Count >= 500) return BadRequest();
            var left = state.Pairs.SingleOrDefault(pair => pair.SwedishKey == input.SwedishKey && pair.Round == state.CurrentRound);
            var right = state.Pairs.SingleOrDefault(pair => pair.TigrinyaKey == input.TigrinyaKey && pair.Round == state.CurrentRound);
            if (left is null || right is null || state.Matched.Contains(left.SwedishKey) || state.Matched.Contains(right.SwedishKey)) return BadRequest();
            var correct = left.QuestionId == right.QuestionId;
            if (correct)
            {
                state.Matched.Add(left.SwedishKey);
                var firstTry = !state.Missed.Contains(left.SwedishKey);
                state.Streak = firstTry ? state.Streak + 1 : 0;
                state.Points += firstTry ? 100 + 10 * Math.Min(state.Streak - 1, 4) : 50;
            }
            else { state.Missed.Add(left.SwedishKey); state.WrongAttempts++; state.Streak = 0; }
            var roundComplete = state.Pairs.Where(pair => pair.Round == left.Round).All(pair => state.Matched.Contains(pair.SwedishKey));
            var response = new GameMatchResponse(correct, state.Points, state.Streak, state.Matched.Count, state.Pairs.Count,
                roundComplete, state.Complete, roundComplete && !state.Complete ? state.CurrentRound : null);
            state.Requests.Add(input.RequestId, new GameRequestMemo(input.SwedishKey, input.TigrinyaKey, response));
            return Json(response);
        }
        finally { state.Gate.Release(); }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish(string sessionToken)
    {
        var state = State(sessionToken);
        if (state is null) return BadRequest("Spelomgången har gått ut. Starta en ny omgång.");
        await state.Gate.WaitAsync();
        try
        {
            if (!state.Complete) return BadRequest("Matcha alla par innan du avslutar.");
            if (!state.Finalized && state.Mode == MatchGameMode.Pairs)
            {
                await using var transaction = await progress.BeginLessonsUpdateAsync(UserId, state.Pairs.Select(pair => pair.LessonId));
                // Samma frågelås som frasredigeraren, i stabil ordning.
                foreach (var id in state.Pairs.Select(pair => pair.QuestionId).Order())
                {
                    var resource = "TigrinyaLingo:ContentEditing:Question:" + id;
                    await context.Database.ExecuteSqlInterpolatedAsync($"""
                        DECLARE @result int;
                        EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Shared', @LockOwner = 'Transaction', @LockTimeout = 10000;
                        IF @result < 0 THROW 50001, N'Frasen används just nu. Försök igen.', 1;
                        """);
                }
                var ids = state.Pairs.Select(pair => pair.QuestionId).ToList();
                var questions = await context.Questions.AsNoTracking().Include(question => question.Choices)
                    .Where(question => ids.Contains(question.Id) && context.UserProgresses.Any(saved => saved.UserId == UserId && saved.LessonId == question.LessonId))
                    .ToDictionaryAsync(question => question.Id);
                var states = await context.QuestionReviewStates.AsNoTracking().Where(item => item.UserId == UserId && ids.Contains(item.QuestionId)).ToDictionaryAsync(item => item.QuestionId);
                state.Outdated = state.Pairs.Any(pair => !questions.TryGetValue(pair.QuestionId, out var question)
                    || Fingerprint(question) != pair.ContentFingerprint || states.GetValueOrDefault(pair.QuestionId)?.LastAnsweredAt.Ticks != pair.LastAnsweredTicks);
                if (!state.Outdated)
                {
                    await progress.SaveReviewOutcomesAsync(UserId, state.Pairs.ToDictionary(pair => pair.QuestionId, pair => !state.Missed.Contains(pair.SwedishKey)));
                    await transaction.CommitAsync();
                    state.ReviewSaved = true;
                }
            }
            state.Finalized = true;
            TempData["GameResultToken"] = sessionToken;
            return RedirectToAction(nameof(Result));
        }
        finally { state.Gate.Release(); }
    }

    public async Task<IActionResult> Result()
    {
        var state = State(TempData.Peek("GameResultToken") as string ?? string.Empty);
        if (state is null || !state.Finalized)
        {
            TempData["GameMessage"] = "Resultatet är inte tillgängligt längre. Välj ett spel och starta en ny omgång.";
            return RedirectToAction(nameof(Index));
        }
        return View(new MatchGameResultViewModel
        {
            Mode = state.Mode, Points = state.Points, PairCount = state.Pairs.Count,
            FirstTryCount = state.Pairs.Count - state.Missed.Count,
            Stars = state.WrongAttempts == 0 ? 3 : state.WrongAttempts <= state.Pairs.Count ? 2 : 1,
            ReviewSaved = state.ReviewSaved, Outdated = state.Outdated,
            PendingReviewCount = await context.QuestionReviewStates.CountAsync(item => item.UserId == UserId && item.NeedsReview),
            Words = state.Pairs.Select(pair => new GameResultWord(pair.LatinText, pair.Meaning, pair.AudioFileName,
                state.Missed.Contains(pair.SwedishKey), pair.Audience)).ToList()
        });
    }
}
