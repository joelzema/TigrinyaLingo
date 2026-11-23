using Domain.Entities;

namespace TigrinyaLingo.Models;

public enum MatchGameMode { Pairs, Memory }
public record GamesHomeViewModel(int KnownWordCount, string? Message);
public record GameCardViewModel(string Key, string Text, string? AudioFileName = null, string? UsageLabel = null);
public record GameRoundViewModel(List<GameCardViewModel> Swedish, List<GameCardViewModel> Tigrinya);
public record GameBoardViewModel(bool IsMemory, List<GameRoundViewModel> Rounds);
public class MatchGameViewModel
{
    public MatchGameMode Mode { get; set; }
    public bool IsAvailable { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public GameBoardViewModel? Board { get; set; }
    public int PairCount { get; set; }
}
public class GameMatchInput
{
    public string SessionToken { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public Guid SwedishKey { get; set; }
    public Guid TigrinyaKey { get; set; }
}
public record GameMatchResponse(bool IsCorrect, int Points, int Streak, int MatchedCount, int TotalPairs,
    bool RoundComplete, bool GameComplete, int? NextRound);
public record MatchGameToken(Guid SessionId, string UserId, DateTime IssuedAt);
public record GameResultWord(string LatinText, string Meaning, string? AudioFileName, bool NeedsPractice, PhraseAudience Audience = PhraseAudience.Automatic);
public class MatchGameResultViewModel
{
    public MatchGameMode Mode { get; set; }
    public int Points { get; set; }
    public int PairCount { get; set; }
    public int FirstTryCount { get; set; }
    public int Stars { get; set; }
    public int PendingReviewCount { get; set; }
    public bool ReviewSaved { get; set; }
    public bool Outdated { get; set; }
    public List<GameResultWord> Words { get; set; } = new();
}
