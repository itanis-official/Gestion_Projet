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
    _logger.LogInformation("🏢 Company {Action} Id={Id} {Nom}", m.Action, m.Id, m.RaisonSociale);

    var existing = await _db.Clients.FirstOrDefaultAsync(c => c.Id == m.Id);

    if (m.Action == SyncAction.Deleted)
    {
        if (existing != null)
        {
            existing.Statut = "Supprimé";
            await _db.SaveChangesAsync();
        }
        return;
    }

    if (existing == null)
    {
        // CAS D'AJOUT : On doit insérer un ID spécifique
        existing = new Client { Id = m.Id };

        try
        {
            // 1. On ouvre la connexion (nécessaire pour la commande IDENTITY_INSERT)
            await _db.Database.OpenConnectionAsync();

            // 2. On autorise l'insertion explicite de l'ID
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Clients] ON");

            // 3. On ajoute l'entité avec l'ID imposé par le message
            _db.Clients.Add(existing);
            await _db.SaveChangesAsync();
        }
        finally
        {
            // 4. IMPORTANT : On remet le mode normal (OFF) pour les autres requêtes
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Clients] OFF");
            await _db.Database.CloseConnectionAsync();
        }
    }

    // Mise à jour des propriétés (se produit si le client existe déjà ou s'il vient d'être créé)
    existing.RaisonSociale = m.RaisonSociale;
    existing.Secteur = m.Secteur;
    existing.EmailPrincipal = m.EmailPrincipal;
    existing.TelephonePrincipal = m.TelephonePrincipal;
    existing.Ville = m.Ville;
    existing.Pays = m.Pays;
    existing.Statut = m.Statut;
    existing.SyncedAt = DateTime.UtcNow;

    await _db.SaveChangesAsync();
}
}