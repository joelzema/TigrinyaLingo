using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandEverydayLearning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Lessons",
                columns: new[] { "Id", "CourseId", "Order", "Title" },
                values: new object[,]
                {
                    { 4, 1, 4, "Småprata och lära känna" },
                    { 5, 1, 5, "Hälsa under dagen" },
                    { 6, 1, 6, "Håll samtalet igång" },
                    { 7, 1, 7, "Artighet i vardagen" },
                    { 8, 1, 8, "På stan och på resa" },
                    { 9, 1, 9, "Räkna från ett till fem" },
                    { 10, 1, 10, "Räkna från sex till tio" },
                    { 11, 1, 11, "Mat och dryck" },
                    { 12, 1, 12, "Familjen" },
                    { 13, 1, 13, "Viktiga frågeord" }
                });

            migrationBuilder.InsertData(
                table: "Questions",
                columns: new[] { "Id", "AudioFileName", "LatinText", "LessonId", "Text" },
                values: new object[,]
                {
                    { 13, "phrase-13.mp3", "kemey aleka", 4, "ከመይ ኣለኻ" },
                    { 14, "phrase-14.mp3", "kemey aleki", 4, "ከመይ ኣለኺ" },
                    { 15, "phrase-15.mp3", "kabey adi metsika", 4, "ካበይ ዓዲ መጺኻ" },
                    { 16, "phrase-16.mp3", "kabey adi metsiki", 4, "ካበይ ዓዲ መጺኺ" },
                    { 17, "phrase-17.mp3", "kemey hadirka", 5, "ከመይ ሓዲርካ" },
                    { 18, "phrase-18.mp3", "kemey wu’elka", 5, "ከመይ ውዒልካ" },
                    { 19, "phrase-19.mp3", "kemey amsika", 5, "ከመይ ኣምሲኻ" },
                    { 20, "phrase-20.mp3", "senay leyti", 5, "ሰናይ ለይቲ" },
                    { 21, "phrase-21.mp3", "kes elka tezareb bejaka", 6, "ቀስ ኢልካ ተዛረብ በጃኻ" },
                    { 22, "phrase-22.mp3", "eske digemeley", 6, "እስከ ድገመለይ" },
                    { 23, "phrase-23.mp3", "tsehafo bejaka", 6, "ጸሓፎ በጃኻ" },
                    { 24, "phrase-24.mp3", "ewe kurub kurub", 6, "እወ ቁሩብ ቁሩብ" },
                    { 25, "phrase-25.mp3", "bejaka", 7, "በጃኻ" },
                    { 26, "phrase-26.mp3", "genzebka", 7, "ገንዘብካ" },
                    { 27, "phrase-27.mp3", "tsbuq lela", 7, "ጽቡቕ ሌላ" },
                    { 28, "phrase-28.mp3", "dehan w’alu", 7, "ደሓን ውዓሉ" },
                    { 29, "phrase-29.mp3", "kndeyu wag’u ezi", 8, "ክንደይ’ዩ ዋግኡ እዚ" },
                    { 30, "phrase-30.mp3", "abey alo shnti bet", 8, "ኣበይ ኣሎ ሽንቲ ቤት" },
                    { 31, "phrase-31.mp3", "buruk megbi", 8, "ቡሩኽ መግቢ" },
                    { 32, "phrase-32.mp3", "bdehan eto", 8, "ብደሓን እቶ" },
                    { 33, null, "hade", 9, "ሓደ" },
                    { 34, null, "kilte", 9, "ክልተ" },
                    { 35, null, "seleste", 9, "ሰለስተ" },
                    { 36, null, "arba’te", 9, "ኣርባዕተ" },
                    { 37, null, "hamushte", 9, "ሓሙሽተ" },
                    { 38, null, "shidishte", 10, "ሽድሽተ" },
                    { 39, null, "shew’ate", 10, "ሸውዓተ" },
                    { 40, null, "shemonte", 10, "ሸሞንተ" },
                    { 41, null, "tish’ate", 10, "ትሽዓተ" },
                    { 42, null, "aserte", 10, "ዓሰርተ" },
                    { 43, null, "may", 11, "ማይ" },
                    { 44, null, "bani", 11, "ባኒ" },
                    { 45, null, "bun", 11, "ቡን" },
                    { 46, null, "tseba", 11, "ጸባ" },
                    { 47, null, "ade", 12, "ኣደ" },
                    { 48, null, "abo", 12, "ኣቦ" },
                    { 49, null, "haw", 12, "ሓው" },
                    { 50, null, "hafti", 12, "ሓፍቲ" },
                    { 51, null, "men", 13, "መን" },
                    { 52, null, "entay", 13, "እንታይ" },
                    { 53, null, "abey", 13, "ኣበይ" },
                    { 54, null, "me’as", 13, "መዓስ" }
                });

            migrationBuilder.InsertData(
                table: "Choices",
                columns: new[] { "Id", "IsCorrect", "QuestionId", "Text" },
                values: new object[,]
                {
                    { 49, true, 13, "Hur mår du?" },
                    { 50, false, 13, "Var kommer du ifrån?" },
                    { 51, false, 13, "Hej" },
                    { 52, false, 13, "Tack" },
                    { 53, false, 14, "Tack" },
                    { 54, true, 14, "Hur mår du?" },
                    { 55, false, 14, "Var kommer du ifrån?" },
                    { 56, false, 14, "Hej" },
                    { 57, false, 15, "Hej" },
                    { 58, false, 15, "Tack" },
                    { 59, true, 15, "Var kommer du ifrån?" },
                    { 60, false, 15, "Hur mår du?" },
                    { 61, false, 16, "Hur mår du?" },
                    { 62, false, 16, "Hej" },
                    { 63, false, 16, "Tack" },
                    { 64, true, 16, "Var kommer du ifrån?" },
                    { 65, true, 17, "God morgon" },
                    { 66, false, 17, "God eftermiddag" },
                    { 67, false, 17, "God kväll" },
                    { 68, false, 17, "God natt" },
                    { 69, false, 18, "God natt" },
                    { 70, true, 18, "God eftermiddag" },
                    { 71, false, 18, "God morgon" },
                    { 72, false, 18, "God kväll" },
                    { 73, false, 19, "God eftermiddag" },
                    { 74, false, 19, "God natt" },
                    { 75, true, 19, "God kväll" },
                    { 76, false, 19, "God morgon" },
                    { 77, false, 20, "God morgon" },
                    { 78, false, 20, "God eftermiddag" },
                    { 79, false, 20, "God kväll" },
                    { 80, true, 20, "God natt" },
                    { 81, true, 21, "Kan du prata långsammare?" },
                    { 82, false, 21, "Kan du säga det igen?" },
                    { 83, false, 21, "Kan du skriva ner det?" },
                    { 84, false, 21, "Ja, lite grann" },
                    { 85, false, 22, "Ja, lite grann" },
                    { 86, true, 22, "Kan du säga det igen?" },
                    { 87, false, 22, "Kan du prata långsammare?" },
                    { 88, false, 22, "Kan du skriva ner det?" },
                    { 89, false, 23, "Kan du säga det igen?" },
                    { 90, false, 23, "Ja, lite grann" },
                    { 91, true, 23, "Kan du skriva ner det?" },
                    { 92, false, 23, "Kan du prata långsammare?" },
                    { 93, false, 24, "Kan du prata långsammare?" },
                    { 94, false, 24, "Kan du säga det igen?" },
                    { 95, false, 24, "Kan du skriva ner det?" },
                    { 96, true, 24, "Ja, lite grann" },
                    { 97, true, 25, "Snälla" },
                    { 98, false, 25, "Varsågod" },
                    { 99, false, 25, "Trevligt att träffas" },
                    { 100, false, 25, "Ha en bra dag" },
                    { 101, false, 26, "Ha en bra dag" },
                    { 102, true, 26, "Varsågod" },
                    { 103, false, 26, "Snälla" },
                    { 104, false, 26, "Trevligt att träffas" },
                    { 105, false, 27, "Varsågod" },
                    { 106, false, 27, "Ha en bra dag" },
                    { 107, true, 27, "Trevligt att träffas" },
                    { 108, false, 27, "Snälla" },
                    { 109, false, 28, "Snälla" },
                    { 110, false, 28, "Varsågod" },
                    { 111, false, 28, "Trevligt att träffas" },
                    { 112, true, 28, "Ha en bra dag" },
                    { 113, true, 29, "Vad kostar det här?" },
                    { 114, false, 29, "Var är toaletten?" },
                    { 115, false, 29, "Smaklig måltid" },
                    { 116, false, 29, "Trevlig resa" },
                    { 117, false, 30, "Trevlig resa" },
                    { 118, true, 30, "Var är toaletten?" },
                    { 119, false, 30, "Vad kostar det här?" },
                    { 120, false, 30, "Smaklig måltid" },
                    { 121, false, 31, "Var är toaletten?" },
                    { 122, false, 31, "Trevlig resa" },
                    { 123, true, 31, "Smaklig måltid" },
                    { 124, false, 31, "Vad kostar det här?" },
                    { 125, false, 32, "Vad kostar det här?" },
                    { 126, false, 32, "Var är toaletten?" },
                    { 127, false, 32, "Smaklig måltid" },
                    { 128, true, 32, "Trevlig resa" },
                    { 129, true, 33, "Ett" },
                    { 130, false, 33, "Två" },
                    { 131, false, 33, "Tre" },
                    { 132, false, 33, "Fyra" },
                    { 133, false, 34, "Fyra" },
                    { 134, true, 34, "Två" },
                    { 135, false, 34, "Ett" },
                    { 136, false, 34, "Tre" },
                    { 137, false, 35, "Två" },
                    { 138, false, 35, "Fyra" },
                    { 139, true, 35, "Tre" },
                    { 140, false, 35, "Ett" },
                    { 141, false, 36, "Ett" },
                    { 142, false, 36, "Två" },
                    { 143, false, 36, "Tre" },
                    { 144, true, 36, "Fyra" },
                    { 145, true, 37, "Fem" },
                    { 146, false, 37, "Ett" },
                    { 147, false, 37, "Två" },
                    { 148, false, 37, "Tre" },
                    { 149, true, 38, "Sex" },
                    { 150, false, 38, "Sju" },
                    { 151, false, 38, "Åtta" },
                    { 152, false, 38, "Nio" },
                    { 153, false, 39, "Nio" },
                    { 154, true, 39, "Sju" },
                    { 155, false, 39, "Sex" },
                    { 156, false, 39, "Åtta" },
                    { 157, false, 40, "Sju" },
                    { 158, false, 40, "Nio" },
                    { 159, true, 40, "Åtta" },
                    { 160, false, 40, "Sex" },
                    { 161, false, 41, "Sex" },
                    { 162, false, 41, "Sju" },
                    { 163, false, 41, "Åtta" },
                    { 164, true, 41, "Nio" },
                    { 165, true, 42, "Tio" },
                    { 166, false, 42, "Sex" },
                    { 167, false, 42, "Sju" },
                    { 168, false, 42, "Åtta" },
                    { 169, true, 43, "Vatten" },
                    { 170, false, 43, "Bröd" },
                    { 171, false, 43, "Kaffe" },
                    { 172, false, 43, "Mjölk" },
                    { 173, false, 44, "Mjölk" },
                    { 174, true, 44, "Bröd" },
                    { 175, false, 44, "Vatten" },
                    { 176, false, 44, "Kaffe" },
                    { 177, false, 45, "Bröd" },
                    { 178, false, 45, "Mjölk" },
                    { 179, true, 45, "Kaffe" },
                    { 180, false, 45, "Vatten" },
                    { 181, false, 46, "Vatten" },
                    { 182, false, 46, "Bröd" },
                    { 183, false, 46, "Kaffe" },
                    { 184, true, 46, "Mjölk" },
                    { 185, true, 47, "Mamma" },
                    { 186, false, 47, "Pappa" },
                    { 187, false, 47, "Bror" },
                    { 188, false, 47, "Syster" },
                    { 189, false, 48, "Syster" },
                    { 190, true, 48, "Pappa" },
                    { 191, false, 48, "Mamma" },
                    { 192, false, 48, "Bror" },
                    { 193, false, 49, "Pappa" },
                    { 194, false, 49, "Syster" },
                    { 195, true, 49, "Bror" },
                    { 196, false, 49, "Mamma" },
                    { 197, false, 50, "Mamma" },
                    { 198, false, 50, "Pappa" },
                    { 199, false, 50, "Bror" },
                    { 200, true, 50, "Syster" },
                    { 201, true, 51, "Vem?" },
                    { 202, false, 51, "Vad?" },
                    { 203, false, 51, "Var?" },
                    { 204, false, 51, "När?" },
                    { 205, false, 52, "När?" },
                    { 206, true, 52, "Vad?" },
                    { 207, false, 52, "Vem?" },
                    { 208, false, 52, "Var?" },
                    { 209, false, 53, "Vad?" },
                    { 210, false, 53, "När?" },
                    { 211, true, 53, "Var?" },
                    { 212, false, 53, "Vem?" },
                    { 213, false, 54, "Vem?" },
                    { 214, false, 54, "Vad?" },
                    { 215, false, 54, "Var?" },
                    { 216, true, 54, "När?" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 92);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 93);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 94);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 95);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 96);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 97);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 98);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 99);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 100);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 101);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 102);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 103);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 104);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 105);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 106);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 107);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 108);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 109);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 110);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 111);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 112);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 113);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 114);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 115);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 116);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 117);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 118);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 119);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 120);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 121);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 122);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 123);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 124);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 125);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 126);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 127);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 128);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 129);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 130);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 131);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 132);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 133);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 134);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 135);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 136);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 137);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 138);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 139);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 140);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 141);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 142);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 143);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 144);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 145);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 146);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 147);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 148);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 149);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 150);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 151);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 152);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 153);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 154);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 155);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 156);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 157);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 158);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 159);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 160);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 161);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 162);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 163);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 164);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 165);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 166);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 167);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 168);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 169);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 170);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 171);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 172);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 173);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 174);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 175);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 176);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 177);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 178);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 179);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 180);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 181);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 182);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 183);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 184);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 185);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 186);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 187);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 188);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 189);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 190);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 191);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 192);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 193);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 194);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 195);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 196);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 197);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 198);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 199);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 200);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 201);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 202);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 203);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 204);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 205);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 206);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 207);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 208);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 209);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 210);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 211);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 212);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 213);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 214);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 215);

            migrationBuilder.DeleteData(
                table: "Choices",
                keyColumn: "Id",
                keyValue: 216);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "Questions",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Lessons",
                keyColumn: "Id",
                keyValue: 13);
        }
    }
}
