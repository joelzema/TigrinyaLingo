using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace TigrinyaLingo.Models;

public class ContentEditorIndexViewModel
{
    public string Search { get; set; } = string.Empty;
    public List<ContentEditorItem> Items { get; set; } = new();
}

public class ContentEditorItem
{
    public string? TenseGroup { get; set; }
    public TimeReference TimeReference { get; set; }
    public bool TenseReviewed { get; set; }
    public int Id { get; set; }
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public string LatinText { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public bool HasAudio { get; set; }
}

public class PhraseEditInput : TenseFieldsInput
{
    [EnumDataType(typeof(PhraseAudience), ErrorMessage = "Välj ett giltigt tilltal.")]
    public PhraseAudience Audience { get; set; }

    [Required, StringLength(200, ErrorMessage = "Uttrycket får vara högst 200 tecken.")]
    [Display(Name = "Tigrinjaord eller fras")]
    public string LatinText { get; set; } = string.Empty;

    [StringLength(160)]
    [Display(Name = "Uttalsinspelning")]
    public string? AudioFileName { get; set; }

    public int CorrectChoiceId { get; set; }
    public List<PhraseChoiceInput> Choices { get; set; } = new();
    public string EditStamp { get; set; } = string.Empty;
}

public class PhraseChoiceInput
{
    public int Id { get; set; }
    [Required(ErrorMessage = "Fyll i svarsalternativet."), StringLength(200, ErrorMessage = "Svaret får vara högst 200 tecken.")]
    public string Text { get; set; } = string.Empty;
}

public class PhraseEditViewModel : PhraseEditInput
{
    public int Id { get; set; }
    public int LessonId { get; set; }
    public string LessonTitle { get; set; } = string.Empty;
    public List<string> AudioFiles { get; set; } = new();
    public bool HasConflict { get; set; }
    public string CurrentLatinText { get; set; } = string.Empty;
    public PhraseAudience CurrentAudience { get; set; }
    public string? CurrentTenseStatus { get; set; }
    public string? CurrentTenseContext { get; set; }
    public string? CurrentTenseNotes { get; set; }
    public List<string> CurrentChoices { get; set; } = new();
}

public record PhraseEditStamp(string UserId, int QuestionId, string Fingerprint, DateTime IssuedAt);
