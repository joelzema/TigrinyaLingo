using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class RemoveOmniglotRecordings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rensa kända Omniglot-filnamn även från egna fraser som återanvänt dem.
            // Egna inspelningar, frasändringar och sparade framsteg lämnas orörda.
            migrationBuilder.Sql("""
                UPDATE [Questions] SET [AudioFileName] = NULL
                WHERE [AudioFileName] IN (N'selam.mp3', N'merhaba.mp3', N'yekenyeley.mp3', N'selamat.mp3', N'simey-yebehal.mp3', N'menyu-shimka.mp3', N'menyu-shimki.mp3', N'ane-kab-metsiye.mp3', N'terediuni.mp3', N'ayteredanin.mp3', N'ayfeletikun.mp3', N'yiqreta.mp3', N'phrase-13.mp3', N'phrase-14.mp3', N'phrase-15.mp3', N'phrase-16.mp3', N'phrase-17.mp3', N'phrase-18.mp3', N'phrase-19.mp3', N'phrase-20.mp3', N'phrase-21.mp3', N'phrase-22.mp3', N'phrase-23.mp3', N'phrase-24.mp3', N'phrase-25.mp3', N'phrase-26.mp3', N'phrase-27.mp3', N'phrase-28.mp3', N'phrase-29.mp3', N'phrase-30.mp3', N'phrase-31.mp3', N'phrase-32.mp3');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Avsiktligt ingen återkoppling: ljudfilerna är borttagna på användarens
            // begäran. En nedgradering ska inte skapa länkar till dessa filer igen.
        }
    }
}