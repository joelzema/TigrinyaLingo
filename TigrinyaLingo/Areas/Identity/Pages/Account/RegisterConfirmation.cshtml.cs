using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;

namespace TigrinyaLingo.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterConfirmationModel(IWebHostEnvironment environment) : PageModel
{
    public string? ConfirmationUrl { get; private set; }
    public string ReturnUrl { get; private set; } = "/";

    public void OnGet(string? email = null, string? returnUrl = null)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        if (!environment.IsDevelopment()) return;
        // TempData kan läsa tillbaka ett GUID-format konto-ID som Guid.
        var userId = TempData.Peek("LocalRegistration.UserId")?.ToString();
        if (string.IsNullOrEmpty(userId)
            || TempData.Peek("LocalRegistration.Code") is not string code
            || TempData.Peek("LocalRegistration.Email") is not string registeredEmail
            || !string.Equals(email, registeredEmail, StringComparison.OrdinalIgnoreCase)) return;
        ConfirmationUrl = Url.Page("ConfirmEmail", new { userId, code, returnUrl = ReturnUrl });
    }
}
