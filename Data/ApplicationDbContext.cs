using GestionProjet.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GestionProjet.Data;

public class ApplicationDbContext : IdentityDbContext<Utilisateur>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Client> Clients { get; set; }
    public DbSet<Projet> Projets { get; set; }
    public DbSet<Phase> Phases { get; set; }
    public DbSet<Tache> Taches { get; set; }
    public DbSet<SousTache> SousTaches { get; set; }
    public DbSet<GroupeEquipe> GroupesEquipe { get; set; }
    public DbSet<Employe> Employes { get; set; }
    public DbSet<Affectation> Affectations { get; set; }
    public DbSet<Test> Tests { get; set; }
    public DbSet<DeclarationTemps> DeclarationsTemps { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<TypeProjet> TypesProjet { get; set; }
    public DbSet<Competence> Competences { get; set; }
    public DbSet<EmployeCompetence> EmployeCompetences { get; set; }
    public DbSet<TacheCompetence> TacheCompetences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- COMPÉTENCES ---
        modelBuilder.Entity<EmployeCompetence>()
            .HasOne(ec => ec.Employe)
            .WithMany(e => e.EmployeCompetences)
            .HasForeignKey(ec => ec.EmployeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmployeCompetence>()
            .Property(ec => ec.DateAcquisition)
            .HasDefaultValueSql("GETDATE()");

        modelBuilder.Entity<EmployeCompetence>()
            .HasOne(ec => ec.Competence)
            .WithMany(c => c.EmployeCompetences)
            .HasForeignKey(ec => ec.CompetenceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TacheCompetence>()
            .HasOne(tc => tc.Tache)
            .WithMany(t => t.TacheCompetences)
            .HasForeignKey(tc => tc.TacheId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TacheCompetence>()
            .HasOne(tc => tc.Competence)
            .WithMany(c => c.TacheCompetences)
            .HasForeignKey(tc => tc.CompetenceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Competence>()
            .HasIndex(c => c.Nom)
            .IsUnique();

        modelBuilder.Entity<Competence>()
            .HasIndex(c => c.Categorie);

        modelBuilder.Entity<EmployeCompetence>()
            .HasIndex(ec => new { ec.EmployeId, ec.CompetenceId })
            .IsUnique();

        modelBuilder.Entity<TacheCompetence>()
            .HasIndex(tc => new { tc.TacheId, tc.CompetenceId })
            .IsUnique();

        // --- PROJET / PHASE / SOUS TACHE ---
        modelBuilder.Entity<Phase>()
            .HasOne(p => p.Projet)
            .WithMany(p => p.Phases)
            .HasForeignKey(p => p.ProjetId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SousTache>()
            .HasOne(st => st.Tache)
            .WithMany(t => t.SousTaches)
            .HasForeignKey(st => st.TacheId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- AFFECTATIONS ---
        modelBuilder.Entity<Affectation>()
            .HasOne(a => a.SousTache)
            .WithMany(st => st.Affectations)
            .HasForeignKey(a => a.SousTacheId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Affectation>()
            .HasOne(a => a.Employe)
            .WithMany(e => e.Affectations)
            .HasForeignKey(a => a.EmployeId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- TESTS ---
        modelBuilder.Entity<Test>()
            .HasOne(t => t.Tache)
            .WithMany(t => t.Tests)
            .HasForeignKey(t => t.TacheId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Test>()
            .HasOne(t => t.SousTache)
            .WithMany(st => st.Tests)
            .HasForeignKey(t => t.SousTacheId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Test>()
            .HasOne(t => t.Employe)
            .WithMany(e => e.TestsEffectues)
            .HasForeignKey(t => t.EmployeId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- DECLARATION TEMPS ---
        modelBuilder.Entity<DeclarationTemps>()
            .HasOne(d => d.SousTache)
            .WithMany(st => st.DeclarationsTemps)
            .HasForeignKey(d => d.SousTacheId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DeclarationTemps>()
            .HasOne(d => d.Employe)
            .WithMany(e => e.DeclarationsTemps)
            .HasForeignKey(d => d.EmployeId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- UTILISATEUR ---
        modelBuilder.Entity<Utilisateur>()
            .HasOne(u => u.Employe)
            .WithMany()
            .HasForeignKey(u => u.EmployeId)
            .OnDelete(DeleteBehavior.SetNull);

        // --- NOTIFICATIONS ---
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Employe)
            .WithMany(e => e.Notifications)
            .HasForeignKey(n => n.EmployeId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- GROUPE EQUIPE (CHANGEMENT CRITIQUE ICI) ---
        // ✅ NOUVELLE CONFIGURATION N-N (Many-to-Many)
        // Cela permet à un employé d'être dans plusieurs groupes et un groupe d'avoir plusieurs employés
        modelBuilder.Entity<Employe>()
            .HasMany(e => e.Groupes) // Assurez-vous d'avoir ajouté 'Groupes' dans Employe.cs
            .WithMany(g => g.Employes)
            .UsingEntity<Dictionary<string, object>>(
                "EmployeGroupeEquipe",
                j => j.HasOne<GroupeEquipe>().WithMany().HasForeignKey("GroupeEquipeId").OnDelete(DeleteBehavior.Cascade),
                j => j.HasOne<Employe>().WithMany().HasForeignKey("EmployeId").OnDelete(DeleteBehavior.Cascade),
                j => j.ToTable("EmployeGroupeEquipe")
            );

        // Ancien code à SUPPRIMER (One-to-Many) :
        // modelBuilder.Entity<Employe>()
        //    .HasOne(e => e.GroupeEquipe) ...
        //    .HasForeignKey(e => e.GroupeEquipeId) ...

        // Gestion du Chef d'équipe (Un groupe a un seul chef)
        modelBuilder.Entity<GroupeEquipe>()
            .HasOne(g => g.ChefEquipe)
            .WithMany()
            .HasForeignKey(g => g.ChefEquipeId)
            .OnDelete(DeleteBehavior.Restrict);

        // --- INDEXES ---
        modelBuilder.Entity<Projet>()
            .HasIndex(p => p.Statut);

        modelBuilder.Entity<Projet>()
            .HasIndex(p => p.DateDebut);

        modelBuilder.Entity<Tache>()
            .HasIndex(t => t.Statut);

        modelBuilder.Entity<SousTache>()
            .HasIndex(st => st.Statut);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.EmployeId);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.Lu);

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();

        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.UserId);

        modelBuilder.Entity<TypeProjet>()
            .HasIndex(t => t.TypeProjetGuid)
            .IsUnique();
    }
}