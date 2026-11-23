using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TigrinyaLingo.Models;

namespace TigrinyaLingo.Areas.Identity.Pages.Account.Manage;

[Authorize]
public class ProfileModel(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn, ApplicationDbContext db) : PageModel
{
    public class ProfileInput : ProfileNameInput
    {
        [Phone(ErrorMessage = "Ange ett giltigt telefonnummer."), Display(Name = "Telefonnummer (valfritt)")]
        public string? PhoneNumber { get; set; }
    }
    [BindProperty] public ProfileInput Input { get; set; } = new();
    [TempData] public bool Saved { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var account = await users.GetUserAsync(User);
        if (account == null) return NotFound();
        Input.DisplayName = (await users.GetClaimsAsync(account)).FirstOrDefault(c => c.Type == ProfileNameInput.ClaimType)?.Value ?? "";
        Input.PhoneNumber = account.PhoneNumber;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var account = await users.GetUserAsync(User);
        if (account == null) return NotFound();
        var previous = (await users.GetClaimsAsync(account)).FirstOrDefault(c => c.Type == ProfileNameInput.ClaimType);
        var name = new Claim(ProfileNameInput.ClaimType, Input.DisplayName);
        var result = previous == null ? await users.AddClaimAsync(account, name) : await users.ReplaceClaimAsync(account, previous, name);
        if (result.Succeeded && account.PhoneNumber != Input.PhoneNumber)
            result = await users.SetPhoneNumberAsync(account, Input.PhoneNumber);
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Profilen kunde inte sparas. Ladda om sidan och försök igen.");
            return Page();
        }
        await transaction.CommitAsync();
        await signIn.RefreshSignInAsync(account);
        Saved = true;
        return RedirectToPage();
    }
}
