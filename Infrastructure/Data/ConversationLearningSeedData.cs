using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

// Nya ID:n ligger i ett eget intervall; befintliga fraser uppdateras aldrig här.
// Språkunderlag och latinsk notation: LEARNING_CONTENT_SOURCES.md.
internal static class ConversationLearningSeedData
{
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<Lesson>().HasData(
            new Lesson { Id = 1001, CourseId = 1, Order = 14, Title = "Fråga om språk" },
            new Lesson { Id = 1002, CourseId = 1, Order = 15, Title = "Korta svar och följdfrågor" },
            new Lesson { Id = 1003, CourseId = 1, Order = 16, Title = "Svara på hälsningar och säga hej då" });
        var rows = new[] {
            (1001, "englizgna tzarebdo", "Pratar du engelska?", PhraseAudience.Man),
            (1001, "englizgna tzarebido", "Pratar du engelska?", PhraseAudience.Woman),
            (1001, "tigrigna tzarebdo", "Pratar du tigrinja?", PhraseAudience.Man),
            (1001, "tigrigna tzarebido", "Pratar du tigrinja?", PhraseAudience.Woman),
            (1002, "ewe", "Ja", PhraseAudience.SameForBoth),
            (1002, "ayfal", "Nej", PhraseAudience.SameForBoth),
            (1002, "bwegenka ke", "Och du?", PhraseAudience.Man),
            (1002, "bwegenki ke", "Och du?", PhraseAudience.Woman),
            (1003, "tsbuq aleku", "Jag mår bra", PhraseAudience.SameForBoth),
            (1003, "tsbuq", "Bra", PhraseAudience.SameForBoth),
            (1003, "dehan kun", "Hej då", PhraseAudience.Man),
            (1003, "dehan kuni", "Hej då", PhraseAudience.Woman),
            (1003, "dehan kunu", "Hej då", PhraseAudience.SeveralOrPolite),
            (1003, "dahray yerakbena", "Vi ses", PhraseAudience.SameForBoth)
        };
        var meanings = new[] { "Pratar du engelska?", "Pratar du tigrinja?", "Ja", "Nej", "Och du?", "Jag mår bra", "Bra", "Hej då", "Vi ses" };
        for (var index = 0; index < rows.Length; index++)
        {
            var (lesson, latin, meaning, audience) = rows[index];
            var id = 1001 + index;
            builder.Entity<Question>().HasData(new Question { Id = id, LessonId = lesson, LatinText = latin, Audience = audience });
            var options = new[] { meaning }.Concat(meanings.Where(item => item != meaning).Skip(index % 3).Take(3)).ToArray();
            for (var option = 0; option < 4; option++)
                builder.Entity<Choice>().HasData(new Choice { Id = 4001 + index * 4 + option, QuestionId = id, Text = options[option], IsCorrect = option == 0 });
        }
    }
}
