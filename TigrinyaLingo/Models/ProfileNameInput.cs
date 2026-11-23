using System.ComponentModel.DataAnnotations;

namespace TigrinyaLingo.Models;

public class ProfileNameInput
{
    public const string ClaimType = "TigrinyaLingo.DisplayName";

    [Required(ErrorMessage = "Välj ett användarnamn.")]
    [RegularExpression(@"[\p{L}\p{N}_-]{2,30}", ErrorMessage = "Använd 2–30 bokstäver, siffror, bindestreck eller understreck.")]
    [Display(Name = "Användarnamn")]
    public string DisplayName { get; set; } = "";
}
