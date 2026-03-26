using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjet.Migrations
{
    /// <inheritdoc />
    public partial class AddChefEquipeToGroupe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employes_GroupesEquipe_GroupeEquipeId",
                table: "Employes");

            migrationBuilder.DropIndex(
                name: "IX_Employes_Email",
                table: "Employes");

            migrationBuilder.AddColumn<int>(
                name: "ChefEquipeId",
                table: "GroupesEquipe",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupesEquipe_ChefEquipeId",
                table: "GroupesEquipe",
                column: "ChefEquipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employes_GroupesEquipe_GroupeEquipeId",
                table: "Employes",
                column: "GroupeEquipeId",
                principalTable: "GroupesEquipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GroupesEquipe_Employes_ChefEquipeId",
                table: "GroupesEquipe",
                column: "ChefEquipeId",
                principalTable: "Employes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employes_GroupesEquipe_GroupeEquipeId",
                table: "Employes");

            migrationBuilder.DropForeignKey(
                name: "FK_GroupesEquipe_Employes_ChefEquipeId",
                table: "GroupesEquipe");

            migrationBuilder.DropIndex(
                name: "IX_GroupesEquipe_ChefEquipeId",
                table: "GroupesEquipe");

            migrationBuilder.DropColumn(
                name: "ChefEquipeId",
                table: "GroupesEquipe");

            migrationBuilder.CreateIndex(
                name: "IX_Employes_Email",
                table: "Employes",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Employes_GroupesEquipe_GroupeEquipeId",
                table: "Employes",
                column: "GroupeEquipeId",
                principalTable: "GroupesEquipe",
                principalColumn: "Id");
        }
    }
}
