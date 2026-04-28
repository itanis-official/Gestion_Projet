using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjet.Migrations
{
    /// <inheritdoc />
    public partial class AddCdcFileUrlToProjet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CdcFileUrl",
                table: "Projets",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CdcFileUrl",
                table: "Projets");
        }
    }
}
