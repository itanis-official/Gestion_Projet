using Microsoft.EntityFrameworkCore;
using GestionProjet.Data;
using GestionProjet.Enums;
using ITANIS.SharedEvents;

namespace GestionProjet.Services;

public class ProjetService
{
  private readonly ApplicationDbContext _context;
    private readonly ProjetSyncService _projetSyncService; 

    public ProjetService(ApplicationDbContext context, ProjetSyncService projetSyncService)
    {
        _context = context;
        _projetSyncService = projetSyncService;
    }
   
    public async Task<decimal> CalculerBudget(int projetId)
    {
        var budgetFromDeclarations = await _context.DeclarationsTemps
            .Where(dt => dt.SousTache.Tache.Phase.ProjetId == projetId)
            .SumAsync(dt => dt.DureeHeures * dt.Employe.CoutHoraire);

        if (budgetFromDeclarations > 0)
            return budgetFromDeclarations;

       
        const decimal tauxHoraireDefaut = 100; 

        var sousTaches = await _context.SousTaches
            .Include(st => st.Tache)
            .ThenInclude(t => t.Phase)
            .Where(st => st.Tache.Phase.ProjetId == projetId)
            .ToListAsync();

        decimal budgetFromSubtasks = 0;

        foreach (var st in sousTaches)
        {
            if (st.Statut == StatutSousTache.Validee)
            {
                budgetFromSubtasks += st.DureeEstimeeHeures * tauxHoraireDefaut;
            }
        }

        return budgetFromSubtasks;
    }

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

        if (validees == total)
            return StatutProjet.Termine;

        if (enCours > 0 || validees > 0 || rejetees > 0)
            return StatutProjet.EnCours;

        var projet = await _context.Projets.FindAsync(projetId);
        if (projet != null && projet.DateFinPrevue.Date < DateTime.Today)
            return StatutProjet.Retarde;

        return StatutProjet.Planifie;
    }

    public async Task MettreAJourProjet(int projetId)
    {
        var projet = await _context.Projets.FindAsync(projetId);

        if (projet == null)
            throw new Exception("Projet introuvable");

        projet.BudgetReel = await CalculerBudget(projetId);
        projet.Statut = await CalculerStatut(projetId);

        await _context.SaveChangesAsync();

       try 
        {
            await _projetSyncService.PublierVersCRM(projetId, SyncAction.Updated);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de la sync CRM pour le projet {projetId}: {ex.Message}");
        }
    }
   
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