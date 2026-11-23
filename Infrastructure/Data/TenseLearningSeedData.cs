using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

// Endast nya innehålls-ID:n. Språkunderlag och avgränsningar: LEARNING_CONTENT_SOURCES.md.
internal static class TenseLearningSeedData
{
    public static void Configure(ModelBuilder builder)
    {
        var verbs = new[] {
            (Title: "Äta – jag", Forms: new[] { "ane ybel'e aleku", "ane beli'e", "ane kbel'e eye" },
                Meanings: new[] { "Jag äter just nu", "Jag åt", "Jag ska äta" },
                Contexts: new[] { "Du sitter med maten och äter just nu. Berätta vad du håller på med.", "Måltiden är avslutad. Berätta att du åt tidigare.", "Du har inte börjat äta. Berätta att du ska äta senare." }),
            (Title: "Gå – jag", Forms: new[] { "ane ykeyd aleku", "ane keyde", "ane kkeyd eye" },
                Meanings: new[] { "Jag är på väg", "Jag gick", "Jag ska gå" },
                Contexts: new[] { "Du har gett dig av och är på väg just nu. Berätta det.", "Besöket är över. Berätta att du gick dit tidigare.", "Du är fortfarande hemma. Berätta att du ska gå senare." }),
            (Title: "Prata – jag", Forms: new[] { "ane yzareb aleku", "ane tezaribe", "ane kzareb eye" },
                Meanings: new[] { "Jag pratar just nu", "Jag pratade", "Jag ska prata" },
                Contexts: new[] { "Samtalet pågår och du pratar just nu. Berätta vad du håller på med.", "Samtalet är avslutat. Berätta att du pratade tidigare.", "Samtalet har inte börjat. Berätta att du ska prata senare." })
        };
        for (var verb = 0; verb < verbs.Length; verb++)
        {
            var item = verbs[verb];
            var lessonId = 2001 + verb;
            builder.Entity<Lesson>().HasData(new Lesson { Id = lessonId, CourseId = 1, Order = 17 + verb, Title = item.Title + " i tre tidsformer" });
            for (var time = 0; time < 3; time++)
            {
                var id = 2001 + verb * 3 + time;
                var note = "Ane betyder jag. Formen används oavsett talarens kön. ";
                note += time switch {
                    0 => "Här används pågående nutid med aleku. Det är inte samma sak som att säga vad man brukar göra.",
                    1 => "Här handlar det om en avslutad händelse. Samma form kan i ett annat sammanhang motsvara svenskans har gjort.",
                    _ => "Här används framtidsformen med k- och eye för något som ska hända senare."
                };
                if (verb == 1) note += " Gå betyder här att bege sig eller förflytta sig; formen säger inte i sig att man går till fots.";
                note += " Källunderlag: The Essential Guide to Tigrinya (2019), tempusavsnitt och verblista. Egen lärargranskning återstår.";
                builder.Entity<Question>().HasData(new Question { Id = id, LessonId = lessonId,
                    LatinText = item.Forms[time], Audience = PhraseAudience.SameForBoth,
                    TenseGroup = item.Title, TimeReference = (TimeReference)(time + 1),
                    TenseContext = item.Contexts[time], TenseNotes = note, TenseReviewed = true });
                var meanings = item.Meanings.Append(verbs[(verb + 1) % verbs.Length].Meanings[time]).ToArray();
                for (var option = 0; option < 4; option++)
                    builder.Entity<Choice>().HasData(new Choice { Id = 8001 + (id - 2001) * 4 + option,
                        QuestionId = id, Text = meanings[option], IsCorrect = option == time });
            }
        }
    }
}
