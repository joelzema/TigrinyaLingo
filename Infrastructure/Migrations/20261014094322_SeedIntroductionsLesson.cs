using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedIntroductionsLesson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Lessons",
                columns: new[] { "Id", "CourseId", "Order", "Title" },
                values: new object[] { 2, 1, 2, "Presentera dig" });

            migrationBuilder.InsertData(
                table: "Questions",
                columns: new[] { "Id", "AudioFileName", "LatinText", "LessonId", "Text" },
                values: new object[,]
                {
                    { 5, "simey-yebehal.mp3", "simey … yebehal", 2, "ስመይ … ይበሃል" },
                    { 6, "menyu-shimka.mp3", "menyu shimka", 2, "መን’ዩ ሸምካ" },
                    { 7, "menyu-shimki.mp3", "menyu shimki", 2, "መን’ዩ ሸምኪ" },
                    { 8, "ane-kab-metsiye.mp3", "ane kab … metsiye", 2, "ኣነ ካብ … መጽየ" }
                });

            migrationBuilder.InsertData(
                table: "Choices",
                columns: new[] { "Id", "IsCorrect", "QuestionId", "Text" },
                values: new object[,]
                {
                    { 17, true, 5, "Jag heter …" },
                    { 18, false, 5, "Vad heter du? (till en man)" },
                    { 19, false, 5, "Vad heter du? (till en kvinna)" },
                    { 20, false, 5, "Jag kommer från …" },
                    { 21, false, 6, "Jag heter …" },
                    { 22, true, 6, "Vad heter du? (till en man)" },
                    { 23, false, 6, "Vad heter du? (till en kvinna)" },
                    { 24, false, 6, "Jag kommer från …" },
                    { 25, false, 7, "Jag heter …" },
                    { 26, false, 7, "Vad heter du? (till en man)" },
                    { 27, true, 7, "Vad heter du? (till en kvinna)" },
                    { 28, false, 7, "Jag kommer från …" },
                    { 29, false, 8, "Jag heter …" },
                    { 30, false, 8, "Vad heter du? (till en man)" },
                    { 31, false, 8, "Vad heter du? (till en kvinna)" },
                    { 32, true, 8, "Jag kommer från …" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 2);
        }
    }
}
