using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    // Återskapad historisk markör. Originalkoden finns inte längre.
    // Den granskade databasen hade endast Identity-schemat efter denna migration.
    // Inga ytterligare schemaändringar behövs för att återskapa den baslinjen.
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
