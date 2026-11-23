using System.Text.RegularExpressions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Services;

internal record TenseGroupContent(int Id, string Title, List<Question> Questions, List<string> Issues);
public class TenseLearningStore(ApplicationDbContext context)
{
    private static string GroupKey(string title) => Regex.Replace(title.Normalize().Trim(), @"\s+", " ").ToUpperInvariant();
    internal async Task<List<TenseGroupContent>> LoadAsync()
    {
        var questions = await context.Questions.AsNoTracking().Include(question => question.Choices).Include(question => question.Lesson)
            .Where(question => question.TenseGroup != null && question.TenseGroup != "").OrderBy(question => question.Id).ToListAsync();
        return questions.GroupBy(question => GroupKey(question.TenseGroup!)).Select(group => {
            var words = group.OrderBy(question => question.TimeReference).ThenBy(question => question.Id).ToList();
            var issues = new List<string>();
            if (words.Count != 3 || new[] { TimeReference.Present, TimeReference.Past, TimeReference.Future }.Any(time => words.Count(question => question.TimeReference == time) != 1))
                issues.Add("Gruppen behöver exakt en fras för nutid, en för dåtid och en för framtid.");
            if (words.Any(question => !TenseGuide.IsReviewed(question))) issues.Add("Alla tre former behöver sammanhang och språkgranskning.");
            if (words.Any(question => string.IsNullOrWhiteSpace(question.LatinText) || question.Choices.Count != 4 || question.Choices.Count(choice => choice.IsCorrect) != 1))
                issues.Add("Varje fras behöver latinsk tigrinja och fyra alternativ med ett rätt svar.");
            if (words.Select(question => PhraseUsageGuide.FormKey(question.LatinText)).Distinct().Count() != words.Count)
                issues.Add("För den här övningen behöver de tre tigrinjasvaren vara olika.");
            return new TenseGroupContent(group.Min(question => question.Id), group.First().TenseGroup!, words, issues);
        }).OrderBy(group => group.Title).ToList();
    }

    private async Task<HashSet<int>> CompletedAsync(string? userId) => userId is null ? new() :
        (await context.UserProgresses.AsNoTracking().Where(saved => saved.UserId == userId && saved.CompletedAt != null).Select(saved => saved.LessonId).ToListAsync()).ToHashSet();
    private async Task<List<Question>> KnownAsync(string? userId)
    {
        if (userId is null) return new();
        var lessons = await context.Lessons.AsNoTracking().Include(lesson => lesson.Questions).ThenInclude(question => question.Choices)
            .Where(lesson => context.UserProgresses.Any(saved => saved.UserId == userId && saved.LessonId == lesson.Id && saved.CompletedAt != null)).ToListAsync();
        return lessons.Where(LearningPathService.IsReady).SelectMany(lesson => lesson.Questions).Where(question => !string.IsNullOrWhiteSpace(question.LatinText)).ToList();
    }
    private static List<Question> Distractors(TenseGroupContent group, List<Question> known)
    {
        var forms = group.Questions.Select(question => PhraseUsageGuide.FormKey(question.LatinText)).ToHashSet();
        var meanings = group.Questions.Select(question => question.Choices.FirstOrDefault(choice => choice.IsCorrect)?.Text.Trim().ToUpperInvariant()).ToHashSet();
        return known.Where(question => !forms.Contains(PhraseUsageGuide.FormKey(question.LatinText))
            && !meanings.Contains(question.Choices.Single(choice => choice.IsCorrect).Text.Trim().ToUpperInvariant())).ToList();
    }
    public async Task<List<TenseGroupViewModel>> ModelsAsync(string? userId, bool includeDrafts)
    {
        var complete = await CompletedAsync(userId); var known = await KnownAsync(userId);
        return (await LoadAsync()).Where(group => includeDrafts || group.Issues.Count == 0).Select(group => new TenseGroupViewModel(group.Id, group.Title,
            group.Questions.Select(question => new TenseWordViewModel(question.Id, question.LessonId, question.Lesson!.Title, question.LatinText,
                question.Choices.FirstOrDefault(choice => choice.IsCorrect)?.Text ?? "Betydelse saknas", question.Audience, TenseGuide.For(question))).ToList(),
            group.Issues, group.Questions.All(question => complete.Contains(question.LessonId)),
            group.Issues.Count == 0 && group.Questions.All(question => complete.Contains(question.LessonId)) && Distractors(group, known).Count > 0)).ToList();
    }
    internal async Task<ConversationSession?> CreateAsync(string userId, int id)
    {
        var group = (await LoadAsync()).SingleOrDefault(group => group.Id == id && group.Issues.Count == 0);
        if (group is null) return null;
        var completed = await CompletedAsync(userId);
        if (!group.Questions.All(question => completed.Contains(question.LessonId))) return null;
        var distractors = Distractors(group, await KnownAsync(userId));
        if (distractors.Count == 0) return null;
        var states = await context.QuestionReviewStates.AsNoTracking().Where(state => state.UserId == userId).ToDictionaryAsync(state => state.QuestionId);
        var sources = group.Questions.ToDictionary(question => question.Id);
        var turns = new List<ConversationSessionTurn>();
        foreach (var question in group.Questions)
        {
            var distractor = distractors[Random.Shared.Next(distractors.Count)]; sources.TryAdd(distractor.Id, distractor);
            var choices = group.Questions.Append(distractor).OrderBy(_ => Random.Shared.Next())
                .Select(word => new { word.Id, Choice = new ConversationChoiceViewModel(Guid.NewGuid(), word.LatinText) }).ToList();
            var hint = TenseGuide.For(question)!;
            turns.Add(new ConversationSessionTurn(Guid.NewGuid(), question.Id, question.LessonId, question.LatinText,
                question.Choices.Single(choice => choice.IsCorrect).Text, "Välj frasen som passar situationen.", hint.Context,
                ConversationContentStore.Fingerprint(question), states.GetValueOrDefault(question.Id)?.LastAnsweredAt.Ticks,
                choices.Single(choice => choice.Id == question.Id).Choice.Key, choices.Select(choice => choice.Choice).ToList(), question.Audience,
                TenseGuide.Explanation(question)));
        }
        var ids = group.Questions.Select(question => question.Id).ToHashSet();
        return new ConversationSession { UserId = userId, SceneId = id.ToString(), Title = group.Title, Turns = turns.OrderBy(_ => Random.Shared.Next()).ToList(),
            Sources = sources.Values.Select(question => new PracticeSourceSnapshot(question.Id, question.LessonId, ConversationContentStore.Fingerprint(question),
                states.GetValueOrDefault(question.Id)?.LastAnsweredAt.Ticks, ids.Contains(question.Id))).ToList() };
    }
}
