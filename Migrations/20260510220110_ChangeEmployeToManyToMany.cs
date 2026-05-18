using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjet.Migrations
{
    /// <inheritdoc />
    public partial class ChangeEmployeToManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employes_GroupesEquipe_GroupeEquipeId",
                table: "Employes");

            migrationBuilder.DropIndex(
                name: "IX_Employes_GroupeEquipeId",
                table: "Employes");

            migrationBuilder.DropColumn(
                name: "GroupeEquipeId",
                table: "Employes");

            migrationBuilder.CreateTable(
                name: "EmployeGroupeEquipe",
                columns: table => new
                {
                    EmployeId = table.Column<int>(type: "int", nullable: false),
                    GroupeEquipeId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeGroupeEquipe", x => new { x.EmployeId, x.GroupeEquipeId });
                    table.ForeignKey(
                        name: "FK_EmployeGroupeEquipe_Employes_EmployeId",
                        column: x => x.EmployeId,
                        principalTable: "Employes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeGroupeEquipe_GroupesEquipe_GroupeEquipeId",
                        column: x => x.GroupeEquipeId,
                        principalTable: "GroupesEquipe",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeGroupeEquipe_GroupeEquipeId",
                table: "EmployeGroupeEquipe",
                column: "GroupeEquipeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeGroupeEquipe");

            migrationBuilder.AddColumn<int>(
                name: "GroupeEquipeId",
                table: "Employes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Employes_GroupeEquipeId",
                table: "Employes",
                column: "GroupeEquipeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Employes_GroupesEquipe_GroupeEquipeId",
                table: "Employes",
                column: "GroupeEquipeId",
                principalTable: "GroupesEquipe",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
