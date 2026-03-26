using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Enums;
using GestionProjet.DTOs;
namespace GestionProjet.Controllers
{[ApiController]
[Route("api/[controller]")]
public class DeclarationTempsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DeclarationTempsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> DeclarerHeures([FromBody] CreateDeclarationTempsDto dto)
    {
        var employeId = User.FindFirst("EmployeId")?.Value;

        if (string.IsNullOrEmpty(employeId))
            return Unauthorized("EmployeId introuvable");

        int empId = int.Parse(employeId);

        // 🔍 vérifier que la sous-tâche existe
        var sousTache = await _context.SousTaches
            .Include(st => st.Tache)
            .FirstOrDefaultAsync(st => st.Id == dto.SousTacheId);

        if (sousTache == null)
            return NotFound("Sous-tâche introuvable");

        // 🔒 sécurité : vérifier que l’employé est assigné à la tâche
        if (sousTache.Tache.ResponsableId != empId &&
            sousTache.Tache.TesteurId != empId)
        {
            return BadRequest("Vous n'êtes pas assigné à cette tâche");
        }

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

        return Ok(new { message = "Heures déclarées avec succès" });
    }
}}