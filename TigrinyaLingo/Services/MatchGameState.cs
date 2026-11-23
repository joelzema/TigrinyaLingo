using TigrinyaLingo.Models;

using Domain.Entities;

namespace TigrinyaLingo.Services;

internal record MatchGamePair(int QuestionId, int LessonId, string LatinText, string Meaning, string? AudioFileName,
    string ContentFingerprint, long? LastAnsweredTicks, int Round, Guid SwedishKey, Guid TigrinyaKey, PhraseAudience Audience = PhraseAudience.Automatic);
internal record GameRequestMemo(Guid SwedishKey, Guid TigrinyaKey, GameMatchResponse Response);
internal sealed class MatchGameState
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserId { get; init; } = string.Empty;
    public DateTime IssuedAt { get; init; } = DateTime.UtcNow;
    public MatchGameMode Mode { get; init; }
    public List<MatchGamePair> Pairs { get; init; } = new();
    public SemaphoreSlim Gate { get; } = new(1, 1);
    public HashSet<Guid> Matched { get; } = new();
    public HashSet<Guid> Missed { get; } = new();
    public Dictionary<Guid, GameRequestMemo> Requests { get; } = new();
    public int Points { get; set; }
    public int Streak { get; set; }
    public int WrongAttempts { get; set; }
    public bool Finalized { get; set; }
    public bool ReviewSaved { get; set; }
    public bool Outdated { get; set; }
    public bool Complete => Matched.Count == Pairs.Count;
    public int CurrentRound => Pairs.Where(pair => !Matched.Contains(pair.SwedishKey)).Select(pair => pair.Round).DefaultIfEmpty(-1).Min();
}
