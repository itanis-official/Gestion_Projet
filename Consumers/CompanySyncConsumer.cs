using MassTransit;
using Microsoft.EntityFrameworkCore;
using ITANIS.SharedEvents;
using GestionProjet.Data;
using GestionProjet.Models;

public class CompanySyncConsumer : IConsumer<CompanySyncEvent>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CompanySyncConsumer> _logger;

    public CompanySyncConsumer(ApplicationDbContext db, ILogger<CompanySyncConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<CompanySyncEvent> ctx)
    {
        var m = ctx.Message;
        var action = m.GetActionAsString();
        
        _logger.LogInformation("🏢 Company {Action} Id={Id} {Nom}", action, m.Id, m.RaisonSociale);

        // ✅ Gestion de la suppression
        if (action == "Deleted")
        {
            var existing = await _db.Clients.FirstOrDefaultAsync(c => c.Id == m.Id);
            if (existing != null)
            {
                existing.Statut = "Supprimé";
                await _db.SaveChangesAsync();
            }
            return;
        }

        // ✅ Recherche du client existant
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == m.Id);

        if (client == null)
        {
            // ✅ Création sans IDENTITY_INSERT (car la table n'utilise pas l'auto-incrémentation)
            client = new Client { Id = m.Id };
            _db.Clients.Add(client);
        }

        // ✅ Mise à jour des propriétés (pour Created et Updated)
        client.RaisonSociale = m.RaisonSociale;
        client.Secteur = m.Secteur;
        client.EmailPrincipal = m.EmailPrincipal;
        client.TelephonePrincipal = m.TelephonePrincipal;
        client.Ville = m.Ville;
        client.Pays = m.Pays;
        client.Statut = m.Statut;
        client.SyncedAt = DateTime.UtcNow;

        // ✅ Une seule sauvegarde pour l'ajout ou la mise à jour
        await _db.SaveChangesAsync();
    }
}