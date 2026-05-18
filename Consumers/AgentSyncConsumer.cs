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
        var action = msg.GetActionAsString();
        string nomComplet = $"{msg.Prenom} {msg.Nom}".Trim();

        // ──────────────────────────────────────────────────────
        // 1. SUPPRESSION : Retirer TOUS les enregistrements locaux
        // ──────────────────────────────────────────────────────
        if (action == "Deleted")
        {
            var employesASupprimer = await _db.Employes
                .Where(e => e.IdOrigineRH == msg.Id)
                .ToListAsync();

            if (employesASupprimer.Any())
            {
                _db.Employes.RemoveRange(employesASupprimer);
                await _db.SaveChangesAsync();
                _logger.LogWarning(
                    "🗑️ {Count} enregistrement(s) supprimé(s) pour RH ID {RhId}",
                    employesASupprimer.Count, msg.Id);
            }
            else
            {
                _logger.LogInformation(
                    "ℹ️ RH ID {RhId} déjà absent localement lors de la suppression.", msg.Id);
            }
            return;
        }

        // ──────────────────────────────────────────────────────
        // 2. CRÉATION / MISE À JOUR
        // ──────────────────────────────────────────────────────
        string? eventAgentType = msg.AgentType; // Peut être null

        // Récupérer TOUS les enregistrements locaux pour ce RH ID
        // (potentiellement 1 "interne" + 1 "externe")
        var existants = await _db.Employes
            .Where(e => e.IdOrigineRH == msg.Id)
            .ToListAsync();

        if (existants.Any())
        {
            // ── Mise à jour des propriétés partagées sur TOUS les enregistrements ──
            foreach (var emp in existants)
            {
                emp.NomComplet = nomComplet;
                emp.Email = msg.Email;
                emp.Role = msg.Poste;
                emp.CoutHoraire = msg.CoutHoraire ?? 0m;

                // ⚠️ NE PAS écraser emp.AgentType ici !
                // Il est fixé par EquipeSyncEventConsumer et différencie
                // les doublons (interne vs externe) du même agent RH.
            }

            _logger.LogInformation(
                "✏️ {Count} enregistrement(s) mis à jour pour RH ID {RhId} : {Noms}",
                existants.Count, msg.Id,
                string.Join(", ", existants.Select(e => $"{e.NomComplet}({e.AgentType})")));

            // ── Si l'event spécifie un AgentType absent localement → créer le profil ──
            if (!string.IsNullOrEmpty(eventAgentType) &&
                !existants.Any(e => e.AgentType == eventAgentType))
            {
                var nouveauProfil = new Employe
                {
                    IdOrigineRH = msg.Id,
                    NomComplet = nomComplet,
                    Email = msg.Email,
                    Role = msg.Poste,
                    CoutHoraire = msg.CoutHoraire ?? 0m,
                    AgentType = eventAgentType
                };
                _db.Employes.Add(nouveauProfil);
                _logger.LogInformation(
                    "➕ Nouveau profil '{Type}' créé pour RH ID {RhId}", eventAgentType, msg.Id);
            }
        }
        else
        {
            // ── Aucun enregistrement local n'existe → création ──
            var nouvelEmploye = new Employe
            {
                IdOrigineRH = msg.Id,
                NomComplet = nomComplet,
                Email = msg.Email,
                Role = msg.Poste,
                CoutHoraire = msg.CoutHoraire ?? 0m,
                AgentType = eventAgentType ?? "interne"
            };
            _db.Employes.Add(nouvelEmploye);
            _logger.LogInformation(
                "➕ Nouvel employé ajouté : {Nom} (RH ID: {RhId}, Type: {Type})",
                nomComplet, msg.Id, eventAgentType ?? "interne");
        }

        await _db.SaveChangesAsync();
    }
}