using Domain.Entities;

namespace TigrinyaLingo.Models
{
    // Vy-modeller innehåller bara den information som respektive sida behöver.
    public class CourseListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int LessonCount { get; set; }
    }
    public class CourseDetailsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<LessonListItemViewModel> Lessons { get; set; } = new();
        public int AvailableCount => Lessons.Count(lesson => lesson.IsReady);
        public int CompletedCount => Lessons.Count(lesson => lesson.IsReady && lesson.IsCompleted);
        // Rekommenderad start är vägledning. Tidigare lektioner förblir öppna och oklarade.
        public int RecommendedStartingOrder { get; set; } = 1;
        public LessonListItemViewModel? NextLesson => Lessons.FirstOrDefault(lesson => lesson.IsReady && !lesson.IsCompleted && lesson.Order >= RecommendedStartingOrder)
            ?? Lessons.FirstOrDefault(lesson => lesson.IsReady && !lesson.IsCompleted);
    }
    public class LessonListItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; }
        public int WordCount { get; set; }
        public int? BestScore { get; set; }
        public bool IsCompleted { get; set; }
        public bool IsReady { get; set; }
        public bool IsLocked { get; set; }
        public int? RequiredLessonId { get; set; }
        public string? RequiredLessonTitle { get; set; }
    }
    public class LessonDetailsViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public List<StudyWordViewModel> Words { get; set; } = new();
        public bool IsQuizLocked { get; set; }
        public int? RequiredLessonId { get; set; }
        public string? RequiredLessonTitle { get; set; }
    }
    public class StudyWordViewModel
    {
        public string LatinText { get; set; } = string.Empty;
        public PhraseAudience Audience { get; set; }
        public TenseHint? TenseHint { get; set; }
        public string? AudioFileName { get; set; }
        public string Meaning { get; set; } = string.Empty;
    }
}
