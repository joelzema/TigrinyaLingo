namespace TigrinyaLingo.Models;

public record FirstMeetingTurn(int Step, string Partner, string Prompt, string Expected,
    string[] Options, string? Feedback = null, bool Correct = false, string? Attempt = null);
