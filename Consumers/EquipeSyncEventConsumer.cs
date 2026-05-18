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
        var action = evt.GetActionAsString();

        _logger.LogInformation("🚀 Début Consume EquipeSyncEvent pour {Guid}, Action: {Action}", evt.EquipeGuid, action);

        if (evt.SourceModule == SyncSourceModule.GestionProjet) return;
        if (evt.EquipeGuid == Guid.Empty) return;

        var existing = await _db.GroupesEquipe
            .Include(g => g.Employes)
            .FirstOrDefaultAsync(g => g.EquipeGuid == evt.EquipeGuid);

        if (action == "Deleted")
        {
            if (existing != null)
            {
                _db.GroupesEquipe.Remove(existing);
                await _db.SaveChangesAsync();
                _logger.LogInformation("🗑️ GroupeEquipe {Guid} supprimé", evt.EquipeGuid);
            }
            return;
        }

        if (existing != null && existing.UpdatedAt > evt.ChangedAt) return;

        // --- Création ou Mise à jour du Groupe ---
        if (existing == null)
        {
            existing = new GroupeEquipe
            {
                EquipeGuid = evt.EquipeGuid,
                IdOrigineRH = evt.Id,
                Nom = evt.Nom,
                TypeProjetCompatible = evt.Domaine,
                UpdatedAt = evt.ChangedAt,
            };
            _db.GroupesEquipe.Add(existing);
        }
        else
        {
            existing.Nom = evt.Nom;
            existing.TypeProjetCompatible = evt.Domaine;
            existing.UpdatedAt = evt.ChangedAt;
        }

        await _db.SaveChangesAsync();

        // --- Synchronisation des Membres (LOGIQUE N-N AMÉLIORÉE) ---
        var incomingMembres = evt.Membres ?? new List<EquipeMembreSyncDto>();
        _logger.LogInformation("📋 Équipe '{Nom}' : {Count} membre(s) reçu(s)", evt.Nom, incomingMembres.Count);

        if (incomingMembres.Any())
        {
            // ✅ FIX : On crée un set de Tuples (IdOrigineRH, AgentType) pour la comparaison
            var incomingKeys = incomingMembres
                .Select(m => (Id: m.CollaborateurIdOrigine, Type: m.CollaborateurType ?? "interne"))
                .ToHashSet();

            // 1. Retrait des membres qui ne sont plus dans la liste
            var toDetach = existing.Employes
                .Where(e => e.IdOrigineRH.HasValue && 
                       !incomingKeys.Contains((e.IdOrigineRH.Value, e.AgentType ?? "interne")))
                .ToList();

            foreach (var emp in toDetach)
            {
                existing.Employes.Remove(emp);
                _logger.LogInformation("➖ Retrait {Nom} (RH ID {Rh}, Type {Type})", emp.NomComplet, emp.IdOrigineRH, emp.AgentType);
            }

            // 2. Trouver les membres manquants dans le groupe actuel
            var currentKeys = existing.Employes
                .Where(e => e.IdOrigineRH.HasValue)
                .Select(e => (Id: e.IdOrigineRH.Value, Type: e.AgentType ?? "interne"))
                .ToHashSet();

            var missingMembres = incomingMembres
                .Where(m => !currentKeys.Contains((m.CollaborateurIdOrigine, m.CollaborateurType ?? "interne")))
                .ToList();

            if (missingMembres.Any())
            {
                _logger.LogInformation("🔍 {Count} membre(s) manquant(s) localement dans le groupe.", missingMembres.Count);

                // Récupérer tous les IDs RH manquants pour une recherche en base (batch)
                var missingRhIds = missingMembres.Select(m => m.CollaborateurIdOrigine).Distinct().ToList();
                
                var potentialMatches = await _db.Employes
                    .Where(e => e.IdOrigineRH.HasValue && missingRhIds.Contains(e.IdOrigineRH.Value))
                    .ToListAsync();

               foreach (var missing in missingMembres)
{
    var expectedType = missing.CollaborateurType ?? "interne";

    // ✅ FIX : On cherche par IdOrigineRH, en acceptant soit le bon AgentType, soit NULL (joker)
    var match = potentialMatches.FirstOrDefault(e => 
        e.IdOrigineRH == missing.CollaborateurIdOrigine && 
        (e.AgentType == expectedType || string.IsNullOrEmpty(e.AgentType)));

    if (match != null)
    {
        existing.Employes.Add(match);
        
        // ✅ BONUS : Si l'employé existait mais avait AgentType=NULL, on corrige sa vraie nature
        if (string.IsNullOrEmpty(match.AgentType) && !string.IsNullOrEmpty(expectedType))
        {
            match.AgentType = expectedType;
            _logger.LogInformation("🔄 Mise à jour AgentType de NULL vers '{Type}' pour RH ID {Id}", expectedType, match.IdOrigineRH);
        }
        
        _logger.LogInformation("✅ Ajout de l'employé existant {Nom} (RH ID {Rh}, Type {Type})", match.NomComplet, match.IdOrigineRH, expectedType);
    }
    else
    {
        // Création d'un Stub typé (Interne ou Externe) si absent
        _logger.LogWarning("⚠️ Membre RH ID {Rh} (Type: {Type}) introuvable localement. Création d'un Stub.", missing.CollaborateurIdOrigine, expectedType);
        
        var stub = new Employe
        {
            IdOrigineRH = missing.CollaborateurIdOrigine,
            AgentType = expectedType, 
            NomComplet = $"⏳ En attente ({expectedType}: {missing.CollaborateurIdOrigine})",
            Email = $"stub.{expectedType}.{missing.CollaborateurIdOrigine}@temp.local",
            Role = "Non défini",
            CoutHoraire = 0
        };
        
        existing.Employes.Add(stub);
    }
}
                try
                {
                    await _db.SaveChangesAsync();
                    _logger.LogInformation("💾 Sauvegarde des membres de l'équipe réussie.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ ERREUR CRITIQUE lors de la sauvegarde des membres/Stubs.");
                    throw;
                }
            }
        }
        else
        {
            _logger.LogWarning("⚠️ Événement sans membres pour l'équipe {Nom}.", evt.Nom);
        }

        // --- Résolution du Chef d'équipe ---
        if (evt.ChefProjetIdOrigine.HasValue)
        {
            var chefId = await _db.Employes
                .Where(e => e.IdOrigineRH == evt.ChefProjetIdOrigine.Value)
                .Select(e => (int?)e.Id)
                .FirstOrDefaultAsync();

            if (chefId.HasValue && existing.ChefEquipeId != chefId.Value)
            {
                existing.ChefEquipeId = chefId.Value;
                await _db.SaveChangesAsync();
                _logger.LogInformation("👑 Chef d'équipe défini: Employé ID {Id}", chefId.Value);
            }
        }
    }
}