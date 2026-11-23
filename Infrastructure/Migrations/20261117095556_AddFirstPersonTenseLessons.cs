using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFirstPersonTenseLessons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Lessons",
                columns: new[] { "Id", "CourseId", "Order", "Title" },
                values: new object[,]
                {
                    { 2001, 1, 17, "Äta – jag i tre tidsformer" },
                    { 2002, 1, 18, "Gå – jag i tre tidsformer" },
                    { 2003, 1, 19, "Prata – jag i tre tidsformer" }
                });

            migrationBuilder.InsertData(
                table: "Questions",
                columns: new[] { "Id", "Audience", "AudioFileName", "LatinText", "LessonId", "TenseContext", "TenseGroup", "TenseNotes", "TenseReviewed", "Text", "TimeReference" },
                values: new object[,]
                {
                    { 2001, 4, null, "ane ybel'e aleku", 2001, "Du sitter med maten och äter just nu. Berätta vad du håller på med.", "Äta – jag", "Ane betyder jag. Formen används oavsett talarens kön. Här används pågående nutid med aleku. Det är inte samma sak som att säga vad man brukar göra. Källunderlag: The Essential Guide to Tigrinya (2019), tempusavsnitt och verblista. Egen lärargranskning återstår.", true, "", 1 },
                    { 2002, 4, null, "ane beli'e", 2001, "Måltiden är avslutad. Berätta att du åt tidigare.", "Äta – jag", "Ane betyder jag. Formen används oavsett talarens kön. Här handlar det om en avslutad händelse. Samma form kan i ett annat sammanhang motsvara svenskans har gjort. Källunderlag: The Essential Guide to Tigrinya (2019), tempusavsnitt och verblista. Egen lärargranskning återstår.", true, "", 2 },
                    { 2003, 4, null, "ane kbel'e eye", 2001, "Du har inte börjat äta. Berätta att du ska äta senare.", "Äta – jag", "Ane betyder jag. Formen används oavsett talarens kön. Här används framtidsformen med k- och eye för något som ska hända senare. Källunderlag: The Essential Guide to Tigrinya (2019), tempusavsnitt och verblista. Egen lärargranskning återstår.", true, "", 3 },
                    { 2004, 4, null, "ane ykeyd aleku", 2002, "Du har gett dig av och är på väg just nu. Berätta det.", "Gå – jag", "Ane betyder jag. Formen används oavsett talarens kön. Här används pågående nutid med aleku. Det är inte samma sak som att säga vad man brukar göra. Gå betyder här att bege sig eller förflytta sig; formen säger inte i sig att man går till fots. Källunderlag: The Essential Guide to Tigrinya (2019), tempusavsnitt och verblista. Egen lärargranskning återstår.", true, "", 1 },
                    { 2005, 4, null, "ane keyde", 2002, "Besöket är över. Berätta att du gick dit tidigare.", "Gå – jag", "Ane betyder jag. Formen används oavsett talarens kön. Här handlar det om en avslutad händelse. Samma form kan i ett annat sammanhang motsvara svenskans har gjort. Gå betyder här att bege sig eller förflytta sig; formen säger inte i sig att man går till fots. Källunderlag: The Essential Guide to Tigrinya (2019), tempusavsnitt och verblista. Egen lärargranskning återstår.", true, "", 2 },
                    { 2006, 4, null, "ane kkeyd eye", 2002, "Du är fortfarande hemma. Berätta att du ska gå senare.", "Gå – jag", "Ane betyder jag. Formen används oavsett talarens kön. Här används framtidsformen med k- och eye för något som ska hända senare. Gå betyder här att bege sig eller förflytta sig; formen säger inte i sig att man går till fots. Källunderlag: The Essential Guide to Tigrinya (2019), tempusavsnitt och verblista. Egen lärargranskning återstår.", true, "", 3 },
                    { 2007, 4, null, "ane yzareb aleku", 2003, "Samtalet pågår och du pratar just nu. Berätta vad du håller på med.", "Prata – jag", "Ane betyder jag. Formen används oavsett talarens kön. Här används pågående nutid med aleku. Det är inte samma sak som att säga vad man brukar göra. Källunderlag: The Essential Guide to Tigrinya (2019), tempusavsnitt och verblista. Egen lärargranskning återstår.", true, "", 1 },
                    { 2008, 4, null, "ane tezaribe", 2003, "Samtalet är avslutat. Berätta att du pratade tidigare.", "Prata – jag", "Ane betyder jag. Formen används oavsett talarens kön. Här handlar det om en avslutad händelse. Samma form kan i ett annat sammanhang motsvara svenskans har gjort. Källunderlag: The Essential Guide to Tigrinya (2019), tempusavsnitt och verblista. Egen lärargranskning återstår.", true, "", 2 },
                    { 2009, 4, null, "ane kzareb eye", 2003, "Samtalet har inte börjat. Berätta att du ska prata senare.", "Prata – jag", "Ane betyder jag. Formen används oavsett talarens kön. Här används framtidsformen med k- och eye för något som ska hända senare. Källunderlag: The Essential Guide to Tigrinya (2019), tempusavsnitt och verblista. Egen lärargranskning återstår.", true, "", 3 }
                });

            migrationBuilder.InsertData(
                table: "Choices",
                columns: new[] { "Id", "IsCorrect", "QuestionId", "Text" },
                values: new object[,]
                {
                    { 8001, true, 2001, "Jag äter just nu" },
                    { 8002, false, 2001, "Jag åt" },
                    { 8003, false, 2001, "Jag ska äta" },
                    { 8004, false, 2001, "Jag är på väg" },
                    { 8005, false, 2002, "Jag äter just nu" },
                    { 8006, true, 2002, "Jag åt" },
                    { 8007, false, 2002, "Jag ska äta" },
                    { 8008, false, 2002, "Jag gick" },
                    { 8009, false, 2003, "Jag äter just nu" },
                    { 8010, false, 2003, "Jag åt" },
                    { 8011, true, 2003, "Jag ska äta" },
                    { 8012, false, 2003, "Jag ska gå" },
                    { 8013, true, 2004, "Jag är på väg" },
                    { 8014, false, 2004, "Jag gick" },
                    { 8015, false, 2004, "Jag ska gå" },
                    { 8016, false, 2004, "Jag pratar just nu" },
                    { 8017, false, 2005, "Jag är på väg" },
                    { 8018, true, 2005, "Jag gick" },
                    { 8019, false, 2005, "Jag ska gå" },
                    { 8020, false, 2005, "Jag pratade" },
                    { 8021, false, 2006, "Jag är på väg" },
                    { 8022, false, 2006, "Jag gick" },
                    { 8023, true, 2006, "Jag ska gå" },
                    { 8024, false, 2006, "Jag ska prata" },
                    { 8025, true, 2007, "Jag pratar just nu" },
                    { 8026, false, 2007, "Jag pratade" },
                    { 8027, false, 2007, "Jag ska prata" },
                    { 8028, false, 2007, "Jag äter just nu" },
                    { 8029, false, 2008, "Jag pratar just nu" },
                    { 8030, true, 2008, "Jag pratade" },
                    { 8031, false, 2008, "Jag ska prata" },
                    { 8032, false, 2008, "Jag åt" },
                    { 8033, false, 2009, "Jag pratar just nu" },
                    { 8034, false, 2009, "Jag pratade" },
                    { 8035, true, 2009, "Jag ska prata" },
                    { 8036, false, 2009, "Jag ska äta" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8001);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8002);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8003);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8004);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8005);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8006);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8007);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8008);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8009);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8010);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8011);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8012);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8013);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8014);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8015);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8016);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8017);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8018);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8019);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8020);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8021);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8022);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8023);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8024);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8025);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8026);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8027);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8028);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8029);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8030);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8031);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8032);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8033);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8034);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8035);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 8036);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 2001);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 2002);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 2003);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 2004);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 2005);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 2006);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 2007);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 2008);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 2009);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 2001);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 2002);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 2003);
        }
    }
}
