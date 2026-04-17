using Microsoft.EntityFrameworkCore;
using GestionProjet.Data;
using GestionProjet.Enums;

namespace GestionProjet.Services;

public class ProjetService
{
    private readonly ApplicationDbContext _context;

    public ProjetService(ApplicationDbContext context)
    {
        _context = context;
    }

    // =========================
    // 🔥 1. CALCUL BUDGET (basé sur les sous-tâches validées)
    // =========================
    public async Task<decimal> CalculerBudget(int projetId)
    {
        // Option 1: Basé sur les déclarations de temps (si existantes)
        var budgetFromDeclarations = await _context.DeclarationsTemps
            .Where(dt => dt.SousTache.Tache.Phase.ProjetId == projetId)
            .SumAsync(dt => dt.DureeHeures * dt.Employe.CoutHoraire);

        if (budgetFromDeclarations > 0)
            return budgetFromDeclarations;

        // Option 2: Basé sur les sous-tâches validées avec taux horaire par défaut
        const decimal tauxHoraireDefaut = 100; // 100 DH par heure

        var sousTaches = await _context.SousTaches
            .Include(st => st.Tache)
            .ThenInclude(t => t.Phase)
            .Where(st => st.Tache.Phase.ProjetId == projetId)
            .ToListAsync();

        decimal budgetFromSubtasks = 0;

        foreach (var st in sousTaches)
        {
            // ✅ Ne compter que les sous-tâches validées
            if (st.Statut == StatutSousTache.Validee)
            {
                budgetFromSubtasks += st.DureeEstimeeHeures * tauxHoraireDefaut;
            }
        }

        return budgetFromSubtasks;
    }

    // =========================
    // 🔥 2. CALCUL STATUT PROJET (amélioré)
    // =========================
    public async Task<StatutProjet> CalculerStatut(int projetId)
    {
        var sousTaches = await _context.SousTaches
            .Where(st => st.Tache.Phase.ProjetId == projetId)
            .ToListAsync();

        if (!sousTaches.Any())
            return StatutProjet.Planifie;

        int total = sousTaches.Count;
        int validees = sousTaches.Count(st => st.Statut == StatutSousTache.Validee);
        int rejetees = sousTaches.Count(st => st.Statut == StatutSousTache.Rejetee);
        int enCours = sousTaches.Count(st => st.Statut == StatutSousTache.EnCours || st.Statut == StatutSousTache.ATester);

        // Toutes validées → Terminé
        if (validees == total)
            return StatutProjet.Termine;

        // Il y a du travail en cours
        if (enCours > 0 || validees > 0 || rejetees > 0)
            return StatutProjet.EnCours;

        // Vérifier si la date de fin est dépassée
        var projet = await _context.Projets.FindAsync(projetId);
        if (projet != null && projet.DateFinPrevue.Date < DateTime.Today)
            return StatutProjet.Retarde;

        return StatutProjet.Planifie;
    }

    // =========================
    // 🔥 3. MISE À JOUR COMPLETE PROJET
    // =========================
    public async Task MettreAJourProjet(int projetId)
    {
        var projet = await _context.Projets.FindAsync(projetId);

        if (projet == null)
            throw new Exception("Projet introuvable");

        // 🔥 Budget réel
        projet.BudgetReel = await CalculerBudget(projetId);

        // 🔥 Statut
        projet.Statut = await CalculerStatut(projetId);

        await _context.SaveChangesAsync();
    }

    // =========================
    // 🔥 4. RECALCULER TOUS LES PROJETS
    // =========================
    public async Task RecalculerTousLesProjets()
    {
        var projets = await _context.Projets.ToListAsync();

        foreach (var projet in projets)
        {
            projet.BudgetReel = await CalculerBudget(projet.Id);
            projet.Statut = await CalculerStatut(projet.Id);
        }

        await _context.SaveChangesAsync();
    }
}