using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionProjet.Migrations
{
    /// <inheritdoc />
    public partial class RemoveIdentityFromClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Supprimer les clés étrangères qui pointent vers Clients (pour pouvoir dropper la table)
            migrationBuilder.Sql(@"
                DECLARE @sql NVARCHAR(MAX) = N''
                SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id)) + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';'
                FROM sys.foreign_keys
                WHERE referenced_object_id = OBJECT_ID('Clients');
                EXEC sp_executesql @sql;
            ");

            // 2. Sauvegarder les données dans une table temporaire
            migrationBuilder.Sql(@"SELECT * INTO [Clients_Temp] FROM [Clients];");

            // 3. Supprimer l'ancienne table
            migrationBuilder.Sql(@"DROP TABLE [Clients];");

            // 4. Créer la NOUVELLE table sans IDENTITY
            // Note : On utilise [Nom] pour la colonne SQL car votre modèle a [Column("Nom")]
            migrationBuilder.Sql(@"
                CREATE TABLE [Clients] (
                    [Id] int NOT NULL,
                    [Nom] nvarchar(200) NOT NULL,
                    [Secteur] nvarchar(max) NULL,
                    [EmailPrincipal] nvarchar(100) NULL,
                    [TelephonePrincipal] nvarchar(20) NULL,
                    [Ville] nvarchar(max) NULL,
                    [Pays] nvarchar(max) NULL,
                    [Statut] nvarchar(max) NULL,
                    [SyncedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_Clients] PRIMARY KEY ([Id])
                );
            ");

            // 5. Réinsérer les données depuis la table temporaire
            migrationBuilder.Sql(@"
                INSERT INTO [Clients] ([Id], [Nom], [Secteur], [EmailPrincipal], [TelephonePrincipal], [Ville], [Pays], [Statut], [SyncedAt])
                SELECT [Id], [Nom], [Secteur], [EmailPrincipal], [TelephonePrincipal], [Ville], [Pays], [Statut], [SyncedAt] FROM [Clients_Temp];
            ");

            // 6. Supprimer la table temporaire
            migrationBuilder.Sql(@"DROP TABLE [Clients_Temp];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Pour revenir en arrière, on recrée la table avec IDENTITY
            migrationBuilder.Sql(@"
                CREATE TABLE [Clients_New] (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [Nom] nvarchar(200) NOT NULL,
                    [Secteur] nvarchar(max) NULL,
                    [EmailPrincipal] nvarchar(100) NULL,
                    [TelephonePrincipal] nvarchar(20) NULL,
                    [Ville] nvarchar(max) NULL,
                    [Pays] nvarchar(max) NULL,
                    [Statut] nvarchar(max) NULL,
                    [SyncedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_Clients_New] PRIMARY KEY ([Id])
                );

                INSERT INTO [Clients_New] ([Nom], [Secteur], [EmailPrincipal], [TelephonePrincipal], [Ville], [Pays], [Statut], [SyncedAt])
                SELECT [Nom], [Secteur], [EmailPrincipal], [TelephonePrincipal], [Ville], [Pays], [Statut], [SyncedAt] FROM [Clients];

                DROP TABLE [Clients];
                EXEC sp_rename 'Clients_New', 'Clients';
            ");
        }
    }
}