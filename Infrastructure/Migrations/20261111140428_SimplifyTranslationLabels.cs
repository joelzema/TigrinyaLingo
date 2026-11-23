using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyTranslationLabels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 18,
                column: "Text",
                value: "Vad heter du?");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 19,
                column: "Text",
                value: "Hej");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 22,
                column: "Text",
                value: "Vad heter du?");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 23,
                column: "Text",
                value: "Hej");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 26,
                column: "Text",
                value: "Hej");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 27,
                column: "Text",
                value: "Vad heter du?");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 30,
                column: "Text",
                value: "Vad heter du?");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 31,
                column: "Text",
                value: "Hej");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 18,
                column: "Text",
                value: "Vad heter du? (till en man)");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 19,
                column: "Text",
                value: "Vad heter du? (till en kvinna)");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 22,
                column: "Text",
                value: "Vad heter du? (till en man)");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 23,
                column: "Text",
                value: "Vad heter du? (till en kvinna)");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 26,
                column: "Text",
                value: "Vad heter du? (till en man)");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 27,
                column: "Text",
                value: "Vad heter du? (till en kvinna)");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 30,
                column: "Text",
                value: "Vad heter du? (till en man)");

            migrationBuilder.UpdateData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 31,
                column: "Text",
                value: "Vad heter du? (till en kvinna)");
        }
    }
}
