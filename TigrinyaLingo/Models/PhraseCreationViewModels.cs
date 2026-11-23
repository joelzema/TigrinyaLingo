using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace TigrinyaLingo.Models;

public class PhraseCreateInput : TenseFieldsInput
{
    [EnumDataType(typeof(PhraseAudience), ErrorMessage = "Välj ett giltigt tilltal.")]
    public PhraseAudience Audience { get; set; }

    [Display(Name = "Lektion")]
    public int LessonId { get; set; }
    [Display(Name = "Kurs")]
    public int CourseId { get; set; }
    [StringLength(120, ErrorMessage = "Lektionsnamnet får vara högst 120 tecken.")]
    [Display(Name = "Namn på den nya lektionen")]
    public string? NewLessonTitle { get; set; }
    [Required(ErrorMessage = "Fyll i tigrinjaordet eller frasen."), StringLength(200)]
    [Display(Name = "Tigrinjaord eller fras")]
    public string LatinText { get; set; } = string.Empty;
    [StringLength(160)]
    [Display(Name = "Uttalsinspelning")]
    public string? AudioFileName { get; set; }
    [Required(ErrorMessage = "Markera rätt svar."), Range(0, 3, ErrorMessage = "Markera ett av de fyra svaren.")]
    public int? CorrectChoiceIndex { get; set; }
    public List<PhraseChoiceInput> Choices { get; set; } = new();
    public string CreationToken { get; set; } = string.Empty;
}

public class PhraseCreateViewModel : PhraseCreateInput
{
    public List<ContentCourseOption> Courses { get; set; } = new();
    public List<ContentLessonOption> Lessons { get; set; } = new();
    public List<string> AudioFiles { get; set; } = new();
    public string? PlacementMessage { get; set; }
}

public record ContentCourseOption(int Id, string Title);
public record ContentLessonOption(int Id, int CourseId, string Title, bool HasResults);
public record PhraseCreationStamp(string UserId, Guid Nonce, DateTime IssuedAt);
