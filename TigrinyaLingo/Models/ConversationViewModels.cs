using Domain.Entities;

namespace TigrinyaLingo.Models;

public record ConversationSceneCard(string Id, string Title, string Description, string LessonTitle,
    int LessonId, int TurnCount, bool IsReady, bool LessonCompleted, string? Message);
public record ConversationsHomeViewModel(List<ConversationSceneCard> Scenes, string? Message);
public record ConversationChoiceViewModel(Guid Key, string Text, string? UsageLabel = null);
public record ConversationTurnViewModel(Guid Key, string Situation, string PartnerLine, List<ConversationChoiceViewModel> Choices);
public record ConversationBoardViewModel(string Title, List<ConversationTurnViewModel> Turns);
public class ConversationPlayViewModel
{
    public bool IsAddressPractice { get; init; }
    public bool IsTensePractice { get; init; }
    public bool IsPractice => IsAddressPractice || IsTensePractice;
    public required ConversationSceneCard Scene { get; init; }
    public string SessionToken { get; init; } = string.Empty;
    public ConversationBoardViewModel? Board { get; init; }
}
public class ConversationAnswerInput
{
    public string SessionToken { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public Guid TurnKey { get; set; }
    public Guid ChoiceKey { get; set; }
}
public record ConversationFeedback(bool IsCorrect, Guid CorrectChoiceKey, string CorrectText, string Meaning,
    int Points, int CorrectCount, int AnsweredCount, int TotalCount, bool IsComplete, string? UsageLabel = null, string? Explanation = null);
public record ConversationResultTurn(string Situation, string PartnerLine, string Answer, string CorrectText, string Meaning, bool IsCorrect, PhraseAudience Audience = PhraseAudience.Automatic, string? AnswerUsageLabel = null, string? Explanation = null);
public class ConversationResultViewModel
{
    public bool IsAddressPractice { get; init; }
    public bool IsTensePractice { get; init; }
    public bool IsPractice => IsAddressPractice || IsTensePractice;
    public required string SceneId { get; init; }
    public required string Title { get; init; }
    public int Points { get; init; }
    public int CorrectCount { get; init; }
    public int TotalCount { get; init; }
    public int Stars { get; init; }
    public bool ReviewSaved { get; init; }
    public bool Outdated { get; init; }
    public List<ConversationResultTurn> Turns { get; init; } = new();
}
