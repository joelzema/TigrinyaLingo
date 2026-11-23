using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedEverydayPhrasesLesson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Lessons",
                columns: new[] { "Id", "CourseId", "Order", "Title" },
                values: new object[] { 3, 1, 3, "Enkla vardagsfraser" });

            migrationBuilder.InsertData(
                table: "Questions",
                columns: new[] { "Id", "AudioFileName", "LatinText", "LessonId", "Text" },
                values: new object[,]
                {
                    { 9, "terediuni.mp3", "teredi’uni", 3, "ተረዲኡኒ" },
                    { 10, "ayteredanin.mp3", "aytered’anin", 3, "ኣይተረድኣኒን" },
                    { 11, "ayfeletikun.mp3", "ayfeletikun", 3, "ኣይፈለጥኩን" },
                    { 12, "yiqreta.mp3", "yiqreta", 3, "ይቅሬታ" }
                });

            migrationBuilder.InsertData(
                table: "Choices",
                columns: new[] { "Id", "IsCorrect", "QuestionId", "Text" },
                values: new object[,]
                {
                    { 33, true, 9, "Jag förstår" },
                    { 34, false, 9, "Jag förstår inte" },
                    { 35, false, 9, "Jag vet inte" },
                    { 36, false, 9, "Förlåt" },
                    { 37, false, 10, "Jag förstår" },
                    { 38, true, 10, "Jag förstår inte" },
                    { 39, false, 10, "Jag vet inte" },
                    { 40, false, 10, "Förlåt" },
                    { 41, false, 11, "Jag förstår" },
                    { 42, false, 11, "Jag förstår inte" },
                    { 43, true, 11, "Jag vet inte" },
                    { 44, false, 11, "Förlåt" },
                    { 45, false, 12, "Jag förstår" },
                    { 46, false, 12, "Jag förstår inte" },
                    { 47, false, 12, "Jag vet inte" },
                    { 48, true, 12, "Förlåt" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 3);
        }
    }
}
