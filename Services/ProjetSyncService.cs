// GestionProjet/Services/ProjetSyncService.cs
using MassTransit;
using ITANIS.SharedEvents;
using GestionProjet.Data;
using GestionProjet.Models;
using Microsoft.EntityFrameworkCore;

namespace GestionProjet.Services;

public class ProjetSyncService
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ProjetSyncService> _logger;

    public ProjetSyncService(
        IPublishEndpoint publishEndpoint,
        ApplicationDbContext db,
        ILogger<ProjetSyncService> logger)
    {
        _publishEndpoint = publishEndpoint;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Publie un événement ProjetSyncEvent vers le CRM à partir de l'ID
    /// </summary>
    public async Task PublierVersCRM(int projetId, SyncAction action)
    {
        var projet = await _db.Projets
            .Include(p => p.Client)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projetId);

        if (projet == null)
        {
            _logger.LogWarning("⚠️ Projet {Id} introuvable pour sync CRM", projetId);
            return;
        }

        await PublierVersCRM(projet, action);
    }

    /// <summary>
    /// Publie un événement ProjetSyncEvent vers le CRM à partir de l'entité
    /// </summary>
       /// <summary>
    /// Publie un événement ProjetSyncEvent vers le CRM à partir de l'entité
    /// </summary>
    public async Task PublierVersCRM(Projet projet, SyncAction action)
    {
        try
        {
            // 1. Recharger le projet avec TOUTE la hiérarchie (Phases -> Taches -> SousTaches)
            // Note : On ne peut pas utiliser le paramètre 'projet' passé si ses collections ne sont pas chargées (lazy loading).
            // Il vaut mieux faire une requête spécifique ici pour s'assurer d'avoir tout le détail.
            var projetComplet = await _db.Projets
                .Include(p => p.Client)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.SousTaches)
                            // On inclut le responsable de la sous-tache pour avoir son nom
                            .ThenInclude(st => st.Affectations) 
                                .ThenInclude(a => a.Employe)
                                
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projet.Id);

            if (projetComplet == null)
            {
                _logger.LogWarning("⚠️ Projet {Id} introuvable pour sync CRM (recharge)", projet.Id);
                return;
            }

            var phasesDto = projetComplet.Phases.Select(ph => new PhaseSyncDto
            {
                TypePhase = ph.TypePhase.ToString(),
                Taches = ph.Taches.Select(t => new TacheSyncDto
                {
                    Titre = t.Titre,
                    Statut = t.Statut.ToString(),
                    SousTaches = t.SousTaches.Select(st => new SousTacheSyncDto
                    {
                        Titre = st.Titre,
                        Statut = st.Statut.ToString(),
                        DureeEstimeeHeures = st.DureeEstimeeHeures,
                        // Récupérer le nom de l'employé affecté s'il y en a un
                        ResponsableNom = st.Affectations.FirstOrDefault()?.Employe?.NomComplet
                    }).ToList()
                }).ToList()
            }).ToList();

            await _publishEndpoint.Publish(new ProjetSyncEvent
            {
                Action = action,
                Id = projetComplet.Id,
                OpportuniteIdOrigine = projetComplet.OpportuniteIdOrigine ?? 0,
                Nom = projetComplet.Nom,
                Statut = projetComplet.Statut.ToString(),
                DateDebut = projetComplet.DateDebut,
                DateFinPrevue = projetComplet.DateFinPrevue,
                ClientRaisonSociale = projetComplet.Client?.RaisonSociale ?? string.Empty,
                ClientId = projetComplet.ClientId,
                BudgetEstime = projetComplet.BudgetEstime,
                BudgetReel = projetComplet.BudgetReel,
                Description = projetComplet.Description ?? string.Empty,
                TypeProjet = projetComplet.TypeProjet ?? string.Empty,
                
                // On injecte la liste remplie
                Phases = phasesDto
            });

            _logger.LogInformation(
                "📤 ProjetSyncEvent publié — Id: {Id}, Action: {Action}, NbSousTaches: {NbSt}",
                projetComplet.Id, action, phasesDto.Sum(p => p.Taches.Sum(t => t.SousTaches.Count)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erreur publication ProjetSyncEvent pour projet {Id}", projet.Id);
        }
    }
}