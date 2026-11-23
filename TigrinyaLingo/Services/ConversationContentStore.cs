using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Services;

internal record ConversationSource(ConversationSceneCard Card, List<Question> Questions);
public class ConversationContentStore(ApplicationDbContext context)
{
    private static string Canonical(string text) => Regex.Replace(text.Normalize(), @"\s+", " ").Trim().TrimEnd('.', '!', '?', '…');
    internal static string Fingerprint(Question question) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
        question.Id, question.LessonId, question.LatinText, question.Audience, question.TenseGroup, question.TimeReference, question.TenseContext, question.TenseNotes, question.TenseReviewed,
        Choices = question.Choices.OrderBy(choice => choice.Id).Select(choice => new { choice.Id, choice.Text, choice.IsCorrect })
    }))));

    internal async Task<List<ConversationSource>> LoadAsync(string userId)
    {
        var lessonIds = ConversationCatalog.Scenes.Select(scene => scene.LessonId).ToList();
        var lessons = await context.Lessons.AsNoTracking().Include(lesson => lesson.Questions).ThenInclude(question => question.Choices)
            .Where(lesson => lessonIds.Contains(lesson.Id)).ToDictionaryAsync(lesson => lesson.Id);
        var completed = await context.UserProgresses.AsNoTracking().Where(saved => saved.UserId == userId && saved.CompletedAt != null)
            .Select(saved => saved.LessonId).ToListAsync();
        var sources = new List<ConversationSource>();
        foreach (var scene in ConversationCatalog.Scenes)
        {
            lessons.TryGetValue(scene.LessonId, out var lesson);
            var selected = new List<Question>();
            if (lesson is not null && LearningPathService.IsReady(lesson))
                foreach (var turn in scene.Turns)
                {
                    var matches = lesson.Questions.Where(question => string.Equals(Canonical(question.Choices.Single(choice => choice.IsCorrect).Text),
                        Canonical(turn.Meaning), StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matches.Count == 1 && (turn.Audience == Domain.Entities.PhraseAudience.Automatic
                        || PhraseUsageGuide.For(matches[0].LatinText, matches[0].Audience)?.Label == PhraseUsageGuide.Label(turn.Audience)))
                        selected.Add(matches[0]);
                }
            var valid = selected.Count == scene.Turns.Count && selected.Select(question => question.Id).Distinct().Count() == selected.Count
                && selected.Select(question => Canonical(question.LatinText)).Distinct(StringComparer.OrdinalIgnoreCase).Count() == selected.Count;
            var done = completed.Contains(scene.LessonId);
            var message = lesson is null ? "Lektionsinnehållet finns inte ännu."
                : !done ? "Gör klart lektionen för att öppna samtalet."
                : !valid ? "Fraserna har ändrats. Samtalets betydelser behöver granskas innan du kan spela." : null;
            sources.Add(new ConversationSource(new ConversationSceneCard(scene.Id, scene.Title, scene.Description,
                lesson?.Title ?? "Kommande lektion", scene.LessonId, scene.Turns.Count, done && valid, done, message), valid ? selected : new()));
        }
        return sources;
    }
}
