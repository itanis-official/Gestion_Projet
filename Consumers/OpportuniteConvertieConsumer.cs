using MassTransit;
using ITANIS.SharedEvents;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Enums;
using GestionProjet.Services;
using Microsoft.EntityFrameworkCore;

namespace GestionProjet.Consumers;

public class OpportuniteConvertieConsumer : IConsumer<OpportuniteConvertieEvent>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<OpportuniteConvertieConsumer> _logger;
    private readonly ProjetSyncService _syncService;

    public OpportuniteConvertieConsumer(
        ApplicationDbContext db,
        ILogger<OpportuniteConvertieConsumer> logger,
        ProjetSyncService syncService)
    {
        _db = db;
        _logger = logger;
        _syncService = syncService;
    }

    public async Task Consume(ConsumeContext<OpportuniteConvertieEvent> context)
    {
        var msg = context.Message;
        _logger.LogInformation("📩 MESSAGE REÇU ! OpportuniteId: {Id}", msg.OpportunityId);

        var client = await _db.Clients.FindAsync(msg.CompanyIdOrigine);
        if (client == null)
        {
            client = new Client
            {
                Id = msg.CompanyIdOrigine,
                RaisonSociale = msg.CompanyName,
                SyncedAt = DateTime.UtcNow
            };
            _db.Clients.Add(client);
            await _db.SaveChangesAsync();
        }

        var existe = await _db.Projets
            .AnyAsync(p => p.OpportuniteIdOrigine == msg.OpportunityId);

        if (existe)
        {
            _logger.LogWarning("⚠️ Projet déjà existant pour OpportuniteId {Id}", msg.OpportunityId);
            return;
        }
        GroupeEquipe? groupe = null;
        if (msg.EquipeIdOrigine.HasValue)
        {
            groupe = await _db.GroupesEquipe
                .FirstOrDefaultAsync(g => g.IdOrigineRH == msg.EquipeIdOrigine.Value);
                
            if (groupe == null)
            {
                _logger.LogWarning("⚠️ GroupeEquipe avec IdOrigineRH {EquipeId} introuvable pour l'opportunité {OpportuniteId}", 
                    msg.EquipeIdOrigine.Value, msg.OpportunityId);
            }
        }
        string? cdcUrl = null;

        if (msg.CdcFileContent != null && msg.CdcFileContent.Length > 0)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "cdc");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var safeFileName = string.IsNullOrEmpty(msg.CdcFileName) 
                ? $"{Guid.NewGuid()}.pdf" 
                : Path.GetFileName(msg.CdcFileName);

            var filePath = Path.Combine(uploadsFolder, safeFileName);

            try
            {
                
                await File.WriteAllBytesAsync(filePath, msg.CdcFileContent);
                cdcUrl = $"/uploads/cdc/{safeFileName}";
                
                _logger.LogInformation("📄 Cahier des charges enregistré : {Url}", cdcUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erreur lors de l'enregistrement du fichier CDC pour l'opportunité {Id}", msg.OpportunityId);
            }
        }

        var projet = new Projet
        {
            Nom = msg.NomProjet ?? msg.Titre,
            OpportuniteIdOrigine = msg.OpportunityId,
            ClientId = msg.CompanyIdOrigine,
            DateDebut = msg.DateDebutProjet ?? msg.DateCloturePrevu ?? DateTime.UtcNow,
            DateFinPrevue = (msg.DateDebutProjet ?? msg.DateCloturePrevu ?? DateTime.UtcNow).AddMonths(1),
            Description = msg.DescriptionProjet ?? msg.Description ?? string.Empty,
            Statut = StatutProjet.Planifie,
            TypeProjet = msg.TypeProjet ?? "Web",
            BudgetEstime = msg.BudgetConfirme ?? msg.ValeurEstimee,
            GroupeEquipeId = groupe?.Id,
            CdcFileUrl = cdcUrl
        };

        _db.Projets.Add(projet);
        await _db.SaveChangesAsync();

        await _syncService.PublierVersCRM(projet, SyncAction.Created);

        _logger.LogInformation("✅ Projet {Id} créé ET poussé vers le CRM (CDC: {Url})", projet.Id, projet.CdcFileUrl);
    }
}