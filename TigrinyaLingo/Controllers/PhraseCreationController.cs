using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TigrinyaLingo.Authorization;
using TigrinyaLingo.Models;
using TigrinyaLingo.Services;

namespace TigrinyaLingo.Controllers;

[Authorize(Policy = LocalContentEditing.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PhraseCreationController(ApplicationDbContext context, IDataProtectionProvider protection,
    IWebHostEnvironment environment, IMemoryCache cache) : Controller
{
    private readonly IDataProtector protector = protection.CreateProtector("TigrinyaLingo.PhraseCreation.v1");
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private List<string> AudioFiles()
    {
        var folder = Path.Combine(environment.WebRootPath, "audio", "tigrinya");
        return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.mp3").Select(Path.GetFileName).OfType<string>()
            .Order(StringComparer.OrdinalIgnoreCase).ToList() : new();
    }

    private async Task<PhraseCreateViewModel> ModelAsync(PhraseCreateInput input) => new()
    {
        LessonId = input.LessonId, CourseId = input.CourseId, NewLessonTitle = input.NewLessonTitle,
        TenseGroup = input.TenseGroup, TimeReference = input.TimeReference, TenseContext = input.TenseContext, TenseNotes = input.TenseNotes, TenseReviewed = input.TenseReviewed,
        LatinText = input.LatinText, Audience = input.Audience, AudioFileName = input.AudioFileName, CorrectChoiceIndex = input.CorrectChoiceIndex,
        Choices = input.Choices, CreationToken = input.CreationToken, AudioFiles = AudioFiles(),
        Courses = await context.Courses.AsNoTracking().OrderBy(course => course.Id).Select(course => new ContentCourseOption(course.Id, course.Title)).ToListAsync(),
        Lessons = await context.Lessons.AsNoTracking().OrderBy(lesson => lesson.CourseId).ThenBy(lesson => lesson.Order).ThenBy(lesson => lesson.Id)
            .Select(lesson => new ContentLessonOption(lesson.Id, lesson.CourseId, lesson.Title,
                context.UserProgresses.Any(progress => progress.LessonId == lesson.Id))).ToListAsync()
    };

    [HttpGet]
    public async Task<IActionResult> Index(int lessonId = 0)
    {
        var input = new PhraseCreateInput
        {
            LessonId = lessonId, CorrectChoiceIndex = 0,
            Choices = Enumerable.Range(0, 4).Select(_ => new PhraseChoiceInput()).ToList(),
            CreationToken = protector.Protect(JsonSerializer.Serialize(new PhraseCreationStamp(UserId, Guid.NewGuid(), DateTime.UtcNow)))
        };
        var model = await ModelAsync(input);
        if (model.LessonId != 0 && !model.Lessons.Any(lesson => lesson.Id == model.LessonId && !lesson.HasResults))
        {
            model.LessonId = 0;
            model.PlacementMessage = "Den valda lektionen kan inte byggas ut. Skapa en ny lektion för de nya fraserna.";
        }
        model.CourseId = model.Courses.FirstOrDefault()?.Id ?? 0;
        return View(model);
    }

    private PhraseCreationStamp? ReadToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16000) return null;
        try
        {
            var stamp = JsonSerializer.Deserialize<PhraseCreationStamp>(protector.Unprotect(token));
            return stamp?.UserId == UserId && stamp.Nonce != Guid.Empty && stamp.IssuedAt <= DateTime.UtcNow
                && stamp.IssuedAt >= DateTime.UtcNow.AddHours(-2) ? stamp : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException) { return null; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(PhraseCreateInput input)
    {
        var stamp = ReadToken(input.CreationToken);
        if (stamp is null) return BadRequest("Formuläret är ogiltigt eller har gått ut. Öppna Lägg till fras på nytt.");
        var cacheKey = $"TigrinyaLingo:CreatePhrase:{UserId}:{stamp.Nonce:N}";
        if (cache.TryGetValue<int>(cacheKey, out var previousId)) return RedirectToAction("Edit", "ContentEditor", new { id = previousId });
        if (input.Choices is null || input.Choices.Count != 4 || input.Choices.Any(choice => choice is null))
            return BadRequest("En fras behöver fyra svarsalternativ.");
        input.LatinText = (input.LatinText ?? string.Empty).Trim();
        input.NewLessonTitle = input.NewLessonTitle?.Trim();
        input.AudioFileName = string.IsNullOrWhiteSpace(input.AudioFileName) ? null : input.AudioFileName.Trim();
        foreach (var choice in input.Choices) choice.Text = (choice.Text ?? string.Empty).Trim();
        if (input.LatinText.Length == 0) ModelState.AddModelError(nameof(input.LatinText), "Fyll i tigrinjaordet eller frasen.");
        if (Regex.IsMatch(input.LatinText, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"))
            ModelState.AddModelError(nameof(input.LatinText), "Skriv uttrycket med latinska bokstäver.");
        if (input.Choices.Any(choice => choice.Text.Length == 0)) ModelState.AddModelError(string.Empty, "Fyll i alla fyra svenska svarsalternativ.");
        if (input.Choices.Select(choice => choice.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
            ModelState.AddModelError(string.Empty, "Skriv fyra olika svenska svarsalternativ.");
        if (input.AudioFileName is not null && !AudioFiles().Contains(input.AudioFileName, StringComparer.Ordinal))
            ModelState.AddModelError(nameof(input.AudioFileName), "Välj en befintlig inspelning eller Ingen inspelning.");
        if (input.LessonId < 0) ModelState.AddModelError(nameof(input.LessonId), "Välj en befintlig lektion eller skapa en ny.");
        if (input.LessonId == 0)
        {
            if (string.IsNullOrWhiteSpace(input.NewLessonTitle)) ModelState.AddModelError(nameof(input.NewLessonTitle), "Ge den nya lektionen ett namn.");
            if (!await context.Courses.AnyAsync(course => course.Id == input.CourseId)) ModelState.AddModelError(nameof(input.CourseId), "Välj en befintlig kurs.");
        }
        if (!ModelState.IsValid) return View(await ModelAsync(input));

        await using var transaction = await context.Database.BeginTransactionAsync();
        var requestResource = "TigrinyaLingo:CreatePhrase:" + stamp.Nonce.ToString("N");
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = {requestResource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 50001, N'Kunde inte spara frasen. Försök igen.', 1;
            """);
        if (cache.TryGetValue<int>(cacheKey, out previousId)) return RedirectToAction("Edit", "ContentEditor", new { id = previousId });
        Lesson lesson;
        if (input.LessonId > 0)
        {
            await LessonContentLock.WriteAsync(context, input.LessonId);
            var existing = await context.Lessons.SingleOrDefaultAsync(item => item.Id == input.LessonId);
            if (existing is null) ModelState.AddModelError(nameof(input.LessonId), "Lektionen finns inte längre.");
            else if (await context.UserProgresses.AnyAsync(progress => progress.LessonId == input.LessonId))
                ModelState.AddModelError(nameof(input.LessonId), "Lektionen har sparade resultat. Skapa en ny lektion så att tidigare poäng fortfarande stämmer.");
            else if (await context.Questions.AnyAsync(question => question.LessonId == input.LessonId && question.LatinText == input.LatinText))
                ModelState.AddModelError(nameof(input.LatinText), "Uttrycket finns redan i lektionen. Redigera det befintliga uttrycket i stället.");
            if (!ModelState.IsValid) return View(await ModelAsync(input));
            lesson = existing!;
        }
        else
        {
            var courseResource = "TigrinyaLingo:CourseContent:" + input.CourseId;
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = {courseResource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
                IF @result < 0 THROW 50001, N'Kunde inte skapa lektionen. Försök igen.', 1;
                """);
            var lastOrder = await context.Lessons.Where(item => item.CourseId == input.CourseId).MaxAsync(item => (int?)item.Order) ?? 0;
            lesson = new Lesson { CourseId = input.CourseId, Title = input.NewLessonTitle!, Order = checked(lastOrder + 1) };
        }
        var question = new Question
        {
            Lesson = lesson, TenseGroup = input.TenseGroup, TimeReference = input.TimeReference, TenseContext = input.TenseContext, TenseNotes = input.TenseNotes, TenseReviewed = input.TenseReviewed,
        LatinText = input.LatinText, Audience = input.Audience, Text = string.Empty, AudioFileName = input.AudioFileName,
            Choices = input.Choices.Select((choice, index) => new Choice { Text = choice.Text, IsCorrect = index == input.CorrectChoiceIndex }).ToList()
        };
        TenseGuide.Apply(input, question);
        context.Questions.Add(question);
        await context.SaveChangesAsync();
        // SQL-låset hålls tills transaktionen är klar; cachevärdet skyddar dubbelklick i den aktuella appkörningen.
        cache.Set(cacheKey, question.Id, TimeSpan.FromHours(2));
        try { await transaction.CommitAsync(); }
        catch { cache.Remove(cacheKey); throw; }
        TempData["ContentEditorMessage"] = "Frasen är tillagd. Du kan lägga till fler uttryck i lektionen eller öppna den och börja öva.";
        return RedirectToAction("Edit", "ContentEditor", new { id = question.Id });
    }
}
