using System.Text.RegularExpressions;
using Domain.Entities;
namespace TigrinyaLingo.Models;

public record PhraseUsage(string Label, string? OtherForm = null, string? OtherLabel = null);
public record PhraseUsageDisplay(string LatinText, PhraseAudience Audience = PhraseAudience.Automatic, bool ShowOtherForm = false);

// Matcha hela granskade uttryck, aldrig bara en ändelse eller ett fråge-ID.
// Källor och avgränsningar: LEARNING_CONTENT_SOURCES.md.
public static class PhraseUsageGuide
{
    public const string Explanation = "(m) = till en man, (f) = till en kvinna. Här gäller markeringen personen du talar till, inte den som säger frasen. Samma svenska betydelse kan alltså ha två tigrinjaformer.";
    private record Entry(PhraseAudience Audience, string? OtherForm, PhraseAudience OtherAudience);
    private static string Key(string text) => Regex.Replace(text.Normalize().ToLowerInvariant()
        .Replace("’", "'").Replace("‘", "'").Replace("ˋ", "'").Replace("'", ""), @"\s+", " ").Trim().TrimEnd('?', '!', '.', '…').Trim();
    public static string FormKey(string text) => Key(text);
    public static bool SameForm(string left, string right) => Key(left) == Key(right);
    private static readonly Dictionary<string, Entry> Entries = Build();
    private static Dictionary<string, Entry> Build()
    {
        var entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        void Pair(string man, string woman)
        {
            entries.Add(Key(man), new(PhraseAudience.Man, woman, PhraseAudience.Woman));
            entries.Add(Key(woman), new(PhraseAudience.Woman, man, PhraseAudience.Man));
        }
        Pair("menyu shimka", "menyu shimki");
        Pair("kemey aleka", "kemey aleki");
        Pair("kabey adi metsika", "kabey adi metsiki");
        Pair("kemey hadirka", "kemey hadirki");
        Pair("kemey wu’elka", "kemey wu’elki");
        Pair("kemey amsika", "kemey amsiki");
        Pair("kes elka tezareb bejaka", "kes elki tezarebi bejaki");
        Pair("eske digemeley", "eske digemiley");
        Pair("tsehafo bejaka", "tsehafiyo bejaki");
        Pair("bejaka", "bejaki");
        Pair("genzebka", "genzebki");
        Pair("englizgna tzarebdo", "englizgna tzarebido");
        Pair("tigrigna tzarebdo", "tigrigna tzarebido");
        Pair("bwegenka ke", "bwegenki ke");
        Pair("dehan kun", "dehan kuni");
        entries.Add(Key("dehan kunu"), new(PhraseAudience.SeveralOrPolite, null, PhraseAudience.Automatic));
        entries.Add(Key("dehan w’alu"), new(PhraseAudience.SeveralOrPolite, null, PhraseAudience.Automatic));
        // Eto är uppmaning i manligt ental; ingen ogranskad resevariant läggs till.
        entries.Add(Key("bdehan eto"), new(PhraseAudience.Man, null, PhraseAudience.Automatic));
        return entries;
    }
    public static string? Label(PhraseAudience audience) => audience switch
    {
        PhraseAudience.Man => "(m) Till en man",
        PhraseAudience.Woman => "(f) Till en kvinna",
        PhraseAudience.SeveralOrPolite => "Till flera / artigt tilltal",
        PhraseAudience.SameForBoth => "Samma form till man och kvinna",
        _ => null
    };
    public static PhraseUsage? For(string latinText, PhraseAudience audience = PhraseAudience.Automatic)
    {
        Entries.TryGetValue(Key(latinText), out var entry);
        var resolved = audience == PhraseAudience.Automatic ? entry?.Audience ?? PhraseAudience.Automatic : audience;
        var label = Label(resolved);
        if (label is null) return null; // Okända former är inte automatiskt könsneutrala.
        return new(label, entry?.Audience == resolved ? entry.OtherForm : null,
            entry?.Audience == resolved ? Label(entry.OtherAudience) : null);
    }
    public static string WithLabel(string text, PhraseAudience audience = PhraseAudience.Automatic)
        => For(text, audience) is { } usage ? text + " · " + usage.Label : text;
}
