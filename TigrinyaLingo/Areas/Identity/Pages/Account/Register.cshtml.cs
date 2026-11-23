using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Security.Claims;
using Infrastructure.Data;
using TigrinyaLingo.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace TigrinyaLingo.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel(UserManager<IdentityUser> users, IWebHostEnvironment environment,
    IOptions<IdentityOptions> identityOptions, ApplicationDbContext db) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string ReturnUrl { get; set; } = "/";
    public bool IsLocalRegistration => environment.IsDevelopment();
    public List<string> PasswordRequirements
    {
        get
        {
            var rules = identityOptions.Value.Password;
            var requirements = new List<string> { $"Minst {rules.RequiredLength} tecken." };
            if (rules.RequireUppercase) requirements.Add("Minst en stor bokstav (A–Z).");
            if (rules.RequireLowercase) requirements.Add("Minst en liten bokstav (a–z).");
            if (rules.RequireDigit) requirements.Add("Minst en siffra (0–9).");
            if (rules.RequireNonAlphanumeric) requirements.Add("Minst ett specialtecken, till exempel ! eller ?.");
            if (rules.RequiredUniqueChars > 1) requirements.Add($"Minst {rules.RequiredUniqueChars} olika tecken.");
            return requirements;
        }
    }

    public void OnGet(string? returnUrl = null) => ReturnUrl = LocalReturnUrl(returnUrl);

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = LocalReturnUrl(returnUrl);
        // Den lokala bekräftelsen ersätter ingen mejltjänst vid publicering.
        if (!IsLocalRegistration)
        {
            ModelState.AddModelError(string.Empty, "Registrering är inte öppnad i den publicerade versionen ännu.");
            return Page();
        }
        if (!ModelState.IsValid) return Page();
        var user = new IdentityUser { UserName = Input.Email.Trim(), Email = Input.Email.Trim() };
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await users.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, Describe(error.Code));
            return Page();
        }

        var named = await users.AddClaimAsync(user, new Claim(ProfileNameInput.ClaimType, Input.DisplayName));
        if (!named.Succeeded)
        {
            ModelState.AddModelError("", "Användarnamnet kunde inte sparas. Försök igen.");
            return Page();
        }
        await transaction.CommitAsync();
        var code = await users.GenerateEmailConfirmationTokenAsync(user);
        // TempData är skyddad av ASP.NET. Bara webbläsaren som nyss registrerade kontot
        // får den lokala bekräftelselänken; ett mejladress-parametervärde räcker inte.
        TempData["LocalRegistration.UserId"] = user.Id;
        TempData["LocalRegistration.Email"] = user.Email;
        TempData["LocalRegistration.Code"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        return RedirectToPage("RegisterConfirmation", new { email = user.Email, returnUrl = ReturnUrl });
    }

    private string LocalReturnUrl(string? value) => Url.IsLocalUrl(value) ? value! : "/";
    private string Describe(string code) => code switch
    {
        "DuplicateUserName" or "DuplicateEmail" => "Det finns redan ett konto med den mejladressen. Välj Logga in om kontot är ditt.",
        "PasswordTooShort" => $"Lösenordet måste innehålla minst {identityOptions.Value.Password.RequiredLength} tecken.",
        "PasswordRequiresUpper" => "Lösenordet behöver minst en stor bokstav (A–Z).",
        "PasswordRequiresLower" => "Lösenordet behöver minst en liten bokstav (a–z).",
        "PasswordRequiresDigit" => "Lösenordet behöver minst en siffra (0–9).",
        "PasswordRequiresNonAlphanumeric" => "Lösenordet behöver minst ett specialtecken, till exempel ! eller ?.",
        "PasswordRequiresUniqueChars" => $"Lösenordet behöver minst {identityOptions.Value.Password.RequiredUniqueChars} olika tecken.",
        "InvalidEmail" or "InvalidUserName" => "Kontrollera att mejladressen är korrekt skriven.",
        _ => "Kontot kunde inte skapas. Kontrollera uppgifterna och försök igen."
    };

    public class InputModel : ProfileNameInput
    {
        [Required(ErrorMessage = "Skriv din mejladress.")]
        [EmailAddress(ErrorMessage = "Skriv en giltig mejladress.")]
        [Display(Name = "Mejladress")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Välj ett lösenord.")]
        [DataType(DataType.Password)]
        [Display(Name = "Lösenord")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Upprepa lösenordet.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Lösenorden stämmer inte överens.")]
        [Display(Name = "Upprepa lösenordet")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
