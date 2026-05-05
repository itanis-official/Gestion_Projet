using MassTransit;
using ITANIS.SharedEvents;
using GestionProjet.Data;
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
            "🔄 Réassignation responsable : Ancien={AncienId} → Nouveau={NouveauId}",
            evt.AncienResponsableId, evt.NouveauResponsableId);

        var taches = await _db.Taches
            .Where(t => t.ResponsableId == evt.AncienResponsableId)
            .ToListAsync();

        int tachesCount = taches.Count;
        foreach (var t in taches)
        {
            t.ResponsableId = evt.NouveauResponsableId;
        }

        var affectations = await _db.Affectations
            .Where(a => a.EmployeId == evt.AncienResponsableId)
            .ToListAsync();

        int affectationsCount = affectations.Count;
        foreach (var aff in affectations)
        {
            aff.EmployeId = evt.NouveauResponsableId;
        }

        if (tachesCount > 0 || affectationsCount > 0)
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation(
                "✅ Réassignation terminée : {Taches} tâche(s) et {Affectations} affectation(s) mises à jour",
                tachesCount, affectationsCount);
        }
        else
        {
            _logger.LogInformation("ℹ️ Aucune tâche/affectation trouvée pour l'ancien responsable {AncienId}", 
                evt.AncienResponsableId);
        }
    }
}