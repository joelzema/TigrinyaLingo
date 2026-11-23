using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace TigrinyaLingo.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ConfirmEmailModel(UserManager<IdentityUser> users) : PageModel
{
    public bool Succeeded { get; private set; }
    public string ReturnUrl { get; private set; } = "/";

    public async Task OnGetAsync(string? userId = null, string? code = null, string? returnUrl = null)
    {
        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        if (userId is null || code is null) return;
        var user = await users.FindByIdAsync(userId);
        if (user is null) return;
        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            Succeeded = (await users.ConfirmEmailAsync(user, decoded)).Succeeded;
        }
        catch (FormatException) { return; }
        if (Succeeded)
        {
            TempData.Remove("LocalRegistration.UserId");
            TempData.Remove("LocalRegistration.Email");
            TempData.Remove("LocalRegistration.Code");
        }
    }
}
