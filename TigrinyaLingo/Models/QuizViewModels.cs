using System.ComponentModel.DataAnnotations;

using Domain.Entities;

namespace TigrinyaLingo.Models
{
    public class AudioPlayerViewModel
    {
        public string AudioFileName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }

    public class QuizViewModel
    {
        public bool IsDaily { get; set; }
        public string SessionToken { get; set; } = string.Empty;
        public bool IsReview { get; set; }
        public string? StatusMessage { get; set; }
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public int CourseId { get; set; }
        public bool IsAvailable { get; set; }
        public List<QuizQuestionViewModel> Questions { get; set; } = new();
    }

    // Betydelsen används i lärsteget. Rättningen använder fortfarande databasens facit.
    public class QuizQuestionViewModel
    {
        public ExerciseKind Kind { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string StudyMeaning { get; set; } = string.Empty;
        public int Id { get; set; }
        public string LatinText { get; set; } = string.Empty;
        public PhraseAudience Audience { get; set; }
        public TenseHint? TenseHint { get; set; }
        public string? AudioFileName { get; set; }
        public int? SelectedChoiceId { get; set; }
        public List<QuizChoiceViewModel> Choices { get; set; } = new();
    }

    public class QuizChoiceViewModel
    {
        public string? UsageLabel { get; set; }
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public class QuizSubmissionInput
    {
        public List<QuizAnswerInput> Answers { get; set; } = new();
    }

    public class QuizAnswerInput
    {
        [EnumDataType(typeof(ExerciseKind))]
        public ExerciseKind Kind { get; set; }
        public int QuestionId { get; set; }

        [Required(ErrorMessage = "Välj ett svar för varje fråga.")]
        public int? ChoiceId { get; set; }
    }

    // Dessa uppgifter läggs i skyddad TempData för resultatet efter POST/redirect/GET.
    public class QuizAttemptData
    {
        public int EarnedXp { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int LessonId { get; set; }
        public List<QuizAnswerInput> Answers { get; set; } = new();
    }

    public class QuizResultViewModel
    {
        public List<TenseGroupViewModel> TensePractice { get; set; } = new();
        public int EarnedXp { get; set; }
        public int TotalXp { get; set; }
        public int Level => TotalXp / LearningRewards.XpPerLevel + 1;
        public int LevelProgress => TotalXp % LearningRewards.XpPerLevel;
        public bool IsReview { get; set; }
        public int PendingReviewCount { get; set; }
        public int CourseId { get; set; }
        public LessonListItemViewModel? NextLesson { get; set; }
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public int QuestionCount { get; set; }
        public int? AttemptScore { get; set; }
        public int BestScore { get; set; }
        public bool IsCompleted { get; set; }
        public List<QuizQuestionResultViewModel> Answers { get; set; } = new();
    }

    public class QuizQuestionResultViewModel
    {
        public bool IsLatinAnswer { get; set; }
        public string LatinText { get; set; } = string.Empty;
        public PhraseAudience Audience { get; set; }
        public TenseHint? TenseHint { get; set; }
        public string SelectedAnswer { get; set; } = string.Empty;
        public string CorrectAnswer { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }

    public class ProgressViewModel
    {
        public int TotalXp => Lessons.Sum(lesson => LearningRewards.Total(lesson.BestScore, lesson.IsCompleted));
        public int Level => TotalXp / LearningRewards.XpPerLevel + 1;
        public int LevelProgress => TotalXp % LearningRewards.XpPerLevel;
        public List<LessonProgressViewModel> Lessons { get; set; } = new();
        public int CompletedCount => Lessons.Count(lesson => lesson.IsCompleted);
    }

    public class LessonProgressViewModel
    {
        public int PendingReviewCount { get; set; }
        public int LessonId { get; set; }
        public string LessonTitle { get; set; } = string.Empty;
        public string CourseTitle { get; set; } = string.Empty;
        public int BestScore { get; set; }
        public int QuestionCount { get; set; }
        public bool IsCompleted { get; set; }
    }
}
