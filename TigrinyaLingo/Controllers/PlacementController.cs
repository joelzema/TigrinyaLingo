using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using TigrinyaLingo.Models;
using TigrinyaLingo.Services;

namespace TigrinyaLingo.Controllers;

[Authorize]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public class PlacementController(PlacementStore store, IMemoryCache cache, IDataProtectionProvider protection) : Controller
{
    private record Ticket(Guid Id,string UserId,DateTime IssuedAt);
    private string UserId=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private readonly IDataProtector protector=protection.CreateProtector("TigrinyaLingo.Placement.v1");
    private static string CacheKey(Guid id)=>"TigrinyaLingo:Placement:"+id.ToString("N");
    public IActionResult Index()=>View();

    [HttpGet]
    public async Task<IActionResult> Practice(string token, string area, int step = 1, Guid? choice = null)
    {
        var session = Read(token);
        if (session?.Result is null)
            return Unavailable("Gör ett nytt nivåtest för att få övningar från ditt resultat.");
        if (!ModelState.IsValid || !session.Result.Areas.Any(a => a.Name == area && a.Correct < a.Total))
            return BadRequest();
        var cards = session.PracticeCards.Where(c => c.Area == area).ToList();
        if (step < 1 || step > cards.Count) return BadRequest();
        var card = cards[step - 1];
        if (choice.HasValue && !card.Choices.Any(c => c.Key == choice)) return BadRequest();
        if (!await store.IsCurrentAsync(session))
            return Unavailable("Fraserna har ändrats. Gör ett nytt nivåtest för att träna med de aktuella uttrycken.");
        return View(new PlacementPracticePage(token, card, step, cards.Count, choice));
    }

    [HttpGet]
    public async Task<IActionResult> Start()
    {
        var session=await store.CreateAsync(UserId);
        if(session is null) return Unavailable("Testets innehåll behöver granskas innan du kan börja. Du kan fortsätta med de vanliga lektionerna.");
        cache.Set(CacheKey(session.Id),session,TimeSpan.FromHours(1));
        var token=protector.Protect(JsonSerializer.Serialize(new Ticket(session.Id,UserId,session.IssuedAt)));
        return View("Play",new PlacementPlay(token,session.Turns.Select(t=>t.Question).ToList()));
    }
    private PlacementSession? Read(string? token)
    {
        if(string.IsNullOrWhiteSpace(token)||token.Length>4000) return null;
        try {
            var ticket=JsonSerializer.Deserialize<Ticket>(protector.Unprotect(token));
            return ticket?.UserId==UserId && ticket.IssuedAt>=DateTime.UtcNow.AddHours(-1) && ticket.IssuedAt<=DateTime.UtcNow
                && cache.TryGetValue<PlacementSession>(CacheKey(ticket.Id),out var session) && session?.UserId==UserId ? session : null;
        } catch(Exception error) when(error is CryptographicException or JsonException) { return null; }
    }
    private IActionResult Unavailable(string message)
    {
        Response.StatusCode=409;
        return View("Unavailable",message);
    }
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(PlacementSubmission input)
    {
        var session=Read(input.Token);
        if(session is null) return Unavailable("Testomgången har gått ut eller hör till ett annat konto. Starta ett nytt test när du vill.");
        await session.Gate.WaitAsync();
        try {
            if(input.Answers is null || input.Answers.Count!=session.Turns.Count
                || input.Answers.Any(a=>a is null) || input.Answers.Select(a=>a.QuestionKey).Distinct().Count()!=session.Turns.Count
                || input.Answers.Any(a=>!session.Turns.Any(t=>t.Question.Key==a.QuestionKey
                    && (a.ChoiceKey is null || a.ChoiceKey==Guid.Empty || t.Question.Choices.Any(c=>c.Key==a.ChoiceKey))))) return BadRequest();
            if(!ModelState.IsValid || input.Answers.Any(a=>a.ChoiceKey is null))
                return View("Play",new PlacementPlay(input.Token,session.Turns.Select(t=>t.Question with {
                    Selected=input.Answers.Single(a=>a.QuestionKey==t.Question.Key).ChoiceKey }).ToList(),
                    "Besvara varje fråga eller välj Jag vet inte – hoppa över frågan."));
            var signature=string.Join(";",input.Answers.OrderBy(a=>a.QuestionKey).Select(a=>a.QuestionKey+":"+a.ChoiceKey));
            if(session.Result is not null)
                return session.SubmissionKey==signature ? RedirectToAction(nameof(Result),new { token=input.Token }) : BadRequest();
            if(!await store.IsCurrentAsync(session)) return Unavailable("Fraserna har ändrats sedan testet startade. Starta om för att få ett aktuellt resultat.");
            var result=await store.GradeAsync(session,input.Answers);
            if(result is null) return Unavailable("Det finns ingen färdig startlektion just nu. Försök igen senare.");
            session.Result=result;
            session.SubmissionKey=signature;
            return RedirectToAction(nameof(Result),new { token=input.Token });
        } finally { session.Gate.Release(); }
    }
    [HttpGet]
    public IActionResult Result(string? token)
    {
        var session=Read(token);
        ViewData["PracticeToken"] = token;
        ViewData["PracticeAreas"] = session?.PracticeCards.Select(c => c.Area).ToHashSet();
        return session?.Result is { } result ? View(result) : Unavailable("Resultatet saknas eller testomgången har gått ut. Du kan göra ett nytt test när du vill.");
    }
}
