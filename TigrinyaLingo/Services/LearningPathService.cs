using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Services;

// Förkunskaper styr rekommendationer. Alla färdiga lektionsquiz är öppna.
public class LearningPathService(ApplicationDbContext context)
{
    internal static bool IsReady(Lesson lesson) => lesson.Questions.Count > 0
        && lesson.Questions.All(question => TenseGuide.IsPublished(question) && question.Choices.Count == 4
            && question.Choices.Count(choice => choice.IsCorrect) == 1
            && (!string.IsNullOrWhiteSpace(question.AudioFileName) || !string.IsNullOrWhiteSpace(question.LatinText)));

    public async Task<List<CourseDetailsViewModel>> CoursesAsync(string? userId, int? courseId = null)
    {
        var query = context.Courses.AsNoTracking().AsSplitQuery()
            .Include(course => course.Lessons).ThenInclude(lesson => lesson.Questions).ThenInclude(question => question.Choices)
            .AsQueryable();
        if (courseId.HasValue) query = query.Where(course => course.Id == courseId.Value);
        var courses = await query.OrderBy(course => course.Title).ThenBy(course => course.Id).ToListAsync();
        var progress = userId is null ? new Dictionary<int, Domain.Entities.UserProgress>()
            : await context.UserProgresses.AsNoTracking().Where(saved => saved.UserId == userId
                && (!courseId.HasValue || saved.Lesson!.CourseId == courseId.Value)).ToDictionaryAsync(saved => saved.LessonId);
        var preference = userId is null ? null : await context.UserLearningPreferences.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId);
        var result = new List<CourseDetailsViewModel>();
        foreach (var course in courses)
        {
            var model = new CourseDetailsViewModel { Id = course.Id, Title = course.Title, Description = course.Description, RecommendedStartingOrder = course.Id == 1 ? LearningPreferenceService.StartingOrder(preference?.Experience) : 1 };
            foreach (var lesson in course.Lessons.OrderBy(lesson => lesson.Order).ThenBy(lesson => lesson.Id))
            {
                var saved = progress.GetValueOrDefault(lesson.Id);
                var ready = IsReady(lesson);
                var complete = saved?.CompletedAt is not null;
                var item = new LessonListItemViewModel {
                    Id = lesson.Id, Title = lesson.Title, Order = lesson.Order, WordCount = lesson.Questions.Count,
                    BestScore = saved?.Score, IsCompleted = complete, IsReady = ready, IsLocked = false
                };
                model.Lessons.Add(item);
            }
            result.Add(model);
        }
        return result;
    }

    public async Task<LessonListItemViewModel?> LessonAsync(string userId, int courseId, int lessonId) =>
        (await CoursesAsync(userId, courseId)).SingleOrDefault()?.Lessons.SingleOrDefault(lesson => lesson.Id == lessonId);
}
