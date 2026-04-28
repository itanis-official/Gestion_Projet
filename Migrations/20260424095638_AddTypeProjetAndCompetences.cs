using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjet.Migrations
{
    /// <inheritdoc />
    public partial class AddTypeProjetAndCompetences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TypesProjet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TypeProjetGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Ordre = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TypesProjet", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TypesProjet_TypeProjetGuid",
                table: "TypesProjet",
                column: "TypeProjetGuid",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TypesProjet");
        }
    }
}
