using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Services;
using ITANIS.SharedEvents;
using MassTransit; 
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
        var employeIdClaim = User.FindFirst("EmployeId")?.Value;
        if (string.IsNullOrEmpty(employeIdClaim)) return Unauthorized();

        int empId = int.Parse(employeIdClaim);

        // 1. Charger l'employé pour récupérer IdOrigineRH et AgentType
        var employe = await _context.Employes.FindAsync(empId);
        if (employe == null) return Unauthorized("Employé introuvable");

        if (!employe.IdOrigineRH.HasValue)
        {
            return BadRequest("Cet employé n'a pas d'identifiant RH valide. Impossible de synchroniser.");
        }

        // 2. Charger les données nécessaires
        var sousTache = await _context.SousTaches
            .Include(st => st.Tache)
                .ThenInclude(t => t.Phase)
                    .ThenInclude(p => p.Projet)  // ← Il manquait ce ThenInclude !
            .FirstOrDefaultAsync(st => st.Id == dto.SousTacheId);

        if (sousTache == null) return NotFound();

        int projetId = sousTache.Tache.Phase.ProjetId;

        // 3. Créer et sauvegarder
        var declaration = new DeclarationTemps
        {
            SousTacheId = dto.SousTacheId,
            EmployeId = empId,
            Date = dto.Date,
            DureeHeures = dto.DureeHeures,
            Type = dto.Type
        };

        _context.DeclarationsTemps.Add(declaration);
        await _context.SaveChangesAsync();

        // 4. Mettre à jour le projet
        await _projetService.MettreAJourProjet(projetId);

        // 5. ✅ Publier l'événement avec les IDs RH (compris par le destinataire)
        await _publishEndpoint.Publish(new DeclarationTempsSyncEvent
        {
            DeclarationId = declaration.Id,
            
            // ✅ ID RH (pas l'ID local !)
            EmployeIdRh = employe.IdOrigineRH.Value,
            EmployeNomComplet = employe.NomComplet,
            EmployeAgentType = employe.AgentType,
            
            SousTacheId = sousTache.Id,
            SousTacheTitre = sousTache.Titre,
            
            ProjetId = projetId,
            ProjetNom = sousTache.Tache.Phase.Projet.Nom,
            
            Date = dto.Date,
            DureeHeures = dto.DureeHeures,
            Type = dto.Type
        });

        return Ok(new { message = "Heures déclarées, projet mis à jour et envoyé au TimeSheet." });
    }
    catch (Exception ex)
    {
        return StatusCode(500, ex.Message);
    }
}
    }
}