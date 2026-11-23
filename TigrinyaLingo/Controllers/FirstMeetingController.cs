using System.Text;
using System.Text.RegularExpressions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class FirstMeetingController(ApplicationDbContext context) : Controller
{
    private static string AnswerKey(string text)
    {
        // Macron markerar vokallängd i vissa transkriptioner. Behåll skillnaden mellan a och i.
        var key = PhraseUsageGuide.FormKey(text).Normalize(NormalizationForm.FormD)
            .Replace("\u0304", "").Normalize();
        return key.Replace("kämäy", "kemey").Replace("kämey", "kemey").Replace("sälam", "selam");
    }

    [HttpGet]
    public Task<IActionResult> Index(int step = 1) => Turn(step, null, false);

    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Answer(int step, string? answer) => Turn(step, answer, true);

    private async Task<IActionResult> Turn(int step, string? answer, bool submitted)
    {
        if (!ModelState.IsValid || step < 1 || step > 3 || answer?.Length > 200) return BadRequest();
        int[] ids = [1, 5, 13, 14];
        var words = await context.Questions.AsNoTracking().Include(q => q.Choices)
            .Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id);
        bool Matches(int id, string meaning, PhraseAudience? audience = null)
        {
            if (!words.TryGetValue(id, out var word) || !TenseGuide.IsPublished(word)
                || string.IsNullOrWhiteSpace(word.LatinText)) return false;
            var meanings = word.Choices.Where(c => c.IsCorrect).Select(c => c.Text.Trim().TrimEnd('?', '.', '…').TrimEnd()).ToArray();
            return meanings.Length == 1 && string.Equals(meanings[0], meaning, StringComparison.OrdinalIgnoreCase)
                && (audience == null || PhraseUsageGuide.For(word.LatinText, word.Audience)?.Label == PhraseUsageGuide.Label(audience.Value));
        }
        IActionResult Unavailable()
        {
            Response.StatusCode = 409;
            return View("Index", (FirstMeetingTurn?)null);
        }
        if (!Matches(1, "Hej") || !Matches(5, "Jag heter") || !Matches(13, "Hur mår du", PhraseAudience.Man)
            || !Matches(14, "Hur mår du", PhraseAudience.Woman)) return Unavailable();

        var phrases = words.ToDictionary(pair => pair.Key, pair => pair.Value.LatinText.Trim());
        var nameTemplate = phrases[5];
        phrases[5] = Regex.Replace(nameTemplate, @"\.{3}|…", "Sara");
        var options = phrases.Values.OrderBy(_ => Random.Shared.Next()).ToArray();
        if (options.Select(AnswerKey).Distinct().Count() != 4) return Unavailable();
        var (expected, partner, prompt) = step switch
        {
            1 => (phrases[1], "En kvinna du träffar för första gången säger hej.", "Hälsa tillbaka."),
            2 => (phrases[14], "Ni stannar för att småprata.", "Fråga hur hon mår."),
            _ => (phrases[5], "Hon berättar vad hon heter och frågar efter ditt namn.",
                nameTemplate != phrases[5] ? "I dialogen heter du Sara. Presentera dig." : "Svara med presentationen från lektionen.")
        };
        var turn = new FirstMeetingTurn(step, partner, prompt, expected, options);
        if (!submitted) return View("Index", turn);
        var key = AnswerKey(answer ?? "");
        var correct = key == AnswerKey(expected);
        var feedback = correct ? "Det passar här!" : string.IsNullOrWhiteSpace(answer) ? "Skriv en replik eller välj ett av alternativen."
            : step == 2 && key == AnswerKey(phrases[13])
            ? $"”{phrases[13]}” säger du till en man. Här talar du till en kvinna. Prova ”{phrases[14]}”."
            : options.Any(option => AnswerKey(option) == key)
            ? "Det uttrycket hör till en annan situation. Läs repliken och försök igen."
            : "Svaret matchar inte kursens uttryck ännu. Kontrollera stavningen eller öppna ledtråden.";
        return View("Index", turn with { Correct = correct, Feedback = feedback, Attempt = answer });
    }
}
