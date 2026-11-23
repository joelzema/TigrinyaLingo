using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Domain.Entities;

namespace TigrinyaLingo.Models;

public class TenseFieldsInput : IValidatableObject
{
    [StringLength(80, ErrorMessage = "Gruppnamnet får vara högst 80 tecken."), Display(Name = "Träningsgrupp (verb och person)")]
    public string? TenseGroup { get; set; }
    [EnumDataType(typeof(TimeReference), ErrorMessage = "Välj en giltig tidsform."), Display(Name = "Tidsform")]
    public TimeReference TimeReference { get; set; }
    [StringLength(500, ErrorMessage = "Texten får vara högst 500 tecken."), Display(Name = "När används frasen? (svenskt sammanhang)")]
    public string? TenseContext { get; set; }
    [StringLength(500, ErrorMessage = "Texten får vara högst 500 tecken."), Display(Name = "Förklaring eller språknotering (valfri)")]
    public string? TenseNotes { get; set; }
    [Display(Name = "Frasen och sammanhanget är språkgranskade")]
    public bool TenseReviewed { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var any = TimeReference != TimeReference.None || TenseReviewed || !string.IsNullOrWhiteSpace(TenseContext) || !string.IsNullOrWhiteSpace(TenseNotes);
        if (any && string.IsNullOrWhiteSpace(TenseGroup))
            yield return new("Ange en träningsgrupp, till exempel Äta – jag.", new[] { nameof(TenseGroup) });
        if (TenseReviewed && TimeReference == TimeReference.None)
            yield return new("Välj nutid, dåtid eller framtid innan frasen markeras som granskad.", new[] { nameof(TimeReference) });
        if (TenseReviewed && string.IsNullOrWhiteSpace(TenseContext))
            yield return new("Beskriv en tydlig situation där just den här formen används.", new[] { nameof(TenseContext) });
        foreach (var (name, text) in new[] { (nameof(TenseGroup), TenseGroup), (nameof(TenseContext), TenseContext), (nameof(TenseNotes), TenseNotes) })
            if (text is not null && Regex.IsMatch(text, @"[\u1200-\u139F\u2D80-\u2DDF\uAB00-\uAB2F]"))
                yield return new("Använd svenska och latinska bokstäver i tidsformsstödet.", new[] { name });
    }
}

public record TenseHint(string Label, string Context, string? Notes);
public record TenseWordViewModel(int Id, int LessonId, string LessonTitle, string LatinText, string Meaning,
    PhraseAudience Audience, TenseHint? Hint);
public record TenseGroupViewModel(int Id, string Title, List<TenseWordViewModel> Words, List<string> Issues,
    bool LessonsCompleted, bool CanPractice);
public record TenseHomeViewModel(List<TenseGroupViewModel> Groups, List<TenseGroupViewModel> Drafts, bool CanEdit, string? Message);

public static class TenseGuide
{
    public static string? Label(TimeReference time) => time switch {
        TimeReference.Present => "Nutid", TimeReference.Past => "Dåtid", TimeReference.Future => "Framtid", _ => null
    };
    public static bool HasMetadata(Question question) => question.TimeReference != TimeReference.None || question.TenseReviewed
        || !string.IsNullOrWhiteSpace(question.TenseGroup) || !string.IsNullOrWhiteSpace(question.TenseContext) || !string.IsNullOrWhiteSpace(question.TenseNotes);
    public static bool IsReviewed(Question question) => question.TenseReviewed && Label(question.TimeReference) is not null
        && !string.IsNullOrWhiteSpace(question.TenseGroup) && !string.IsNullOrWhiteSpace(question.TenseContext);
    public static bool IsPublished(Question question) => !HasMetadata(question) || IsReviewed(question);
    public static TenseHint? For(Question question) => IsReviewed(question)
        ? new(Label(question.TimeReference)!, question.TenseContext!, question.TenseNotes) : null;
    public static void Apply(TenseFieldsInput input, Question question)
    {
        question.TenseGroup = string.IsNullOrWhiteSpace(input.TenseGroup) ? null : Regex.Replace(input.TenseGroup.Trim(), @"\s+", " ");
        question.TimeReference = input.TimeReference;
        question.TenseContext = string.IsNullOrWhiteSpace(input.TenseContext) ? null : input.TenseContext.Trim();
        question.TenseNotes = string.IsNullOrWhiteSpace(input.TenseNotes) ? null : input.TenseNotes.Trim();
        question.TenseReviewed = input.TenseReviewed;
    }
    public static string? Explanation(Question question)
    {
        var parts = new List<string>();
        if (PhraseUsageGuide.For(question.LatinText, question.Audience) is { } usage)
        {
            parts.Add(usage.Label + ".");
            if (usage.OtherForm is not null) parts.Add(usage.OtherLabel + ": " + usage.OtherForm + ".");
        }
        if (For(question) is { } time)
        {
            parts.Add(time.Label + ": " + time.Context);
            if (!string.IsNullOrWhiteSpace(time.Notes)) parts.Add(time.Notes);
        }
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }
}

public record TenseWritingTurn(int GroupId, string Title, int Step, string Context, string Meaning,
    string Phrase, string Label, string? Attempt, bool Correct, string? Feedback);
