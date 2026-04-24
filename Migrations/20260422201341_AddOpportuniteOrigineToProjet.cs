using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjet.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportuniteOrigineToProjet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OpportuniteIdOrigine",
                table: "Projets",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OpportuniteIdOrigine",
                table: "Projets");
        }
    }
}
