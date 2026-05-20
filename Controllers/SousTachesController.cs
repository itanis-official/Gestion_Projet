using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Enums;
using GestionProjet.DTOs;
using GestionProjet.Services;

namespace GestionProjet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SousTachesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly StatutService _statutService;
        private readonly ILogger<SousTachesController> _logger;

        public SousTachesController(
            ApplicationDbContext context, 
            StatutService statutService, 
            NotificationService service,
            ILogger<SousTachesController> logger)
        {
            _context = context;
            _statutService = statutService;
            _notificationService = service;
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

        [HttpGet("sous-tache/{subTaskId}")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> GetBySousTache(int subTaskId)
        {
            var temps = await _context.DeclarationsTemps
                .Where(t => t.SousTacheId == subTaskId)
                .Include(t => t.Employe)
                .ToListAsync();

            var result = temps.Select(t => new DeclarationTempsDto
            {
                Id = t.Id,
                SousTacheId = t.SousTacheId,
                EmployeId = t.EmployeId,
                Date = t.Date,
                DureeHeures = t.DureeHeures,
                Type = t.Type
            });

            return Ok(result);
        }

        [HttpGet("a-tester")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> GetSousTachesATester([FromQuery] int? projetId)
        {
            var employeId = await GetAuthenticatedEmployeId();
            if (employeId == null) return Unauthorized(new { message = "Employé non identifié." });

            int empId = employeId.Value;

            var query = _context.SousTaches
                .Include(st => st.Tache)
                    .ThenInclude(t => t.Phase)
                        .ThenInclude(p => p.Projet)
                .Where(st =>
                    st.Tache.TesteurId == empId &&
                    st.Statut == StatutSousTache.ATester);

            if (projetId.HasValue)
            {
                query = query.Where(st => st.Tache.Phase.ProjetId == projetId.Value);
            }

            var result = await query
                .Select(st => new SousTacheATesterDto
                {
                    Id = st.Id,
                    Titre = st.Titre,
                    Statut = "ATester",
                    DureeEstimeeHeures = st.DureeEstimeeHeures,
                    Tache = st.Tache.Titre,
                    Phase = st.Tache.Phase.TypePhase.ToString(),
                    Projet = st.Tache.Phase.Projet.Nom
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpPost("{id}/valider")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> ValiderSousTache(int id)
        {
            var employeId = await GetAuthenticatedEmployeId();
            if (employeId == null) return Unauthorized(new { message = "Employé non identifié." });

            int empId = employeId.Value;

            var sousTache = await _context.SousTaches
                .Include(st => st.Tache)
                    .ThenInclude(t => t.Responsable)
                .Include(st => st.Tache)
                    .ThenInclude(t => t.Phase)
                        .ThenInclude(p => p.Projet)
                .FirstOrDefaultAsync(st => st.Id == id);

            if (sousTache == null) return NotFound();

            if (sousTache.Tache.TesteurId != empId)
                return Forbid();

            var ancienStatut = sousTache.Statut;
            sousTache.Statut = StatutSousTache.Validee;

            await _context.SaveChangesAsync();
            await _statutService.MettreAJourStatuts(sousTache.TacheId);

            if (sousTache.Tache.ResponsableId.HasValue)
            {
                await _notificationService.NotifierSousTacheValidee(
                    sousTache.Id,
                    sousTache.Tache.ResponsableId.Value,
                    sousTache.Tache.Titre,
                    sousTache.Titre);
            }

            return Ok("Validée");
        }

        [HttpPost("{id}/rejeter")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> RejeterSousTache(int id, [FromBody] RejetSousTacheDto dto)
        {
            var employeId = await GetAuthenticatedEmployeId();
            if (employeId == null) return Unauthorized(new { message = "Employé non identifié." });

            int empId = employeId.Value;

            var sousTache = await _context.SousTaches
                .Include(st => st.Tache)
                    .ThenInclude(t => t.Responsable)
                .Include(st => st.Tache)
                    .ThenInclude(t => t.Phase)
                        .ThenInclude(p => p.Projet)
                .FirstOrDefaultAsync(st => st.Id == id);

            if (sousTache == null) return NotFound();

            sousTache.Statut = StatutSousTache.Rejetee;

            _context.Tests.Add(new Test
            {
                TacheId = sousTache.TacheId,
                SousTacheId = sousTache.Id,
                EmployeId = empId,
                DateTest = DateTime.UtcNow,
                Resultat = ResultatTest.Echoue,
                Commentaire = dto.RaisonRejet
            });

            await _context.SaveChangesAsync();
            await _statutService.MettreAJourStatuts(sousTache.TacheId);

            if (sousTache.Tache.ResponsableId.HasValue)
            {
                await _notificationService.NotifierSousTacheRejetee(
                    sousTache.Id,
                    sousTache.Tache.ResponsableId.Value,
                    sousTache.Tache.Titre,
                    sousTache.Titre,
                    dto.RaisonRejet);
            }

            return Ok("Rejetée");
        }

        [HttpGet("utilisateur/{userId}")]
        [Authorize(AuthenticationSchemes = "Bearer", Roles = "ceo")]
        public async Task<IActionResult> GetByUser(int userId, [FromQuery] int? projetId = null)
        {
            IQueryable<SousTache> query = _context.SousTaches;

            query = query.Include(st => st.Tache)
                .ThenInclude(t => t.Phase)
                .ThenInclude(p => p.Projet);

            query = query.Include(st => st.Tache.Responsable);

            query = query.Where(st => st.Tache != null && st.Tache.ResponsableId == userId);

            if (projetId.HasValue)
            {
                query = query.Where(st => st.Tache.Phase != null && st.Tache.Phase.ProjetId == projetId.Value);
            }

            var data = await query.Select(st => new
            {
                st.Id,
                st.Titre,
                Statut = st.Statut.ToString(),
                st.DureeEstimeeHeures,
                DateDebutPrevue = st.Tache.DateDebutPrevue,
                DateFinPrevue = st.Tache.DateFinPrevue,
                TacheId = st.Tache.Id,
                TacheTitre = st.Tache.Titre,
                ProjetId = st.Tache.Phase.Projet.Id,
                ProjetNom = st.Tache.Phase.Projet.Nom,
                EmployeNom = st.Tache.Responsable != null ? st.Tache.Responsable.NomComplet : null,
                Priorite = "medium",
                RaisonRejet = (string)null,
            }).ToListAsync();

            return Ok(data);
        }

        [HttpPatch("{id}/statut")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> UpdateStatut(int id, [FromBody] UpdateStatutDto dto)
        {
            var sousTache = await _context.SousTaches
                .Include(st => st.Tache)
                .FirstOrDefaultAsync(st => st.Id == id);

            if (sousTache == null) return NotFound();

            if (!Enum.TryParse<StatutSousTache>(dto.Statut, true, out var statutEnum))
                return BadRequest();

            sousTache.Statut = statutEnum;

            await _context.SaveChangesAsync();

            await _statutService.MettreAJourStatuts(sousTache.TacheId);
            
            if (statutEnum == StatutSousTache.ATester && sousTache.Tache?.TesteurId.HasValue == true)
            {  
                var testeurId = sousTache.Tache.TesteurId.Value;
                
                await _notificationService.NotifierSousTacheATester(
                    sousTache.Id, 
                    testeurId, 
                    sousTache.Tache.Titre, 
                    sousTache.Titre
                );
            }

            return Ok();
        }

        [HttpGet("{id}/commentaires")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<IActionResult> GetCommentairesSousTache(int id)
        {
            var commentaires = await _context.Tests
                .Where(t => t.SousTacheId == id)
                .Include(t => t.Employe)
                .OrderByDescending(t => t.DateTest)
                .Select(t => new
                {
                    t.Id,
                    t.Commentaire,
                    DateTest = DateTime.SpecifyKind(t.DateTest, DateTimeKind.Utc)
                })
                .ToListAsync();

            return Ok(commentaires);
        }

        [HttpGet("mes-sous-taches")]
        [Authorize(AuthenticationSchemes = "Bearer")]
        public async Task<ActionResult> GetMesSousTachesResponsable([FromQuery] int? projetId)
        {
            var employeId = await GetAuthenticatedEmployeId();
            if (employeId == null)
                return Unauthorized(new { message = "Employé non identifié." });

            int empId = employeId.Value;

            var query = _context.SousTaches
                .Include(st => st.Tache)
                    .ThenInclude(t => t.Phase)
                        .ThenInclude(p => p.Projet)
                .Where(st => st.Tache.ResponsableId == empId);

            if (projetId.HasValue)
            {
                query = query.Where(st => st.Tache.Phase.Projet.Id == projetId.Value);
            }

            var result = await query.Select(st => new
            {
                st.Id,
                st.Titre,
                Statut = st.Statut.ToString(),
                st.DureeEstimeeHeures,
                TacheId = st.Tache.Id,
                TacheTitre = st.Tache.Titre,
                ProjetId = st.Tache.Phase.Projet.Id,
                ProjetNom = st.Tache.Phase.Projet.Nom,
                DateDebutPrevue = st.Tache.DateDebutPrevue,
                DateFinPrevue = st.Tache.DateFinPrevue,
            }).ToListAsync();

            return Ok(result);
        }
    }
}