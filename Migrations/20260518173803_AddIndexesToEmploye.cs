using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjet.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesToEmploye : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Employes_Email",
                table: "Employes",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Employes_IdOrigineRH",
                table: "Employes",
                column: "IdOrigineRH");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employes_Email",
                table: "Employes");

            migrationBuilder.DropIndex(
                name: "IX_Employes_IdOrigineRH",
                table: "Employes");
        }
    }
}
