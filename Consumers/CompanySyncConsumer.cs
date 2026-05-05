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
        existing = new Client { Id = m.Id };

        try
        {
            await _db.Database.OpenConnectionAsync();

            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Clients] ON");

            _db.Clients.Add(existing);
            await _db.SaveChangesAsync();
        }
        finally
        {
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT [Clients] OFF");
            await _db.Database.CloseConnectionAsync();
        }
    }

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