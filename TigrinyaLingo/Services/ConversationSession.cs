using TigrinyaLingo.Models;

using Domain.Entities;

namespace TigrinyaLingo.Services;

internal record ConversationToken(Guid SessionId, string UserId, DateTime IssuedAt);
internal record ConversationSessionTurn(Guid Key, int QuestionId, int LessonId, string LatinText, string Meaning,
    string Situation, string PartnerLine, string Fingerprint, long? LastAnsweredTicks, Guid CorrectChoiceKey,
    List<ConversationChoiceViewModel> Choices, PhraseAudience Audience = PhraseAudience.Automatic, string? Explanation = null);
internal record ConversationAnswerMemo(Guid RequestId, Guid TurnKey, Guid ChoiceKey, string Answer, bool IsCorrect, ConversationFeedback Feedback, string? UsageLabel = null);
internal sealed class ConversationSession
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime IssuedAt { get; } = DateTime.UtcNow;
    public required string UserId { get; init; }
    public required string SceneId { get; init; }
    public required string Title { get; init; }
    public required List<ConversationSessionTurn> Turns { get; init; }
    public List<ConversationAnswerMemo> Answers { get; } = new();
    public List<PracticeSourceSnapshot> Sources { get; init; } = new();
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public int CorrectCount => Answers.Count(answer => answer.IsCorrect);
    public int Points => CorrectCount * 100;
    public bool Complete => Answers.Count == Turns.Count;
    public bool Finalized { get; set; }
    public bool Outdated { get; set; }
    public bool ReviewSaved { get; set; }
}
