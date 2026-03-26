using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjet.Migrations
{
    /// <inheritdoc />
    public partial class AddResponsableTesteurToTache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResponsableId",
                table: "Taches",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TesteurId",
                table: "Taches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Taches_ResponsableId",
                table: "Taches",
                column: "ResponsableId");

            migrationBuilder.CreateIndex(
                name: "IX_Taches_TesteurId",
                table: "Taches",
                column: "TesteurId");

            migrationBuilder.AddForeignKey(
                name: "FK_Taches_Employes_ResponsableId",
                table: "Taches",
                column: "ResponsableId",
                principalTable: "Employes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Taches_Employes_TesteurId",
                table: "Taches",
                column: "TesteurId",
                principalTable: "Employes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Taches_Employes_ResponsableId",
                table: "Taches");

            migrationBuilder.DropForeignKey(
                name: "FK_Taches_Employes_TesteurId",
                table: "Taches");

            migrationBuilder.DropIndex(
                name: "IX_Taches_ResponsableId",
                table: "Taches");

            migrationBuilder.DropIndex(
                name: "IX_Taches_TesteurId",
                table: "Taches");

            migrationBuilder.DropColumn(
                name: "ResponsableId",
                table: "Taches");

            migrationBuilder.DropColumn(
                name: "TesteurId",
                table: "Taches");
        }
    }
}
