
namespace TigrinyaLingo.Models;

public class DailySubmissionInput
{
    public string SessionToken { get; set; } = string.Empty;
    public List<QuizAnswerInput> Answers { get; set; } = new();
}

// Skyddas av ASP.NET Data Protection och binds till det inloggade kontot.
public record DailyQuestionSelection(int QuestionId, ExerciseKind Kind, long? LastAnsweredTicks);
public record DailySession(string UserId, DateTime IssuedAt, List<DailyQuestionSelection> Questions);
public record DailyAttempt(string UserId, List<QuizAnswerInput> Answers);

public class DailyTrainingResultViewModel
{
    public List<DailyAnswerResult> Answers { get; set; } = new();
    public int CorrectCount => Answers.Count(answer => answer.IsCorrect);
    public int PendingCount { get; set; }
}

public class DailyAnswerResult : QuizQuestionResultViewModel
{
    public string Meaning { get; set; } = string.Empty;
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
}
