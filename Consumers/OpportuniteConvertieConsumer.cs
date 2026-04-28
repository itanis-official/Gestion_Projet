// Fichier : GestionProjet/Consumers/OpportuniteConvertieConsumer.cs
using MassTransit;
using ITANIS.SharedEvents;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Enums;
using GestionProjet.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO; // Nécessaire pour manipuler les fichiers

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

        // 1. Vérifier/Créer le Client
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

        // 2. Éviter les doublons
        var existe = await _db.Projets
            .AnyAsync(p => p.OpportuniteIdOrigine == msg.OpportunityId);

        if (existe)
        {
            _logger.LogWarning("⚠️ Projet déjà existant pour OpportuniteId {Id}", msg.OpportunityId);
            return;
        }

        // 3. Résoudre le GroupeEquipe via IdOrigineRH
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

        // ==================== LOGIQUE FICHIER (PDF) ====================
        string? cdcUrl = null;

        // Si un contenu de fichier est présent dans le message
        if (msg.CdcFileContent != null && msg.CdcFileContent.Length > 0)
        {
            // Définir le dossier de stockage : wwwroot/uploads/cdc
            // Directory.GetCurrentDirectory() se positionne correctement en Dev et Prod
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "cdc");

            // Créer le dossier s'il n'existe pas
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // Sécuriser le nom du fichier (éviter les chemins relatifs comme ../../)
            var safeFileName = string.IsNullOrEmpty(msg.CdcFileName) 
                ? $"{Guid.NewGuid()}.pdf" 
                : Path.GetFileName(msg.CdcFileName);

            var filePath = Path.Combine(uploadsFolder, safeFileName);

            try
            {
                // Écrire le fichier sur le disque
                await File.WriteAllBytesAsync(filePath, msg.CdcFileContent);
                
                // Générer l'URL relative pour l'application (ex: /uploads/cdc/monfichier.pdf)
                cdcUrl = $"/uploads/cdc/{safeFileName}";
                
                _logger.LogInformation("📄 Cahier des charges enregistré : {Url}", cdcUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erreur lors de l'enregistrement du fichier CDC pour l'opportunité {Id}", msg.OpportunityId);
            }
        }
        // ===============================================================

        // 4. Créer le Projet
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
            
            // On assigne l'URL générée ici
            CdcFileUrl = cdcUrl
        };

        _db.Projets.Add(projet);
        await _db.SaveChangesAsync();

        // 5. 📤 Pousser vers le CRM
        await _syncService.PublierVersCRM(projet, SyncAction.Created);

        _logger.LogInformation("✅ Projet {Id} créé ET poussé vers le CRM (CDC: {Url})", projet.Id, projet.CdcFileUrl);
    }
}