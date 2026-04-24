using ITANIS.SharedEvents;
using GestionProjet.Data;
using GestionProjet.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionProjet.Consumers;

public class EquipeSyncEventConsumer : IConsumer<EquipeSyncEvent>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<EquipeSyncEventConsumer> _logger;

    public EquipeSyncEventConsumer(
        ApplicationDbContext db,
        ILogger<EquipeSyncEventConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<EquipeSyncEvent> context)
    {
        var evt = context.Message;

        // ╔═══════════════════════════════════════════════╗
        // ║  1. ANTI-BOUCLE                                ║
        // ╚═══════════════════════════════════════════════╝
        if (evt.SourceModule == "GestionProjet")
            return;

        if (evt.EquipeGuid == Guid.Empty)
        {
            _logger.LogWarning("EquipeSyncEvent sans GUID, ignoré.");
            return;
        }

        // ╔══════════════════════════════════════════╗
        // ║  2. Lookup par GUID                      ║
        // ╚══════════════════════════════════════════╝
        var existing = await _db.GroupesEquipe
            .Include(g => g.Employes)
            .FirstOrDefaultAsync(g => g.EquipeGuid == evt.EquipeGuid);

        // ╔═══════════════════════╗
        // ║  3. Delete            ║
        // ╚═══════════════════════╝
        if (evt.GetActionAsString() == "Deleted")
        {
            if (existing != null)
            {
                foreach (var emp in existing.Employes)
                {
                    emp.GroupeEquipeId = null;
                }

                _db.GroupesEquipe.Remove(existing);
                await _db.SaveChangesAsync();
                _logger.LogInformation("🗑️ GroupeEquipe {Guid} supprimé (sync RH)", evt.EquipeGuid);
            }
            return;
        }

        // ╔══════════════════════════════════════╗
        // ║  4. Last-write-wins                  ║
        // ╚══════════════════════════════════════╝
        if (existing != null && existing.UpdatedAt > evt.ChangedAt)
        {
            _logger.LogInformation("⏭️ Local plus récent, event ignoré (GroupeEquipe {Guid})", evt.EquipeGuid);
            return;
        }

        // ╔══════════════════════════════════════════════════╗
        // ║  5. Upsert avec validation du Chef (IdOrigineRH)║
        // ╚══════════════════════════════════════════════════╝
        int? validChefId = null;
        if (evt.ChefProjetIdOrigine.HasValue)
        {
            var chefLocal = await _db.Employes.FirstOrDefaultAsync(e => e.IdOrigineRH == evt.ChefProjetIdOrigine.Value);
            if (chefLocal != null)
            {
                validChefId = chefLocal.Id; 
            }
            else
            {
                _logger.LogWarning("⚠️ Chef d'équipe RH ID {ChefId} introuvable localement. L'équipe sera créée sans chef.", evt.ChefProjetIdOrigine.Value);
            }
        }

        if (existing == null)
        {
            existing = new GroupeEquipe
            {
                EquipeGuid = evt.EquipeGuid,
                IdOrigineRH = evt.Id, // ✅ SAUVEGARDE DE L'ID RH
                Nom = evt.Nom,
                TypeProjetCompatible = evt.Domaine,
                ChefEquipeId = validChefId, 
                UpdatedAt = evt.ChangedAt,
            };
            _db.GroupesEquipe.Add(existing);
            _logger.LogInformation("➕ GroupeEquipe {Guid} créé (sync RH)", evt.EquipeGuid);
        }
        else
        {
            existing.Nom = evt.Nom;
            existing.TypeProjetCompatible = evt.Domaine;
            existing.ChefEquipeId = validChefId; 
            existing.UpdatedAt = evt.ChangedAt;
            _logger.LogInformation("✏️ GroupeEquipe {Guid} mis à jour (sync RH)", evt.EquipeGuid);
        }

        await _db.SaveChangesAsync();

        // ╔══════════════════════════════════════════════════╗
        // ║  6. Sync membres via Employe.IdOrigineRH        ║
        // ╚══════════════════════════════════════════════════╝
        var incomingIds = evt.Membres
            .Select(m => m.CollaborateurIdOrigine)
            .ToHashSet();

        var toDetach = existing.Employes
            .Where(e => e.IdOrigineRH == null || !incomingIds.Contains(e.IdOrigineRH.Value))
            .ToList();

        foreach (var emp in toDetach)
        {
            emp.GroupeEquipeId = null;
        }

        foreach (var membreRhId in incomingIds)
        {
            var alreadyIn = existing.Employes.Any(e => e.IdOrigineRH == membreRhId);
            if (!alreadyIn)
            {
                var employe = await _db.Employes.FirstOrDefaultAsync(e => e.IdOrigineRH == membreRhId);
                if (employe != null)
                {
                    employe.GroupeEquipeId = existing.Id; 
                }
                else
                {
                    _logger.LogWarning("⚠️ Employé RH ID {EmployeId} introuvable pour le groupe {Guid}", 
                        membreRhId, evt.EquipeGuid);
                }
            }
        }

        await _db.SaveChangesAsync();
    }
}