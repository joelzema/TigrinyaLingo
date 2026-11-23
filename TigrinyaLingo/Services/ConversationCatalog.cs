using Domain.Entities;

namespace TigrinyaLingo.Services;

// Svenskt samtalsstöd. Tigrinjasvaren hämtas alltid från de aktuella fraserna i databasen.
// Om en svensk betydelse byts måste scenen granskas innan den kan användas igen.
internal record ConversationTurnDefinition(string Meaning, string Situation, string PartnerLine, PhraseAudience Audience = PhraseAudience.Automatic);
internal record ConversationDefinition(string Id, string Title, string Description, int LessonId, List<ConversationTurnDefinition> Turns);
internal static class ConversationCatalog
{
    public static readonly IReadOnlyList<ConversationDefinition> Scenes = new List<ConversationDefinition> {
        new("first-meeting", "Ett första möte", "Hälsa, välkomna en vän, tacka och säg hej då.", 1, new() {
            new("Hej", "Du möter en vän på stan. Hälsa tillbaka.", "Hej! Vad roligt att se dig."),
            new("Välkommen", "Ni kommer hem till dig. Välkomna din vän.", "Tack för att jag får komma på besök."),
            new("Tack", "Din vän ger dig en liten present. Visa din uppskattning.", "Den här är till dig!"),
            new("Hej då", "Besöket är slut. Säg hej då till din vän.", "Nu behöver jag gå. Vi ses!")
        }),
        new("ask-for-help", "När du behöver hjälp", "Berätta om du förstår, be om ursäkt och säg när du inte vet.", 3, new() {
            new("Förlåt", "Du behöver avbryta personen artigt innan du ställer din fråga.", "Jag är på väg till busshållplatsen."),
            new("Jag förstår inte", "Personen förklarar snabbt. Berätta att du inte förstår.", "Ta andra gatan, sedan höger efter korsningen och fortsätt förbi hållplatsen …"),
            new("Jag förstår", "Personen förklarar igen, lugnare. Nu hänger du med.", "Vi tar det en gång till: gå rakt fram. Hållplatsen ligger på höger sida."),
            new("Jag vet inte", "Personen ställer en fråga som du inte kan svaret på. Svara ärligt.", "Vet du vilket nummer bussen har?")
        }),
        new("through-the-day", "Hälsningar under dagen", "Välj en hälsning som passar tiden på dagen. Du pratar med en man.", 5, new() {
            new("God morgon", "Det är morgon och du möter din vän. Hälsa på honom.", "God morgon! Redo för en ny dag?", PhraseAudience.Man),
            new("God eftermiddag", "Ni ses igen efter lunch. Välj en eftermiddagshälsning.", "Nu är lunchen slut. Hur går dagen?", PhraseAudience.Man),
            new("God kväll", "Det har blivit kväll när ni träffas. Hälsa på din vän.", "Vad trevligt att ses här i kväll.", PhraseAudience.Man),
            new("God natt", "Dagen är slut och din vän ska sova. Önska honom god natt.", "Nu går jag och lägger mig. Vi ses i morgon!")
        })
    };
}
