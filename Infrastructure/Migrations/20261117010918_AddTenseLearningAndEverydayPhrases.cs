using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenseLearningAndEverydayPhrases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TenseContext",
                table: "Questions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenseGroup",
                table: "Questions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenseNotes",
                table: "Questions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TenseReviewed",
                table: "Questions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TimeReference",
                table: "Questions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.InsertData(
                table: "Lessons",
                columns: new[] { "Id", "CourseId", "Order", "Title" },
                values: new object[,]
                {
                    { 1001, 1, 14, "Fråga om språk" },
                    { 1002, 1, 15, "Korta svar och följdfrågor" },
                    { 1003, 1, 16, "Svara på hälsningar och säga hej då" }
                });

            migrationBuilder.InsertData(
                table: "Questions",
                columns: new[] { "Id", "Audience", "AudioFileName", "LatinText", "LessonId", "TenseContext", "TenseGroup", "TenseNotes", "TenseReviewed", "Text", "TimeReference" },
                values: new object[,]
                {
                    { 1001, 1, null, "englizgna tzarebdo", 1001, null, null, null, false, "", 0 },
                    { 1002, 2, null, "englizgna tzarebido", 1001, null, null, null, false, "", 0 },
                    { 1003, 1, null, "tigrigna tzarebdo", 1001, null, null, null, false, "", 0 },
                    { 1004, 2, null, "tigrigna tzarebido", 1001, null, null, null, false, "", 0 },
                    { 1005, 4, null, "ewe", 1002, null, null, null, false, "", 0 },
                    { 1006, 4, null, "ayfal", 1002, null, null, null, false, "", 0 },
                    { 1007, 1, null, "bwegenka ke", 1002, null, null, null, false, "", 0 },
                    { 1008, 2, null, "bwegenki ke", 1002, null, null, null, false, "", 0 },
                    { 1009, 4, null, "tsbuq aleku", 1003, null, null, null, false, "", 0 },
                    { 1010, 4, null, "tsbuq", 1003, null, null, null, false, "", 0 },
                    { 1011, 1, null, "dehan kun", 1003, null, null, null, false, "", 0 },
                    { 1012, 2, null, "dehan kuni", 1003, null, null, null, false, "", 0 },
                    { 1013, 3, null, "dehan kunu", 1003, null, null, null, false, "", 0 },
                    { 1014, 4, null, "dahray yerakbena", 1003, null, null, null, false, "", 0 }
                });

            migrationBuilder.InsertData(
                table: "Choices",
                columns: new[] { "Id", "IsCorrect", "QuestionId", "Text" },
                values: new object[,]
                {
                    { 4001, true, 1001, "Pratar du engelska?" },
                    { 4002, false, 1001, "Pratar du tigrinja?" },
                    { 4003, false, 1001, "Ja" },
                    { 4004, false, 1001, "Nej" },
                    { 4005, true, 1002, "Pratar du engelska?" },
                    { 4006, false, 1002, "Ja" },
                    { 4007, false, 1002, "Nej" },
                    { 4008, false, 1002, "Och du?" },
                    { 4009, true, 1003, "Pratar du tigrinja?" },
                    { 4010, false, 1003, "Nej" },
                    { 4011, false, 1003, "Och du?" },
                    { 4012, false, 1003, "Jag mår bra" },
                    { 4013, true, 1004, "Pratar du tigrinja?" },
                    { 4014, false, 1004, "Pratar du engelska?" },
                    { 4015, false, 1004, "Ja" },
                    { 4016, false, 1004, "Nej" },
                    { 4017, true, 1005, "Ja" },
                    { 4018, false, 1005, "Pratar du tigrinja?" },
                    { 4019, false, 1005, "Nej" },
                    { 4020, false, 1005, "Och du?" },
                    { 4021, true, 1006, "Nej" },
                    { 4022, false, 1006, "Ja" },
                    { 4023, false, 1006, "Och du?" },
                    { 4024, false, 1006, "Jag mår bra" },
                    { 4025, true, 1007, "Och du?" },
                    { 4026, false, 1007, "Pratar du engelska?" },
                    { 4027, false, 1007, "Pratar du tigrinja?" },
                    { 4028, false, 1007, "Ja" },
                    { 4029, true, 1008, "Och du?" },
                    { 4030, false, 1008, "Pratar du tigrinja?" },
                    { 4031, false, 1008, "Ja" },
                    { 4032, false, 1008, "Nej" },
                    { 4033, true, 1009, "Jag mår bra" },
                    { 4034, false, 1009, "Ja" },
                    { 4035, false, 1009, "Nej" },
                    { 4036, false, 1009, "Och du?" },
                    { 4037, true, 1010, "Bra" },
                    { 4038, false, 1010, "Pratar du engelska?" },
                    { 4039, false, 1010, "Pratar du tigrinja?" },
                    { 4040, false, 1010, "Ja" },
                    { 4041, true, 1011, "Hej då" },
                    { 4042, false, 1011, "Pratar du tigrinja?" },
                    { 4043, false, 1011, "Ja" },
                    { 4044, false, 1011, "Nej" },
                    { 4045, true, 1012, "Hej då" },
                    { 4046, false, 1012, "Ja" },
                    { 4047, false, 1012, "Nej" },
                    { 4048, false, 1012, "Och du?" },
                    { 4049, true, 1013, "Hej då" },
                    { 4050, false, 1013, "Pratar du engelska?" },
                    { 4051, false, 1013, "Pratar du tigrinja?" },
                    { 4052, false, 1013, "Ja" },
                    { 4053, true, 1014, "Vi ses" },
                    { 4054, false, 1014, "Pratar du tigrinja?" },
                    { 4055, false, 1014, "Ja" },
                    { 4056, false, 1014, "Nej" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4001);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4002);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4003);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4004);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4005);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4006);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4007);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4008);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4009);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4010);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4011);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4012);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4013);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4014);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4015);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4016);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4017);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4018);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4019);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4020);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4021);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4022);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4023);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4024);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4025);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4026);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4027);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4028);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4029);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4030);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4031);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4032);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4033);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4034);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4035);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4036);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4037);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4038);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4039);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4040);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4041);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4042);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4043);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4044);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4045);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4046);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4047);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4048);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4049);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4050);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4051);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4052);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4053);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4054);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4055);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 4056);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1004);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1005);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1006);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1007);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1008);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1009);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1010);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1011);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1012);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1013);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 1014);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 1001);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 1002);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 1003);

            migrationBuilder.DropColumn(
                name: "TenseContext",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TenseGroup",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TenseNotes",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TenseReviewed",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "TimeReference",
                table: "Questions");
        }
    }
}
