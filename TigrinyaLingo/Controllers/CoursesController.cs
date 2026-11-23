using Infrastructure.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;
using TigrinyaLingo.Services;

namespace TigrinyaLingo.Controllers
{
    public class CoursesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly LearningPathService _path;
        public CoursesController(ApplicationDbContext context, LearningPathService path) { _context = context; _path = path; }

        public async Task<IActionResult> Index()
        {
            // Sidan läser data. AsNoTracking undviker onödig ändringsspårning.
            var courses = await _context.Courses.AsNoTracking()
                .OrderBy(course => course.Title).ThenBy(course => course.Id)
                .Select(course => new CourseListItemViewModel
                {
                    Id = course.Id, Title = course.Title, Description = course.Description,
                    Category = course.Category, LessonCount = course.Lessons.Count
                }).ToListAsync();
            return View(courses);
        }

        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Details(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var course = (await _path.CoursesAsync(userId, id)).SingleOrDefault();
            if (course is null) return NotFound();
            return View(course);
        }
    }
}
