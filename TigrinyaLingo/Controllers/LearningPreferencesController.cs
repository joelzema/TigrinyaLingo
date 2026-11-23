using System.Security.Claims;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TigrinyaLingo.Models;
using TigrinyaLingo.Services;

namespace TigrinyaLingo.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class LearningPreferencesController(ApplicationDbContext context, LearningPreferenceService preferences) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string SafeReturn(string? value) => Url.IsLocalUrl(value) && !value!.StartsWith("/LearningPreferences", StringComparison.OrdinalIgnoreCase) ? value : "/Learn";

    [HttpGet]
    public async Task<IActionResult> Index(string? returnUrl = null)
    {
        var saved = await preferences.FindAsync(UserId);
        ModelState.Remove(nameof(LearningPreferenceInput.ReturnUrl));
        return View(new LearningPreferenceInput { Experience = saved?.Experience, ReturnUrl = SafeReturn(returnUrl) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(LearningPreferenceInput input)
    {
        input.ReturnUrl = SafeReturn(input.ReturnUrl);
        // HTML helpers får inte återge ett förfalskat ReturnUrl ur ModelState.
        ModelState.Remove(nameof(input.ReturnUrl));
        if (!ModelState.IsValid) return View(input);
        // Samma kontos samtidiga första inskickningar ska skapa en enda rad.
        await using var transaction = await context.Database.BeginTransactionAsync();
        var resource = "TigrinyaLingo:LearningPreference:" + UserId;
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 50001, N'Kunde inte spara valet. Försök igen.', 1;
            """);
        var row = await context.UserLearningPreferences.SingleOrDefaultAsync(row => row.UserId == UserId);
        if (row is null) { row = new UserLearningPreference { UserId = UserId }; context.UserLearningPreferences.Add(row); }
        row.Experience = input.Experience!.Value;
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return LocalRedirect(input.ReturnUrl);
    }
}
