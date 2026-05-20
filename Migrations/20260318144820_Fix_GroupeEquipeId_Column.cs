using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjet.Migrations
{
    /// <inheritdoc />
    public partial class Fix_GroupeEquipeId_Column : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ajouter la colonne GroupeEquipeId à la table Projets
            migrationBuilder.AddColumn<int>(
                name: "GroupeEquipeId",
                table: "Projets",
                type: "int",
                nullable: true);

            // Créer l'index pour la nouvelle colonne
            migrationBuilder.CreateIndex(
                name: "IX_Projets_GroupeEquipeId",
                table: "Projets",
                column: "GroupeEquipeId");

            // Ajouter la contrainte de clé étrangère
            migrationBuilder.AddForeignKey(
                name: "FK_Projets_GroupesEquipe_GroupeEquipeId",
                table: "Projets",
                column: "GroupeEquipeId",
                principalTable: "GroupesEquipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict); // ou SetNull selon votre besoin
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Supprimer la clé étrangère d'abord
            migrationBuilder.DropForeignKey(
                name: "FK_Projets_GroupesEquipe_GroupeEquipeId",
                table: "Projets");

            // Supprimer l'index
            migrationBuilder.DropIndex(
                name: "IX_Projets_GroupeEquipeId",
                table: "Projets");

            // Supprimer la colonne
            migrationBuilder.DropColumn(
                name: "GroupeEquipeId",
                table: "Projets");
        }
    }
}