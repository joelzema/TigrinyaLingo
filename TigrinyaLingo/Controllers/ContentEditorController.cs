using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Authorization;
using TigrinyaLingo.Models;
using TigrinyaLingo.Services;

namespace TigrinyaLingo.Controllers;

[Authorize(Policy = LocalContentEditing.Policy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ContentEditorController(ApplicationDbContext context, IDataProtectionProvider protection,
    IWebHostEnvironment environment, ILogger<ContentEditorController> logger) : Controller
{
    private readonly IDataProtector protector = protection.CreateProtector("TigrinyaLingo.ContentEditor.v1");
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index(string? search)
    {
        search = (search ?? string.Empty).Trim();
        if (search.Length > 100) search = search[..100];
        var query = context.Questions.AsNoTracking();
        if (search.Length > 0)
            query = query.Where(question => question.LatinText.Contains(search) || question.Choices.Any(choice => choice.Text.Contains(search))
                || question.Lesson!.Title.Contains(search) || (question.TenseGroup != null && question.TenseGroup.Contains(search)));
        var items = await query.OrderBy(question => question.Lesson!.Order).ThenBy(question => question.LessonId).ThenBy(question => question.Id)
            .Select(question => new ContentEditorItem
            {
                Id = question.Id, LessonId = question.LessonId, LessonTitle = question.Lesson!.Title,
                TenseGroup = question.TenseGroup, TimeReference = question.TimeReference, TenseReviewed = question.TenseReviewed,
                LatinText = question.LatinText, HasAudio = question.AudioFileName != null,
                Meaning = question.Choices.Where(choice => choice.IsCorrect).Select(choice => choice.Text).FirstOrDefault() ?? "Rätt svar saknas"
            }).ToListAsync();
        return View(new ContentEditorIndexViewModel { Search = search, Items = items });
    }

    private Task<Question?> QuestionAsync(int id) => context.Questions.Include(question => question.Lesson)
        .Include(question => question.Choices).SingleOrDefaultAsync(question => question.Id == id);

    private List<string> AudioFiles()
    {
        var folder = Path.Combine(environment.WebRootPath, "audio", "tigrinya");
        return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.mp3").Select(Path.GetFileName)
            .OfType<string>().Order(StringComparer.OrdinalIgnoreCase).ToList() : new();
    }

    private static string Fingerprint(Question question) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { question.Id, question.LessonId, question.LatinText, question.Audience, question.TenseGroup, question.TimeReference, question.TenseContext, question.TenseNotes, question.TenseReviewed, question.AudioFileName,
            Choices = question.Choices.OrderBy(choice => choice.Id).Select(choice => new { choice.Id, choice.Text, choice.IsCorrect }) }))));

    private PhraseEditViewModel Model(Question question, PhraseEditInput? input = null) => new()
    {
        Id = question.Id, LessonId = question.LessonId, LessonTitle = question.Lesson!.Title,
        LatinText = input?.LatinText ?? question.LatinText,
        Audience = input?.Audience ?? question.Audience,
        TenseGroup = input is null ? question.TenseGroup : input.TenseGroup,
        TimeReference = input is null ? question.TimeReference : input.TimeReference,
        TenseContext = input is null ? question.TenseContext : input.TenseContext,
        TenseNotes = input is null ? question.TenseNotes : input.TenseNotes,
        TenseReviewed = input is null ? question.TenseReviewed : input.TenseReviewed,
        AudioFileName = input is null ? question.AudioFileName : input.AudioFileName,
        Choices = input?.Choices ?? question.Choices.OrderBy(choice => choice.Id).Select(choice => new PhraseChoiceInput { Id = choice.Id, Text = choice.Text }).ToList(),
        CorrectChoiceId = input?.CorrectChoiceId ?? question.Choices.FirstOrDefault(choice => choice.IsCorrect)?.Id ?? 0,
        AudioFiles = AudioFiles(),
        EditStamp = protector.Protect(JsonSerializer.Serialize(new PhraseEditStamp(UserId, question.Id, Fingerprint(question), DateTime.UtcNow)))
    };

    public async Task<IActionResult> Edit(int id)
    {
        var question = await QuestionAsync(id);
        if (question is null) return NotFound();
        if (question.Choices.Count != 4) return BadRequest("Redigeraren behöver en fråga med fyra svarsalternativ.");
        return View(Model(question));
    }

    private PhraseEditStamp? ReadStamp(string token, int id)
    {
        if (string.IsNullOrEmpty(token) || token.Length > 16000) return null;
        try
        {
            var stamp = JsonSerializer.Deserialize<PhraseEditStamp>(protector.Unprotect(token));
            return stamp?.UserId == UserId && stamp.QuestionId == id && stamp.IssuedAt <= DateTime.UtcNow
                && stamp.IssuedAt >= DateTime.UtcNow.AddHours(-12) ? stamp : null;
        }
        catch (Exception error) when (error is CryptographicException or JsonException) { return null; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PhraseEditInput input)
    {
        var stamp = ReadStamp(input.EditStamp, id);
        if (stamp is null) return BadRequest("Redigeringssidan är ogiltig eller har gått ut. Öppna frasen på nytt.");
        var lessonId = await context.Questions.Where(question => question.Id == id).Select(question => (int?)question.LessonId).SingleOrDefaultAsync();
        if (lessonId is null) return NotFound();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await LessonContentLock.WriteAsync(context, lessonId.Value);
        var resource = "TigrinyaLingo:ContentEditing:Question:" + id;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 50001, N'Kunde inte spara ändringen. Försök igen.', 1;
            """);
        var question = await QuestionAsync(id);
        if (question is null) return NotFound();
        if (question.Choices.Count != 4 || input.Choices is null || input.Choices.Count != 4 || input.Choices.Any(choice => choice is null)
            || !input.Choices.Select(choice => choice.Id).ToHashSet().SetEquals(question.Choices.Select(choice => choice.Id)))
            return BadRequest("Svarsalternativen stämmer inte med frasen.");
        if (!question.Choices.Any(choice => choice.Id == input.CorrectChoiceId))
            ModelState.AddModelError(nameof(input.CorrectChoiceId), "Välj vilket av de fyra svaren som är rätt.");
        input.LatinText = (input.LatinText ?? string.Empty).Trim();
        input.AudioFileName = string.IsNullOrWhiteSpace(input.AudioFileName) ? null : input.AudioFileName.Trim();
        foreach (var choice in input.Choices) choice.Text = (choice.Text ?? string.Empty).Trim();
        if (input.LatinText.Length == 0) ModelState.AddModelError(nameof(input.LatinText), "Fyll i tigrinjaordet eller frasen.");
        if (Regex.IsMatch(input.LatinText, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"))
            ModelState.AddModelError(nameof(input.LatinText), "Skriv uttrycket med latinska bokstäver.");
        if (input.Choices.Any(choice => choice.Text.Length == 0)) ModelState.AddModelError(string.Empty, "Fyll i alla fyra svenska svarsalternativ.");
        if (input.Choices.Select(choice => choice.Text).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
            ModelState.AddModelError(string.Empty, "De fyra svarsalternativen behöver vara olika.");
        if (input.AudioFileName is not null && !AudioFiles().Contains(input.AudioFileName, StringComparer.Ordinal))
            ModelState.AddModelError(nameof(input.AudioFileName), "Välj en befintlig uttalsinspelning eller inget ljud.");
        if (Fingerprint(question) != stamp.Fingerprint)
        {
            ModelState.AddModelError(string.Empty, "Frasen har ändrats sedan du öppnade sidan. Jämför den aktuella versionen nedan med dina ändringar och spara igen om du vill ersätta den.");
            ModelState.Remove(nameof(input.EditStamp));
            var conflict = Model(question, input);
            conflict.HasConflict = true;
            conflict.CurrentLatinText = question.LatinText;
            conflict.CurrentAudience = question.Audience;
            conflict.CurrentTenseContext = question.TenseContext;
            conflict.CurrentTenseNotes = question.TenseNotes;
            conflict.CurrentTenseStatus = question.TenseGroup is null ? "Inga tidsformsuppgifter" : question.TenseGroup + " · " + TenseGuide.Label(question.TimeReference) + (question.TenseReviewed ? " · granskad" : " · utkast");
            conflict.CurrentChoices = question.Choices.OrderBy(choice => choice.Id).Select(choice => choice.Text).ToList();
            Response.StatusCode = StatusCodes.Status409Conflict;
            return View(conflict);
        }
        if (!ModelState.IsValid)
        {
            ModelState.Remove(nameof(input.EditStamp));
            return View(Model(question, input));
        }
        // En inspelning läses inte om när texten ändras. Behåll inte den gamla
        // kopplingen av misstag; en annan uttryckligen vald fil får användas.
        var removePreviousAudio = !string.Equals(question.LatinText, input.LatinText, StringComparison.Ordinal)
            && input.AudioFileName is not null
            && string.Equals(question.AudioFileName, input.AudioFileName, StringComparison.Ordinal);
        question.LatinText = input.LatinText;
        question.Audience = input.Audience;
        TenseGuide.Apply(input, question);
        question.AudioFileName = removePreviousAudio ? null : input.AudioFileName;
        foreach (var choice in question.Choices)
        {
            choice.Text = input.Choices.Single(item => item.Id == choice.Id).Text;
            choice.IsCorrect = choice.Id == input.CorrectChoiceId;
        }
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        logger.LogInformation("Fras {QuestionId} redigerades lokalt av konto {UserId}", question.Id, UserId);
        TempData["ContentEditorMessage"] = removePreviousAudio
            ? "Ändringen är sparad. Den tidigare inspelningen kopplades bort eftersom frasen ändrades. Välj en ny inspelning om du har en som passar."
            : "Ändringen är sparad. Den visas nästa gång du öppnar lektionen eller träningen.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    public async Task<IActionResult> Export()
    {
        var questions = await context.Questions.AsNoTracking().Include(question => question.Lesson).Include(question => question.Choices)
            .OrderBy(question => question.LessonId).ThenBy(question => question.Id).ToListAsync();
        var data = new { FormatVersion = 1, ExportedAtUtc = DateTime.UtcNow, Phrases = questions.Select(question => new
        {
            question.Id, question.LessonId, LessonTitle = question.Lesson!.Title, question.LatinText, question.AudioFileName,
            TenseGroup = question.TenseGroup, TimeReference = question.TimeReference.ToString(), question.TenseContext, question.TenseNotes, question.TenseReviewed,
            Audience = question.Audience.ToString(), Usage = PhraseUsageGuide.For(question.LatinText, question.Audience),
            Choices = question.Choices.OrderBy(choice => choice.Id).Select(choice => new { choice.Id, choice.Text, choice.IsCorrect })
        }) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions { WriteIndented = true });
        return File(bytes, "application/json", $"tigrinyalingo-innehall-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
    }
}
