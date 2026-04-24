using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjet.Migrations
{
    /// <inheritdoc />
    public partial class AddIdOrigineRHToEmploye : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IdOrigineRH",
                table: "Employes",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdOrigineRH",
                table: "Employes");
        }
    }
}
