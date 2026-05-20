using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using GestionProjet.Data;
using GestionProjet.Models;

namespace GestionProjet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TachesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TachesController> _logger;

        public TachesController(ApplicationDbContext context, ILogger<TachesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ──────────────────────────────────────────────────────
        // ✅ MÉTHODE CENTRALE : Résoudre l'EmployeId depuis le token Authentik
        // ──────────────────────────────────────────────────────
        private async Task<int?> GetAuthenticatedEmployeId()
        {
            // 1. Essayer l'ancien claim local
            var employeIdClaim = User.FindFirst("EmployeId")?.Value;
            if (!string.IsNullOrEmpty(employeIdClaim) && int.TryParse(employeIdClaim, out var localId))
                return localId;

            // 2. Récupérer l'email
            var email = User.FindFirst("email")?.Value 
                     ?? User.FindFirst(ClaimTypes.Email)?.Value;
            
            if (string.IsNullOrWhiteSpace(email))
                email = null;

            // 3. Récupérer le nom d'utilisateur Authentik
            var username = User.FindFirst("preferred_username")?.Value 
                        ?? User.FindFirst("name")?.Value;

            // 4. Chercher l'employé local
            Employe? employe = null;

            if (!string.IsNullOrEmpty(email))
            {
                employe = await _context.Employes.FirstOrDefaultAsync(e => e.Email == email);
            }

            if (employe == null && !string.IsNullOrEmpty(username))
            {
                var cleanUsername = username.Replace("-", " ").Replace(".", " ");
                employe = await _context.Employes.FirstOrDefaultAsync(e => 
                    e.Email.Contains(username) || e.NomComplet.Contains(cleanUsername));
            }

            if (employe == null)
            {
                _logger.LogWarning("❌ Aucun employé trouvé en base pour Email={Email}, Username={Username}", 
                    email, username);
                return null;
            }

            return employe.Id;
        }

        [HttpGet("membre")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> GetTachesParMembre(
            [FromQuery] int projetId,
            [FromQuery] int? employeId) // ✅ Passé en optionnel (int?)
        {
            // ── Vérification des droits ──────────────────────────
            var groups = User.FindAll("groups").Select(c => c.Value).ToList();
            var isAdmin = groups.Contains("ceo");
            var authenticatedEmployeId = await GetAuthenticatedEmployeId();

            if (authenticatedEmployeId == null)
                return Unauthorized(new { message = "Employé non identifié." });

            int targetEmployeId;

            // Si un employeId est spécifié dans la requête
            if (employeId.HasValue)
            {
                // Si ce n'est pas l'utilisateur connecté lui-même, 
                // il doit être admin pour voir les tâches de quelqu'un d'autre
                if (employeId.Value != authenticatedEmployeId.Value && !isAdmin)
                {
                    return Forbid(); // 403 Forbidden
                }
                targetEmployeId = employeId.Value;
            }
            else
            {
                // Si aucun employeId n'est spécifié, on prend l'utilisateur connecté
                targetEmployeId = authenticatedEmployeId.Value;
            }

            // ── Logique métier ───────────────────────────────────
            var projet = await _context.Projets
                .Include(p => p.GroupeEquipe)
                    .ThenInclude(g => g.Employes)
                .FirstOrDefaultAsync(p => p.Id == projetId);

            if (projet == null)
                return NotFound("Projet introuvable");

            if (projet.GroupeEquipe == null ||
                !projet.GroupeEquipe.Employes.Any(e => e.Id == targetEmployeId))
            {
                return BadRequest("Cet employé n'appartient pas à l'équipe du projet");
            }

            var taches = await _context.Taches
                .Include(t => t.Phase)
                    .ThenInclude(ph => ph.Projet)
                .Include(t => t.SousTaches) 
                .Where(t =>
                    t.Phase.ProjetId == projetId &&
                    (t.ResponsableId == targetEmployeId || t.TesteurId == targetEmployeId))
                .Select(t => new
                {
                    t.Id,
                    t.Titre,
                    t.Statut,
                    t.DateDebutPrevue,
                    t.DateFinPrevue,

                    Role = t.ResponsableId == targetEmployeId ? "Responsable" : "Testeur",

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
        }
    }
}