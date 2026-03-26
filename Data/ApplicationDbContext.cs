using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using GestionProjet.Models;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);


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

        modelBuilder.Entity<Test>()
            .HasOne(t => t.Tache)
            .WithMany(t => t.Tests)
            .HasForeignKey(t => t.TacheId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Test>()
            .HasOne(t => t.Employe)
            .WithMany(e => e.TestsEffectues)
            .HasForeignKey(t => t.EmployeId)
            .OnDelete(DeleteBehavior.Restrict);

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

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Employe)
            .WithMany(e => e.Notifications)
            .HasForeignKey(n => n.EmployeId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Utilisateur>()
            .HasOne(u => u.Employe)
            .WithMany()
            .HasForeignKey(u => u.EmployeId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Projet>()
            .HasIndex(p => p.Statut);

        modelBuilder.Entity<Projet>()
            .HasIndex(p => p.DateDebut);

        modelBuilder.Entity<Tache>()
            .HasIndex(t => t.Statut);

        modelBuilder.Entity<SousTache>()
            .HasIndex(st => st.Statut);

        modelBuilder.Entity<Employe>()
    .HasOne(e => e.GroupeEquipe)
    .WithMany(g => g.Employes)
    .HasForeignKey(e => e.GroupeEquipeId)
    .OnDelete(DeleteBehavior.SetNull);
    modelBuilder.Entity<GroupeEquipe>()
    .HasOne(g => g.ChefEquipe)
    .WithMany()
    .HasForeignKey(g => g.ChefEquipeId)
    .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.EmployeId);
            
        modelBuilder.Entity<Notification>()
            .HasIndex(n => n.Lu);
            
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.Token)
            .IsUnique();
            
        modelBuilder.Entity<RefreshToken>()
            .HasIndex(rt => rt.UserId);
    }
}