namespace TigrinyaLingo.Models;

public class LearnViewModel
{
    public Domain.Entities.LearningExperience? Experience { get; set; }
    public List<TenseGroupViewModel> TensePractice { get; set; } = new();
    public List<CourseDetailsViewModel> Courses { get; set; } = new();
    public int PendingReviewCount { get; set; }
    public int TotalXp { get; set; }
    public int Level => TotalXp / LearningRewards.XpPerLevel + 1;
    public int LevelProgress => TotalXp % LearningRewards.XpPerLevel;
    public int AvailableCount => Courses.Sum(course => course.AvailableCount);
    public int CompletedCount => Courses.Sum(course => course.CompletedCount);
    public CourseDetailsViewModel? NextCourse => Courses
        .Where(course => course.NextLesson is not null)
        .OrderByDescending(course => course.NextLesson!.BestScore.HasValue)
        .ThenByDescending(course => course.Lessons.Any(lesson => lesson.BestScore.HasValue))
        .ThenBy(course => course.Title).ThenBy(course => course.Id).FirstOrDefault();
    public LessonListItemViewModel? NextLesson => NextCourse?.NextLesson;
    public bool CanPlay => Courses.SelectMany(course => course.Lessons)
        .Where(lesson => lesson.IsReady && lesson.BestScore.HasValue).Sum(lesson => lesson.WordCount) >= 2;
}

public record LockedLessonViewModel(string LessonTitle, int CourseId, int RequiredLessonId, string RequiredLessonTitle);
