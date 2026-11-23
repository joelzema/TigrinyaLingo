using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace TigrinyaLingo.Models;

public class LearningPreferenceInput
{
    [Required(ErrorMessage = "Välj det alternativ som passar dig bäst.")]
    [EnumDataType(typeof(LearningExperience), ErrorMessage = "Välj ett av de tre alternativen.")]
    public LearningExperience? Experience { get; set; }
    public string ReturnUrl { get; set; } = "/Learn";
}
