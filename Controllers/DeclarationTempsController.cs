using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Services;

namespace GestionProjet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeclarationTempsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ProjetService _projetService;

        public DeclarationTempsController(ApplicationDbContext context,ProjetService projetService)
        {
            _context = context;
            _projetService = projetService;
        }

       [HttpPost]
[Authorize]
public async Task<IActionResult> DeclarerHeures([FromBody] CreateDeclarationTempsDto dto)
{
    try
    {
        var employeId = User.FindFirst("EmployeId")?.Value;

        if (string.IsNullOrEmpty(employeId))
            return Unauthorized();

        int empId = int.Parse(employeId);

        var sousTache = await _context.SousTaches
            .Include(st => st.Tache)
                .ThenInclude(t => t.Phase)
            .FirstOrDefaultAsync(st => st.Id == dto.SousTacheId);

        if (sousTache == null)
            return NotFound();

        int projetId = sousTache.Tache.Phase.ProjetId;

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

       
        await _projetService.MettreAJourProjet(projetId);

        return Ok("Heures déclarées + projet mis à jour");
    }
    catch (Exception ex)
    {
        return StatusCode(500, ex.Message);
    }
}
}
}