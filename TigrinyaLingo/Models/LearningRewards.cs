namespace TigrinyaLingo.Models;

public enum ExerciseKind { Listen, Read, Translate }

public static class LearningRewards
{
    public const int CorrectAnswerXp = 10;
    public const int CompletionBonus = 20;
    public const int XpPerLevel = 100;
    public static int Total(int bestScore, bool completed) =>
        bestScore * CorrectAnswerXp + (completed ? CompletionBonus : 0);
}
