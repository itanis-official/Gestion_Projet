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
    public async Task PublierVersCRM(Projet projet, SyncAction action)
    {
        try
        {
            await _publishEndpoint.Publish(new ProjetSyncEvent
            {
                Action = action,
                Id = projet.Id,
                OpportuniteIdOrigine = projet.OpportuniteIdOrigine ?? 0,
                Nom = projet.Nom,
                Statut = projet.Statut.ToString(),
                DateDebut = projet.DateDebut,
                DateFinPrevue = projet.DateFinPrevue,
                ClientRaisonSociale = projet.Client?.RaisonSociale ?? string.Empty,
                ClientId = projet.ClientId,
                BudgetEstime = projet.BudgetEstime,
                BudgetReel = projet.BudgetReel,
                Description = projet.Description ?? string.Empty,
                TypeProjet = projet.TypeProjet ?? string.Empty
            });

            _logger.LogInformation(
                "📤 ProjetSyncEvent publié — Id: {Id}, Action: {Action}, Statut: {Statut}, BudgetReel: {Budget}",
                projet.Id, action, projet.Statut, projet.BudgetReel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erreur publication ProjetSyncEvent pour projet {Id}", projet.Id);
        }
    }
}