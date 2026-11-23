using System.Security.Cryptography;
using System.Text;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Services;

public class QuizProgressStore(ApplicationDbContext context)
{
    // Samtidiga inskickningar för samma konto och lektion sparas i turordning.
    // Låset försvinner när transaktionen avslutas. Olika användare blockerar inte varandra.
    public Task<IDbContextTransaction> BeginLessonUpdateAsync(string userId, int lessonId) => BeginLessonsUpdateAsync(userId, new[] { lessonId });

    public async Task<IDbContextTransaction> BeginLessonsUpdateAsync(string userId, IEnumerable<int> lessonIds)
    {
        var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            foreach (var lessonId in lessonIds.Distinct().Order())
            {
                await LessonContentLock.ReadAsync(context, lessonId);
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId}:{lessonId}")));
                var resource = "TigrinyaLingo:Quiz:" + key;
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    DECLARE @result int;
                    EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive',
                        @LockOwner = 'Transaction', @LockTimeout = 10000;
                    IF @result < 0 THROW 50001, N'Kunde inte spara resultatet. Försök igen.', 1;
                    """);
            }
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }
    }

    // För spel som servern redan rättat. Anropas under berörda lektions-/användarlås.
    // Endast senaste repetitionsstatus ändras, aldrig bästa lektionspoäng eller XP.
    public async Task SaveReviewOutcomesAsync(string userId, IReadOnlyDictionary<int, bool> outcomes)
    {
        var ids = outcomes.Keys.ToList();
        var states = await context.QuestionReviewStates.Where(state => state.UserId == userId && ids.Contains(state.QuestionId))
            .ToDictionaryAsync(state => state.QuestionId);
        var now = DateTime.UtcNow;
        foreach (var outcome in outcomes)
        {
            if (!states.TryGetValue(outcome.Key, out var state))
            {
                state = new QuestionReviewState { UserId = userId, QuestionId = outcome.Key };
                context.QuestionReviewStates.Add(state);
            }
            state.NeedsReview = !outcome.Value;
            state.LastAnsweredAt = now;
        }
        await context.SaveChangesAsync();
    }
    // Anropas inom en lektions transaktion, efter att svaren validerats och rättats.
    public async Task<int> SaveAnswersAsync(string userId, Lesson lesson, List<QuizAnswerInput> answers, bool fullQuiz)
    {
        var earnedXp = 0;
        var now = DateTime.UtcNow;
        var questionIds = answers.Select(answer => answer.QuestionId).ToList();
        var states = await context.QuestionReviewStates
            .Where(state => state.UserId == userId && questionIds.Contains(state.QuestionId))
            .ToDictionaryAsync(state => state.QuestionId);
        var score = 0;
        foreach (var answer in answers)
        {
            var question = lesson.Questions.Single(question => question.Id == answer.QuestionId);
            var correct = question.Choices.Single(choice => choice.Id == answer.ChoiceId).IsCorrect;
            if (correct) score++;
            if (!states.TryGetValue(question.Id, out var state))
            {
                state = new QuestionReviewState { UserId = userId, QuestionId = question.Id };
                context.QuestionReviewStates.Add(state);
            }
            state.NeedsReview = !correct;
            state.LastAnsweredAt = now;
        }

        // En delövning får aldrig ge poäng eller klar-status för hela lektionen.
        if (fullQuiz)
        {
            var progress = await context.UserProgresses
                .SingleOrDefaultAsync(progress => progress.UserId == userId && progress.LessonId == lesson.Id);
            if (progress is null)
            {
                progress = new UserProgress { UserId = userId, LessonId = lesson.Id };
                context.UserProgresses.Add(progress);
            }
            var previousXp = LearningRewards.Total(progress.Score, progress.CompletedAt.HasValue);
            progress.Score = Math.Max(progress.Score, score);
            if (score == lesson.Questions.Count) progress.CompletedAt ??= now;
            earnedXp = LearningRewards.Total(progress.Score, progress.CompletedAt.HasValue) - previousXp;
        }
        await context.SaveChangesAsync();
        return earnedXp;
    }
}
