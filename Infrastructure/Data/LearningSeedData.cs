using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data
{
    internal static class LearningSeedData
    {
        public static void Configure(ModelBuilder builder)
        {
            ExtendedLearningSeedData.Configure(builder);
            ConversationLearningSeedData.Configure(builder);
            TenseLearningSeedData.Configure(builder);

            // Fasta ID:n låter EF lägga in innehållet en gång via en migration.
            // Fraskälla: https://www.omniglot.com/language/phrases/tigrinya.php
            builder.Entity<Course>().HasData(new Course
            {
                Id = 1,
                Title = "Tigrinja för nybörjare",
                Description = "Lär dig säga vanliga ord och uttryck på tigrinja med hjälp av latinska bokstäver.",
                Category = "Grunder"
            });
            builder.Entity<Lesson>().HasData(new Lesson
            {
                Id = 1, CourseId = 1, Title = "Dina första hälsningar", Order = 1
            });
            builder.Entity<Lesson>().HasData(new Lesson { Id = 2, CourseId = 1, Title = "Presentera dig", Order = 2 });
            builder.Entity<Lesson>().HasData(new Lesson { Id = 3, CourseId = 1, Title = "Enkla vardagsfraser", Order = 3 });
            builder.Entity<Question>().HasData(
                new Question { Id = 1, LessonId = 1, Text = "ሰላም", LatinText = "selam", AudioFileName = null },
                new Question { Id = 2, LessonId = 1, Text = "መርሓባ", LatinText = "merhaba", AudioFileName = null },
                new Question { Id = 3, LessonId = 1, Text = "የቐንየለይ", LatinText = "yekenyeley", AudioFileName = null },
                new Question { Id = 4, LessonId = 1, Text = "ሰላማት", LatinText = "selamat", AudioFileName = null });

            // Text är ordet. Rätt svarsalternativ är dess svenska betydelse.
            // Quizet rättar mot IsCorrect i databasen.
            // Översättningarna är naturliga svenska betydelser. Tilltalsformer förklaras separat.
            builder.Entity<Question>().HasData(
                new Question { Id = 5, LessonId = 2, Text = "ስመይ … ይበሃል", LatinText = "ane shimey ... behal", AudioFileName = null },
                new Question { Id = 6, LessonId = 2, Text = "መን’ዩ ሸምካ", LatinText = "menyu shimka", AudioFileName = null },
                new Question { Id = 7, LessonId = 2, Text = "መን’ዩ ሸምኪ", LatinText = "menyu shimki", AudioFileName = null },
                new Question { Id = 8, LessonId = 2, Text = "ኣነ ካብ … መጽየ", LatinText = "ane kab … metsiye", AudioFileName = null });

            builder.Entity<Choice>().HasData(
                new Choice { Id = 17, QuestionId = 5, Text = "Jag heter …", IsCorrect = true },
                new Choice { Id = 18, QuestionId = 5, Text = "Vad heter du?" },
                new Choice { Id = 19, QuestionId = 5, Text = "Hej" },
                new Choice { Id = 20, QuestionId = 5, Text = "Jag kommer från …" },
                new Choice { Id = 21, QuestionId = 6, Text = "Jag heter …" },
                new Choice { Id = 22, QuestionId = 6, Text = "Vad heter du?", IsCorrect = true },
                new Choice { Id = 23, QuestionId = 6, Text = "Hej" },
                new Choice { Id = 24, QuestionId = 6, Text = "Jag kommer från …" },
                new Choice { Id = 25, QuestionId = 7, Text = "Jag heter …" },
                new Choice { Id = 26, QuestionId = 7, Text = "Hej" },
                new Choice { Id = 27, QuestionId = 7, Text = "Vad heter du?", IsCorrect = true },
                new Choice { Id = 28, QuestionId = 7, Text = "Jag kommer från …" },
                new Choice { Id = 29, QuestionId = 8, Text = "Jag heter …" },
                new Choice { Id = 30, QuestionId = 8, Text = "Vad heter du?" },
                new Choice { Id = 31, QuestionId = 8, Text = "Hej" },
                new Choice { Id = 32, QuestionId = 8, Text = "Jag kommer från …", IsCorrect = true });
            builder.Entity<Choice>().HasData(
                new Choice { Id = 1, QuestionId = 1, Text = "Hej", IsCorrect = true },
                new Choice { Id = 2, QuestionId = 1, Text = "Välkommen" },
                new Choice { Id = 3, QuestionId = 1, Text = "Tack" },
                new Choice { Id = 4, QuestionId = 1, Text = "Hej då" },
                new Choice { Id = 5, QuestionId = 2, Text = "Hej" },
                new Choice { Id = 6, QuestionId = 2, Text = "Välkommen", IsCorrect = true },
                new Choice { Id = 7, QuestionId = 2, Text = "Tack" },
                new Choice { Id = 8, QuestionId = 2, Text = "Hej då" },
                new Choice { Id = 9, QuestionId = 3, Text = "Hej" },
                new Choice { Id = 10, QuestionId = 3, Text = "Välkommen" },
                new Choice { Id = 11, QuestionId = 3, Text = "Tack", IsCorrect = true },
                new Choice { Id = 12, QuestionId = 3, Text = "Hej då" },
                new Choice { Id = 13, QuestionId = 4, Text = "Hej" },
                new Choice { Id = 14, QuestionId = 4, Text = "Välkommen" },
                new Choice { Id = 15, QuestionId = 4, Text = "Tack" },
                new Choice { Id = 16, QuestionId = 4, Text = "Hej då", IsCorrect = true });

            // Naturliga vardagsfraser. Originalskriften sparas som källa men visas inte i appen.
            builder.Entity<Question>().HasData(
                new Question { Id = 9, LessonId = 3, Text = "ተረዲኡኒ", LatinText = "teredi’uni", AudioFileName = null },
                new Question { Id = 10, LessonId = 3, Text = "ኣይተረድኣኒን", LatinText = "aytered’anin", AudioFileName = null },
                new Question { Id = 11, LessonId = 3, Text = "ኣይፈለጥኩን", LatinText = "ayfeletikun", AudioFileName = null },
                new Question { Id = 12, LessonId = 3, Text = "ይቅሬታ", LatinText = "yiqreta", AudioFileName = null });
            builder.Entity<Choice>().HasData(
                new Choice { Id = 33, QuestionId = 9, Text = "Jag förstår", IsCorrect = true },
                new Choice { Id = 34, QuestionId = 9, Text = "Jag förstår inte" },
                new Choice { Id = 35, QuestionId = 9, Text = "Jag vet inte" },
                new Choice { Id = 36, QuestionId = 9, Text = "Förlåt" },
                new Choice { Id = 37, QuestionId = 10, Text = "Jag förstår" },
                new Choice { Id = 38, QuestionId = 10, Text = "Jag förstår inte", IsCorrect = true },
                new Choice { Id = 39, QuestionId = 10, Text = "Jag vet inte" },
                new Choice { Id = 40, QuestionId = 10, Text = "Förlåt" },
                new Choice { Id = 41, QuestionId = 11, Text = "Jag förstår" },
                new Choice { Id = 42, QuestionId = 11, Text = "Jag förstår inte" },
                new Choice { Id = 43, QuestionId = 11, Text = "Jag vet inte", IsCorrect = true },
                new Choice { Id = 44, QuestionId = 11, Text = "Förlåt" },
                new Choice { Id = 45, QuestionId = 12, Text = "Jag förstår" },
                new Choice { Id = 46, QuestionId = 12, Text = "Jag förstår inte" },
                new Choice { Id = 47, QuestionId = 12, Text = "Jag vet inte" },
                new Choice { Id = 48, QuestionId = 12, Text = "Förlåt", IsCorrect = true });
        }
    }
}
