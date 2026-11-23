using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TigrinyaLingo.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    [BindProperty] public InputModel Input { get; set; } = new();
    public string ReturnUrl { get; set; } = "/";

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        if (!ModelState.IsValid) return Page();
        var result = await signInManager.PasswordSignInAsync(Input.Email.Trim(), Input.Password,
            Input.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded) return LocalRedirect(ReturnUrl);
        if (result.RequiresTwoFactor)
            return RedirectToPage("LoginWith2fa", new { returnUrl = ReturnUrl, rememberMe = Input.RememberMe });
        // Samma hjälptext för okänt konto, fel lösenord och obekräftat konto.
        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "För många inloggningsförsök. Vänta en stund och försök igen."
            : "Det gick inte att logga in. Kontrollera mejladress och lösenord. Om kontot är nytt behöver du först bekräfta det på sidan efter registreringen.");
        return Page();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Skriv din mejladress.")]
        [EmailAddress(ErrorMessage = "Skriv en giltig mejladress.")]
        [Display(Name = "Mejladress")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Skriv ditt lösenord.")]
        [DataType(DataType.Password)]
        [Display(Name = "Lösenord")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Kom ihåg mig på den här enheten")]
        public bool RememberMe { get; set; }
    }
}
