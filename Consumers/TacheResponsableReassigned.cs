using MassTransit;
using ITANIS.SharedEvents;
using GestionProjet.Data;
using GestionProjet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionProjet.Consumers;

public class TacheResponsableReassignedConsumer : IConsumer<TacheResponsableReassignedEvent>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TacheResponsableReassignedConsumer> _logger;

    public TacheResponsableReassignedConsumer(
        ApplicationDbContext db,
        ILogger<TacheResponsableReassignedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TacheResponsableReassignedEvent> context)
    {
        var evt = context.Message;

        _logger.LogInformation(
            "📥 Event reçu : Ancien Responsable RH={Ancien}, Nouveau Responsable RH={Nouveau}",
            evt.AncienResponsableId, evt.NouveauResponsableId);

        try
        {
            // ──────────────────────────────────────────────────
            // 1. Trouver TOUS les Employés locaux pour l'ancien RH ID
            //    (il peut y avoir 1 "interne" + 1 "externe")
            // ──────────────────────────────────────────────────
            var anciensLocaux = await _db.Employes
                .Where(e => e.IdOrigineRH == evt.AncienResponsableId)
                .Select(e => new { e.Id, e.AgentType })
                .ToListAsync();

            if (!anciensLocaux.Any())
            {
                _logger.LogInformation(
                    "ℹ️ Ancien responsable RH ID {Id} introuvable localement. Aucune réassignation.",
                    evt.AncienResponsableId);
                return;
            }

            // ──────────────────────────────────────────────────
            // 2. Trouver TOUS les Employés locaux pour le nouveau RH ID
            // ──────────────────────────────────────────────────
            var nouveauxLocaux = await _db.Employes
                .Where(e => e.IdOrigineRH == evt.NouveauResponsableId)
                .Select(e => new { e.Id, e.AgentType })
                .ToListAsync();

            if (!nouveauxLocaux.Any())
            {
                _logger.LogWarning(
                    "⚠️ Nouveau responsable RH ID {Id} introuvable localement. Retry...",
                    evt.NouveauResponsableId);
                throw new InvalidOperationException(
                    $"L'employé RH ID {evt.NouveauResponsableId} n'existe pas encore dans la base locale.");
            }

            // ──────────────────────────────────────────────────
            // 3. Construire le mapping : AncienLocalId → NouveauLocalId
            //    On matching par AgentType pour respecter la logique interne/externe
            // ──────────────────────────────────────────────────
            var nouveauByType = nouveauxLocaux
                .GroupBy(n => n.AgentType ?? "interne")
                .ToDictionary(g => g.Key, g => g.First().Id);

            // Fallback : le premier disponible si le type n'existe pas
            int nouveauFallback = nouveauxLocaux.First().Id;

            var ancienIds = anciensLocaux.Select(a => a.Id).ToList();

            var mapping = new Dictionary<int, int>(); // ancienLocalId → nouveauLocalId
            foreach (var ancien in anciensLocaux)
            {
                var type = ancien.AgentType ?? "interne";
                mapping[ancien.Id] = nouveauByType.TryGetValue(type, out var targetId)
                    ? targetId
                    : nouveauFallback;

                _logger.LogInformation(
                    "🔄 Mapping : Ancien Local {AncienId} (Type={Type}) → Nouveau Local {NouveauId}",
                    ancien.Id, type, mapping[ancien.Id]);
            }

            // ──────────────────────────────────────────────────
            // 4. Réassignation des Tâches
            // ──────────────────────────────────────────────────
            var taches = await _db.Taches
                .Where(t => t.ResponsableId.HasValue && ancienIds.Contains(t.ResponsableId.Value))
                .ToListAsync();

            foreach (var t in taches)
            {
                var oldId = t.ResponsableId!.Value;
                if (mapping.TryGetValue(oldId, out var newId))
                {
                    t.ResponsableId = newId;
                    _logger.LogDebug("  📋 Tache '{Titre}' : Responsable {Old} → {New}", t.Titre, oldId, newId);
                }
            }

            // ──────────────────────────────────────────────────
            // 5. Réassignation des Affectations
            // ──────────────────────────────────────────────────
            var affectations = await _db.Affectations
                .Where(a => ancienIds.Contains(a.EmployeId))
                .ToListAsync();

            foreach (var aff in affectations)
            {
                if (mapping.TryGetValue(aff.EmployeId, out var newId))
                {
                    aff.EmployeId = newId;
                    _logger.LogDebug("  👤 Affectation {AffId} : Employe {Old} → {New}", aff.Id, aff.EmployeId, newId);
                }
            }

            // ──────────────────────────────────────────────────
            // 6. Sauvegarde
            // ──────────────────────────────────────────────────
            if (taches.Count > 0 || affectations.Count > 0)
            {
                await _db.SaveChangesAsync();
                _logger.LogInformation(
                    "✅ Succès : {Taches} tâches et {Affectations} affectations réassignées.",
                    taches.Count, affectations.Count);
            }
            else
            {
                _logger.LogInformation(
                    "ℹ️ Aucune tâche ni affectation trouvée pour les anciens responsables locaux [{Ids}].",
                    string.Join(", ", ancienIds));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erreur critique lors de la réassignation de tâches");
            throw; // Re-throw pour MassTransit retry
        }
    }
}