using MassTransit;
using ITANIS.SharedEvents;
using GestionProjet.Data;
using GestionProjet.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GestionProjet.Consumers;

public class AgentSyncConsumer : IConsumer<AgentSyncEvent>
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AgentSyncConsumer> _logger;

    public AgentSyncConsumer(ApplicationDbContext db, ILogger<AgentSyncConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AgentSyncEvent> context)
    {
        var msg = context.Message;
        string nomComplet = $"{msg.Prenom} {msg.Nom}".Trim();

        _logger.LogInformation("👤 Synchronisation Employé : {Nom} (ID RH: {Id})", nomComplet, msg.Id);

        var employeExistant = await _db.Employes.FirstOrDefaultAsync(e => e.IdOrigineRH == msg.Id);

        if (employeExistant != null)
        {
            employeExistant.NomComplet = nomComplet;
            employeExistant.Email = msg.Email;
            employeExistant.Role = msg.Poste;
            employeExistant.CoutHoraire = msg.CoutHoraire ?? 0m;
            
            _logger.LogInformation("✏️ Employé local ID {LocalId} (RH ID: {RhId}) mis à jour.", employeExistant.Id, msg.Id);
        }
        else
        {
            var nouvelEmploye = new Employe
            {
                IdOrigineRH = msg.Id, 
                NomComplet = nomComplet,
                Email = msg.Email,
                Role = msg.Poste, 
                CoutHoraire = msg.CoutHoraire ?? 0m 
            };

            _db.Employes.Add(nouvelEmploye);
            _logger.LogInformation("➕ Nouvel employé local (RH ID: {RhId}) ajouté.", msg.Id);
        }

        await _db.SaveChangesAsync();
    }
}