using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Services;
using ITANIS.SharedEvents;
using MassTransit; 
using System.Security.Claims; 
namespace GestionProjet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeclarationTempsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ProjetService _projetService;
        private readonly IPublishEndpoint _publishEndpoint;

        public DeclarationTempsController(ApplicationDbContext context,ProjetService projetService,IPublishEndpoint publishEndpoint)
        {
            _context = context;
            _projetService = projetService;
            _publishEndpoint = publishEndpoint;
        }

[HttpPost]
[Authorize]
public async Task<IActionResult> DeclarerHeures([FromBody] CreateDeclarationTempsDto dto)
{
    try
    {
        // ✅ Récupérer l'email depuis le token Authentik
        var email = User.FindFirst("email")?.Value 
                 ?? User.FindFirst(ClaimTypes.Email)?.Value;
        
        if (string.IsNullOrEmpty(email))
        {
            return Unauthorized("Email non trouvé dans le token");
        }

        // ✅ Trouver l'employé par email
        var employe = await _context.Employes
            .FirstOrDefaultAsync(e => e.Email == email);
        
        if (employe == null)
        {
            return Unauthorized($"Employé avec email {email} non trouvé");
        }

        // Vérifier l'ID RH
        if (!employe.IdOrigineRH.HasValue)
        {
            return BadRequest("Cet employé n'a pas d'identifiant RH valide. Impossible de synchroniser.");
        }

        // Charger les données nécessaires
        var sousTache = await _context.SousTaches
            .Include(st => st.Tache)
                .ThenInclude(t => t.Phase)
                    .ThenInclude(p => p.Projet)
            .FirstOrDefaultAsync(st => st.Id == dto.SousTacheId);

        if (sousTache == null) 
            return NotFound($"Sous-tâche {dto.SousTacheId} non trouvée");

        int projetId = sousTache.Tache.Phase.ProjetId;

        // Créer et sauvegarder
        var declaration = new DeclarationTemps
        {
            SousTacheId = dto.SousTacheId,
            EmployeId = employe.Id,
            Date = dto.Date,
            DureeHeures = dto.DureeHeures,
            Type = dto.Type
        };

        _context.DeclarationsTemps.Add(declaration);
        await _context.SaveChangesAsync();

        // Mettre à jour le projet
        await _projetService.MettreAJourProjet(projetId);

        // ✅ Publier l'événement
        await _publishEndpoint.Publish(new DeclarationTempsSyncEvent
        {
            DeclarationId = declaration.Id,
            EmployeIdRh = employe.IdOrigineRH.Value,
            EmployeNomComplet = employe.NomComplet,
            EmployeAgentType = employe.AgentType ?? "interne",
            SousTacheId = sousTache.Id,
            SousTacheTitre = sousTache.Titre,
            ProjetId = projetId,
            ProjetNom = sousTache.Tache.Phase.Projet.Nom,
            Date = dto.Date,
            DureeHeures = dto.DureeHeures,
            Type = dto.Type
        });

        return Ok(new { message = "Heures déclarées et synchronisées avec TimeSheet" });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
    }
}
    }
}