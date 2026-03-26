using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using GestionProjet.Data;

namespace GestionProjet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TachesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TachesController(ApplicationDbContext context)
        {
            _context = context;
        }
[HttpGet("membre")]
[Authorize]
public async Task<IActionResult> GetTachesParMembre(
    [FromQuery] int projetId,
    [FromQuery] int employeId)
{
    var projet = await _context.Projets
        .Include(p => p.GroupeEquipe)
            .ThenInclude(g => g.Employes)
        .FirstOrDefaultAsync(p => p.Id == projetId);

    if (projet == null)
        return NotFound("Projet introuvable");

    if (projet.GroupeEquipe == null ||
        !projet.GroupeEquipe.Employes.Any(e => e.Id == employeId))
    {
        return BadRequest("Cet employé n'appartient pas à l'équipe du projet");
    }

    var taches = await _context.Taches
        .Include(t => t.Phase)
            .ThenInclude(ph => ph.Projet)
        .Include(t => t.SousTaches) // ✅ important
        .Where(t =>
            t.Phase.ProjetId == projetId &&
            (t.ResponsableId == employeId || t.TesteurId == employeId))
        .Select(t => new
        {
            t.Id,
            t.Titre,
            t.Statut,
            t.DateDebutPrevue,
            t.DateFinPrevue,

            Role = t.ResponsableId == employeId ? "Responsable" : "Testeur",

            Phase = t.Phase.TypePhase.ToString(),
            Projet = t.Phase.Projet.Nom,

            SousTaches = t.SousTaches.Select(st => new
            {
                st.Id,
                st.Titre,
                st.Statut,
                st.DureeEstimeeHeures
            }).ToList()
        })
        .ToListAsync();

    return Ok(taches);
}}}