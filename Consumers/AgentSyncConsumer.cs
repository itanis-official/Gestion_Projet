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

        // ✅ AJOUT : Log systématique à l'entrée pour confirmer la réception
        _logger.LogWarning("🚨 AgentSyncConsumer CONSUME CALLED — Action: {Action}, RH ID: {RhId}, MessageId: {MessageId}", 
            action, msg.Id, context.MessageId);

        // ✅ AJOUT : Sécurité si l'action est vide ou non reconnue
        if (string.IsNullOrEmpty(action) || (action != "Created" && action != "Updated" && action != "Deleted"))
        {
            _logger.LogError("❌ AgentSyncEvent — Action non reconnue ou vide. Action: '{Action}', Action ValueKind: {Kind}", 
                action, msg.Action.ValueKind);
            return; // On ne peut pas traiter sans action valide
        }

        // ── Protection valeurs null ──
        string safeEmail       = msg.Email ?? string.Empty;
        string safeRole        = msg.Role ?? string.Empty;    
        string safePoste       = msg.Poste ?? string.Empty;   
        string safeNomComplet  = string.IsNullOrWhiteSpace(nomComplet) ? "Inconnu" : nomComplet;
        string safeAgentType   = msg.AgentType ?? "interne";

        // ──────────────────────────────────────────────────────
        // 1. SUPPRESSION
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
                _logger.LogWarning("🗑️ {Count} enregistrement(s) supprimé(s) pour RH ID {RhId}",
                    employesASupprimer.Count, msg.Id);
            }
            else
            {
                _logger.LogInformation("ℹ️ RH ID {RhId} déjà absent localement.", msg.Id);
            }
            return;
        }

        // ──────────────────────────────────────────────────────
        // 2. Recherche de l'existant — stratégies multiples
        // ──────────────────────────────────────────────────────

        // A : par IdOrigineRH + AgentType (match exact)
        var existantExact = await _db.Employes
            .FirstOrDefaultAsync(e => e.IdOrigineRH == msg.Id && e.AgentType == safeAgentType);

        // B : par IdOrigineRH seul (autres AgentType)
        var existantsMemeRh = await _db.Employes
            .Where(e => e.IdOrigineRH == msg.Id)
            .ToListAsync();

        // C : fallback par Email si IdOrigineRH pas trouvé
        Employe? existantByEmail = null;
        if (existantExact == null && !existantsMemeRh.Any() && msg.Id > 0 && !string.IsNullOrEmpty(safeEmail))
        {
            existantByEmail = await _db.Employes
                .FirstOrDefaultAsync(e => e.Email == safeEmail);

            if (existantByEmail != null)
            {
                _logger.LogInformation(
                    "🔍 RH ID {RhId} non trouvé par IdOrigineRH, trouvé par Email '{Email}' — correction du lien",
                    msg.Id, safeEmail);
                existantByEmail.IdOrigineRH = msg.Id;
            }
        }

        _logger.LogInformation(
            "🔍 Résultat : exact={Exact}, memeRh={MemeRh}, byEmail={ByEmail}",
            existantExact != null, existantsMemeRh.Count, existantByEmail != null);

        // ──────────────────────────────────────────────────────
        // 3. MISE À JOUR ou CRÉATION
        // ──────────────────────────────────────────────────────

        if (existantExact != null)
        {
            existantExact.NomComplet  = safeNomComplet;
            existantExact.Email       = safeEmail;
            existantExact.Role        = safePoste;  
            existantExact.CoutHoraire = msg.CoutHoraire ?? 0m;

            _logger.LogInformation("✏️ Mise à jour employé ID {LocalId} pour RH ID {RhId} ({AgentType})",
                existantExact.Id, msg.Id, safeAgentType);
        }
        else if (existantByEmail != null)
        {
            existantByEmail.NomComplet  = safeNomComplet;
            existantByEmail.Email       = safeEmail;
            existantByEmail.Role        = safePoste;
            existantByEmail.CoutHoraire = msg.CoutHoraire ?? 0m;
            existantByEmail.AgentType   = safeAgentType;

            _logger.LogInformation("✏️ Mise à jour (fallback Email) employé ID {LocalId} → RH ID {RhId}",
                existantByEmail.Id, msg.Id);
        }
        else if (existantsMemeRh.Any())
        {
            var nouveauProfil = new Employe
            {
                IdOrigineRH = msg.Id,
                NomComplet  = safeNomComplet,
                Email       = safeEmail,
                Role        = safePoste,
                CoutHoraire = msg.CoutHoraire ?? 0m,
                AgentType   = safeAgentType
            };
            _db.Employes.Add(nouveauProfil);
            _logger.LogInformation("➕ Nouveau profil '{AgentType}' pour RH ID {RhId}",
                safeAgentType, msg.Id);
        }
        else
        {
            var nouvelEmploye = new Employe
            {
                IdOrigineRH = msg.Id > 0 ? msg.Id : null,
                NomComplet  = safeNomComplet,
                Email       = safeEmail,
                Role        = safePoste,
                CoutHoraire = msg.CoutHoraire ?? 0m,
                AgentType   = safeAgentType
            };
            _db.Employes.Add(nouvelEmploye);
            _logger.LogInformation("➕ Nouvel employé : {Nom} (RH ID: {RhId}, Type: {AgentType})",
                safeNomComplet, msg.Id, safeAgentType);
        }

        try
        {
            await _db.SaveChangesAsync();
            _logger.LogInformation("✅ SaveChanges OK pour RH ID {RhId}", msg.Id);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex,
                "❌ DbUpdateException pour RH ID {RhId} — {Nom}, {Email}, {AgentType}. Inner: {InnerMessage}",
                msg.Id, safeNomComplet, safeEmail, safeAgentType,
                ex.InnerException?.Message ?? "N/A");
            throw; // Laisse MassTransit retenter ou déplacer dans la queue _error
        }
        catch (Exception ex) // ✅ AJOUT : Capturer les autres erreurs (validation, etc.)
        {
            _logger.LogError(ex,
                "❌ Exception inattendue pour RH ID {RhId}: {Message}",
                msg.Id, ex.Message);
            throw;
        }
    }
}