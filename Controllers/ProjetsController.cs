using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Enums;
using GestionProjet.DTOs;
using GestionProjet.Services;
using ITANIS.SharedEvents;

namespace GestionProjet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjetsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly ProjetSyncService _syncService;
        private readonly ILogger<ProjetsController> _logger;

        public ProjetsController(
            ApplicationDbContext context,
            NotificationService service,
            ProjetSyncService syncService,
            ILogger<ProjetsController> logger)
        {
            _context = context;
            _notificationService = service;
            _syncService = syncService;
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

            // 2. Chercher l'email sous toutes ses formes possibles dans le token Authentik
            var email = User.FindFirst("email")?.Value                     // Standard
                     ?? User.FindFirst(ClaimTypes.Email)?.Value            // Microsoft standard
                     ?? User.FindFirst("preferred_username")?.Value        // Souvent l'email ou le nom d'user
                     ?? User.FindFirst("upn")?.Value;                      // User Principal Name

            if (string.IsNullOrEmpty(email))
            {
                // ✅ Si toujours rien, on log ce qu'on a reçu pour débugger
                var allClaims = User.Claims.Select(c => $"{c.Type}: {c.Value}");
                _logger.LogWarning("❌ Aucun email trouvé. Claims reçus : {Claims}", string.Join(", ", allClaims));
                return null;
            }

            // 3. Chercher l'employé local par email
            var employe = await _context.Employes
                .FirstOrDefaultAsync(e => e.Email == email);

            if (employe == null)
            {
                _logger.LogWarning("❌ Aucun employé trouvé en base pour l'email: {Email}", email);
                return null;
            }

            return employe.Id;
        }
        // ──────────────────────────────────────────────────────
        // ENDPOINTS
        // ──────────────────────────────────────────────────────

        [HttpPost("upload-cdc/{projectId}")]
        [Authorize]
        public async Task<IActionResult> UploadCdc(int projectId, IFormFile file)
        {
            var projet = await _context.Projets.FindAsync(projectId);
            if (projet == null) return NotFound(new { message = "Projet non trouvé" });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Aucun fichier fourni" });

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".pdf" && extension != ".docx")
                return BadRequest(new { message = "Seuls les fichiers PDF et DOCX sont acceptés." });

            try
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "cdc");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var safeFileName = $"{projectId}_{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsFolder, safeFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                projet.CdcFileUrl = $"/uploads/cdc/{safeFileName}";
                await _context.SaveChangesAsync();

                return Ok(new { cdcFileUrl = projet.CdcFileUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Erreur lors de la sauvegarde du fichier" });
            }
        }

        [HttpGet("mes-projets-chef")]
        [Authorize(AuthenticationSchemes = "Bearer")] 
        public async Task<ActionResult> GetMesProjetsChef()
        {
            // ✅ Utiliser la nouvelle méthode au lieu du claim direct
            var employeId = await GetAuthenticatedEmployeId();

            if (employeId == null)
                return Unauthorized(new { message = "Employé non identifié. Vérifiez que votre email Authentik correspond à un employé en base." });

            int empId = employeId.Value;

            _logger.LogInformation("📋 GetMesProjetsChef — EmployeId: {EmpId}", empId);

            var projets = await _context.Projets
                .Include(p => p.Client)
                .Include(p => p.GroupeEquipe)
                    .ThenInclude(g => g.Employes)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.SousTaches)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.Responsable)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.Testeur)
                .Where(p => p.GroupeEquipe != null &&
                            p.GroupeEquipe.ChefEquipeId == empId)
                .ToListAsync();

            var result = projets.Select(p => MapToProjetDetailDto(p));

            return Ok(result);
        }

        [HttpGet("mes-projets-membre")]
        [Authorize]
        public async Task<ActionResult> GetMesProjetsMembre()
        {
            // ✅ Utiliser la nouvelle méthode au lieu du claim direct
            var employeId = await GetAuthenticatedEmployeId();

            if (employeId == null)
                return Unauthorized(new { message = "Employé non identifié. Vérifiez que votre email Authentik correspond à un employé en base." });

            int empId = employeId.Value;

            _logger.LogInformation("📋 GetMesProjetsMembre — EmployeId: {EmpId}", empId);

            var projets = await _context.Projets
                .Include(p => p.Client)
                .Include(p => p.GroupeEquipe)
                    .ThenInclude(g => g.Employes)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.SousTaches)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.Responsable)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.Testeur)
                .Where(p => p.GroupeEquipe != null &&
                            p.GroupeEquipe.Employes.Any(e => e.Id == empId))
                .ToListAsync();

            var result = projets.Select(p => MapToProjetDetailDto(p));

            return Ok(result);
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetProjetById(int id)
        {
            var projet = await _context.Projets
                .Include(p => p.Client)
                .Include(p => p.GroupeEquipe)
                    .ThenInclude(g => g.Employes)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.Responsable)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.Testeur)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.SousTaches)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.TacheCompetences)
                            .ThenInclude(tc => tc.Competence)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound();

            var dto = MapToProjetDetailDto(projet);
            return Ok(dto);
        }

        private string CalculateProjectStatus(Projet projet)
        {
            var allSubTasks = projet.Phases
                .SelectMany(p => p.Taches)
                .SelectMany(t => t.SousTaches)
                .ToList();
            if (!allSubTasks.Any())
            {
                return projet.Statut.ToString();
            }

            int total = allSubTasks.Count;
            int validated = allSubTasks.Count(st => st.Statut == StatutSousTache.Validee);
            int rejected = allSubTasks.Count(st => st.Statut == StatutSousTache.Rejetee);
            int inProgress = allSubTasks.Count(st => st.Statut == StatutSousTache.EnCours);

            if (validated == total)
            {
                return "Termine";
            }
            if (inProgress > 0 || validated > 0 || rejected > 0)
            {
                return "EnCours";
            }
            return projet.Statut.ToString();
        }

        private bool IsProjectDelayed(Projet projet, string calculatedStatus)
        {
            if (calculatedStatus == "Termine")
                return false;

            var today = DateTime.Today;
            var endDate = projet.DateFinPrevue.Date;

            return endDate < today;
        }

        private string GetFinalProjectStatus(Projet projet)
        {
            var calculatedStatus = CalculateProjectStatus(projet);

            if (calculatedStatus == "Termine")
                return "Termine";

            if (IsProjectDelayed(projet, calculatedStatus))
                return "EnRetard";

            return calculatedStatus;
        }

        [HttpGet]
        [Authorize]
        public async Task<ActionResult> GetAllProjets()
        {
            // ✅ Vérifier que l'utilisateur est admin ou CEO via les groupes Authentik
            var groups = User.FindAll("groups").Select(c => c.Value).ToList();
            var isAdmin = groups.Contains("ceo");

            if (!isAdmin)
            {
                _logger.LogWarning("❌ Accès refusé à GetAllProjets. Groupes: {Groups}",
                    string.Join(", ", groups));
                // Au lieu de bloquer, on retourne les projets de l'utilisateur
                var empId = await GetAuthenticatedEmployeId();
                if (empId == null)
                    return Unauthorized(new { message = "Employé non identifié" });

                var userProjets = await _context.Projets
                    .Include(p => p.Client)
                    .Include(p => p.GroupeEquipe)
                        .ThenInclude(g => g.Employes)
                    .Include(p => p.Phases)
                        .ThenInclude(ph => ph.Taches)
                            .ThenInclude(t => t.SousTaches)
                    .Include(p => p.Phases)
                        .ThenInclude(ph => ph.Taches)
                            .ThenInclude(t => t.Responsable)
                    .Include(p => p.Phases)
                        .ThenInclude(ph => ph.Taches)
                            .ThenInclude(t => t.Testeur)
                    .Where(p => p.GroupeEquipe != null &&
                                p.GroupeEquipe.Employes.Any(e => e.Id == empId.Value))
                    .ToListAsync();

                return Ok(userProjets.Select(p => MapToProjetDetailDto(p)));
            }

            var projets = await _context.Projets
                .Include(p => p.Client)
                .Include(p => p.GroupeEquipe)
                    .ThenInclude(g => g.Employes)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.SousTaches)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.Responsable)
                .Include(p => p.Phases)
                    .ThenInclude(ph => ph.Taches)
                        .ThenInclude(t => t.Testeur)
                .ToListAsync();

            var result = projets.Select(p => MapToProjetDetailDto(p));

            return Ok(result);
        }

        private ProjetDetailDto MapToProjetDetailDto(Projet projet)
        {
            var finalStatus = GetFinalProjectStatus(projet);

            return new ProjetDetailDto
            {
                Id = projet.Id,
                Nom = projet.Nom,
                Description = projet.Description,
                Lieu = projet.Lieu,
                DateDebut = projet.DateDebut,
                DateFinPrevue = projet.DateFinPrevue,
                DateFinReelle = projet.DateFinReelle,
                BudgetEstime = projet.BudgetEstime,
                BudgetReel = projet.BudgetReel,
                TypeProjet = projet.TypeProjet,
                Statut = finalStatus,

                Client = projet.Client == null ? null : new ClientDto
                {
                    Id = projet.Client.Id,
                    Nom = projet.Client.RaisonSociale,
                },

                GroupeEquipe = projet.GroupeEquipe == null ? null : new GroupeEquipeDetailDto
                {
                    Id = projet.GroupeEquipe.Id,
                    Nom = projet.GroupeEquipe.Nom,
                    TypeProjetCompatible = projet.GroupeEquipe.TypeProjetCompatible,
                    ChefEquipeId = projet.GroupeEquipe.ChefEquipeId,
                    Employes = projet.GroupeEquipe.Employes.Select(e => new EmployeSimplifieDto
                    {
                        Id = e.Id,
                        NomComplet = e.NomComplet,
                        Role = e.Role.ToString()
                    }).ToList()
                },

                Phases = projet.Phases.Select(ph => new PhaseDetailDto
                {
                    Id = ph.Id,
                    TypePhase = ph.TypePhase.ToString(),
                    Statut = ph.Statut.ToString(),
                    Taches = ph.Taches.Select(t => new TacheDetailDto
                    {
                        Id = t.Id,
                        Titre = t.Titre,
                        Statut = t.Statut.ToString(),
                        DateDebutPrevue = t.DateDebutPrevue,
                        DateFinPrevue = t.DateFinPrevue,
                        Responsable = t.Responsable == null ? null : new EmployeDto
                        {
                            Id = t.Responsable.Id,
                            NomComplet = t.Responsable.NomComplet,
                            Email = t.Responsable.Email,
                            Role = t.Responsable.Role.ToString()
                        },
                        Testeur = t.Testeur == null ? null : new EmployeDto
                        {
                            Id = t.Testeur.Id,
                            NomComplet = t.Testeur.NomComplet,
                            Email = t.Testeur.Email,
                            Role = t.Testeur.Role.ToString()
                        },
                        CompetencesRequises = t.TacheCompetences?.Select(tc => tc.Competence?.Nom).Where(n => n != null).ToList() ?? new List<string>(),
                        SousTaches = t.SousTaches.Select(st => new SousTacheSimplifieDto
                        {
                            Id = st.Id,
                            Titre = st.Titre,
                            Statut = st.Statut,
                            DureeEstimeeHeures = st.DureeEstimeeHeures
                        }).ToList()
                    }).ToList()
                }).ToList()
            };
        }

        [HttpGet("{id}/cdc/exists")]
        [Authorize]
        public async Task<IActionResult> CheckCdcExists(int id)
        {
            var projet = await _context.Projets.FindAsync(id);
            if (projet == null) return NotFound();

            return Ok(new { exists = !string.IsNullOrEmpty(projet.CdcFileUrl) });
        }

        [HttpGet("{id}/cdc/download")]
        [Authorize]
        public async Task<IActionResult> DownloadCdc(int id)
        {
            var projet = await _context.Projets.FindAsync(id);
            if (projet == null) return NotFound();

            if (string.IsNullOrEmpty(projet.CdcFileUrl))
                return NotFound(new { message = "Aucun cahier des charges attaché" });

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", projet.CdcFileUrl.TrimStart('/'));

            if (!System.IO.File.Exists(filePath))
                return NotFound(new { message = "Fichier introuvable" });

            var fileName = Path.GetFileName(filePath);
            var contentType = Path.GetExtension(fileName).ToLower() == ".pdf"
                ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

            return PhysicalFile(filePath, contentType, fileName);
        }

        [HttpDelete("{id}/cdc")]
        [Authorize]
        public async Task<IActionResult> DeleteCdc(int id)
        {
            var projet = await _context.Projets.FindAsync(id);
            if (projet == null) return NotFound();

            if (!string.IsNullOrEmpty(projet.CdcFileUrl))
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", projet.CdcFileUrl.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

                projet.CdcFileUrl = null;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Fichier supprimé" });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> UpdateProjet(int id, [FromBody] CreateProjetDto dto)
        {
            var projet = await _context.Projets
                .Include(p => p.Client)
                .Include(p => p.Phases)
                    .ThenInclude(p => p.Taches)
                        .ThenInclude(t => t.SousTaches)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null)
                return NotFound("Projet introuvable");

            var existingAffectations = await _context.Affectations
                .Include(a => a.SousTache)
                    .ThenInclude(st => st.Tache)
                        .ThenInclude(t => t.Phase)
                .Where(a => a.SousTache.Tache.Phase.ProjetId == id)
                .ToListAsync();

            projet.Nom = dto.Nom;
            projet.Description = dto.Description;
            projet.ClientId = dto.ClientId;
            projet.Lieu = dto.Lieu;
            projet.DateDebut = dto.DateDebut;
            projet.DateFinPrevue = dto.DateFinPrevue;
            projet.BudgetEstime = dto.BudgetEstime;
            projet.BudgetReel = dto.BudgetReel ?? projet.BudgetReel;
            projet.TypeProjet = dto.TypeProjet;

            // Traitement des phases
            foreach (var phaseDto in dto.Phases)
            {
                var existingPhase = projet.Phases.FirstOrDefault(p => p.TypePhase.ToString() == phaseDto.TypePhase);

                if (existingPhase == null)
                {
                    var newPhase = new Phase
                    {
                        TypePhase = Enum.Parse<TypePhase>(phaseDto.TypePhase),
                        PourcentageBudget = phaseDto.PourcentageBudget,
                        Statut = StatutPhase.ADemarrer,
                        Taches = new List<Tache>()
                    };

                    foreach (var tacheDto in phaseDto.Taches)
                    {
                        var newTache = new Tache
                        {
                            Titre = tacheDto.Titre,
                            DateDebutPrevue = tacheDto.DateDebutPrevue,
                            DateFinPrevue = tacheDto.DateFinPrevue,
                            ResponsableId = tacheDto.ResponsableId,
                            TesteurId = tacheDto.TesteurId,
                            SousTaches = new List<SousTache>()
                        };

                        foreach (var stDto in tacheDto.SousTaches)
                        {
                            var newSousTache = new SousTache
                            {
                                Titre = stDto.Titre,
                                DureeEstimeeHeures = stDto.DureeEstimeeHeures,
                                Statut = Enum.TryParse<StatutSousTache>(stDto.Statut.ToString(), true, out var statutParsed)
                                    ? statutParsed
                                    : StatutSousTache.AFaire,
                            };
                            newTache.SousTaches.Add(newSousTache);

                            if (tacheDto.ResponsableId.HasValue)
                            {
                                var affectation = new Affectation
                                {
                                    SousTache = newSousTache,
                                    EmployeId = tacheDto.ResponsableId.Value
                                };
                                _context.Affectations.Add(affectation);
                            }
                        }

                        newPhase.Taches.Add(newTache);

                        if (tacheDto.ResponsableId.HasValue)
                        {
                            await _notificationService.NotifierAssignationResponsable(
                                newTache.Id, tacheDto.ResponsableId.Value,
                                projet.Nom, tacheDto.Titre);
                        }
                    }
                    projet.Phases.Add(newPhase);
                }
                else
                {
                    existingPhase.PourcentageBudget = phaseDto.PourcentageBudget;

                    foreach (var tacheDto in phaseDto.Taches)
                    {
                        var existingTache = existingPhase.Taches.FirstOrDefault(t => t.Titre == tacheDto.Titre);

                        if (existingTache == null)
                        {
                            var newTache = new Tache
                            {
                                Titre = tacheDto.Titre,
                                DateDebutPrevue = tacheDto.DateDebutPrevue,
                                DateFinPrevue = tacheDto.DateFinPrevue,
                                ResponsableId = tacheDto.ResponsableId,
                                TesteurId = tacheDto.TesteurId,
                                SousTaches = new List<SousTache>()
                            };

                            foreach (var stDto in tacheDto.SousTaches)
                            {
                                var newSousTache = new SousTache
                                {
                                    Titre = stDto.Titre,
                                    DureeEstimeeHeures = stDto.DureeEstimeeHeures,
                                    Statut = Enum.TryParse<StatutSousTache>(stDto.Statut.ToString(), true, out var statutParsed)
                                        ? statutParsed
                                        : StatutSousTache.AFaire,
                                };
                                newTache.SousTaches.Add(newSousTache);

                                if (tacheDto.ResponsableId.HasValue)
                                {
                                    var affectation = new Affectation
                                    {
                                        SousTache = newSousTache,
                                        EmployeId = tacheDto.ResponsableId.Value
                                    };
                                    _context.Affectations.Add(affectation);
                                }
                            }
                            existingPhase.Taches.Add(newTache);

                            if (tacheDto.ResponsableId.HasValue)
                            {
                                await _notificationService.NotifierAssignationResponsable(
                                    newTache.Id, tacheDto.ResponsableId.Value,
                                    projet.Nom, tacheDto.Titre);
                            }
                        }
                        else
                        {
                            var oldResponsableId = existingTache.ResponsableId;
                            existingTache.Titre = tacheDto.Titre;
                            existingTache.DateDebutPrevue = tacheDto.DateDebutPrevue;
                            existingTache.DateFinPrevue = tacheDto.DateFinPrevue;
                            existingTache.ResponsableId = tacheDto.ResponsableId;
                            existingTache.TesteurId = tacheDto.TesteurId;

                            foreach (var stDto in tacheDto.SousTaches)
                            {
                                var existingSt = existingTache.SousTaches.FirstOrDefault(st => st.Titre == stDto.Titre);

                                if (existingSt == null)
                                {
                                    var newSousTache = new SousTache
                                    {
                                        Titre = stDto.Titre,
                                        DureeEstimeeHeures = stDto.DureeEstimeeHeures,
                                        Statut = Enum.TryParse<StatutSousTache>(stDto.Statut.ToString(), true, out var statutParsed)
                                            ? statutParsed
                                            : StatutSousTache.AFaire,
                                    };
                                    existingTache.SousTaches.Add(newSousTache);

                                    if (tacheDto.ResponsableId.HasValue)
                                    {
                                        var affectation = new Affectation
                                        {
                                            SousTache = newSousTache,
                                            EmployeId = tacheDto.ResponsableId.Value
                                        };
                                        _context.Affectations.Add(affectation);
                                    }
                                }
                                else
                                {
                                    existingSt.Titre = stDto.Titre;
                                    existingSt.DureeEstimeeHeures = stDto.DureeEstimeeHeures;

                                    if (stDto.Statut != null && stDto.Statut.ToString() != existingSt.Statut.ToString())
                                    {
                                        existingSt.Statut = Enum.TryParse<StatutSousTache>(stDto.Statut.ToString(), true, out var statutParsed)
                                            ? statutParsed
                                            : existingSt.Statut;
                                    }

                                    var existingAffectation = existingAffectations
                                        .FirstOrDefault(a => a.SousTacheId == existingSt.Id);

                                    if (existingAffectation != null && tacheDto.ResponsableId.HasValue &&
                                        existingAffectation.EmployeId != tacheDto.ResponsableId.Value)
                                    {
                                        existingAffectation.EmployeId = tacheDto.ResponsableId.Value;
                                    }
                                    else if (existingAffectation == null && tacheDto.ResponsableId.HasValue)
                                    {
                                        var newAffectation = new Affectation
                                        {
                                            SousTacheId = existingSt.Id,
                                            EmployeId = tacheDto.ResponsableId.Value
                                        };
                                        _context.Affectations.Add(newAffectation);
                                    }
                                }
                            }

                            // Supprimer les sous-tâches qui ne sont plus dans le DTO
                            var stToRemove = existingTache.SousTaches
                                .Where(st => !tacheDto.SousTaches.Any(stdto => stdto.Titre == st.Titre))
                                .ToList();

                            foreach (var st in stToRemove)
                            {
                                var testsForSt = await _context.Tests.Where(t => t.SousTacheId == st.Id).ToListAsync();
                                if (testsForSt.Any())
                                {
                                    _context.Tests.RemoveRange(testsForSt);
                                }

                                var affectationsToRemove = existingAffectations.Where(a => a.SousTacheId == st.Id).ToList();
                                _context.Affectations.RemoveRange(affectationsToRemove);
                                _context.SousTaches.Remove(st);
                            }

                            if (tacheDto.ResponsableId != oldResponsableId && tacheDto.ResponsableId.HasValue)
                            {
                                await _notificationService.NotifierAssignationResponsable(
                                    existingTache.Id, tacheDto.ResponsableId.Value,
                                    projet.Nom, tacheDto.Titre);
                            }
                        }
                    }

                    // Supprimer les tâches qui ne sont plus dans le DTO
                    var tachesToRemove = existingPhase.Taches
                        .Where(t => !phaseDto.Taches.Any(tdto => tdto.Titre == t.Titre))
                        .ToList();

                    foreach (var tache in tachesToRemove)
                    {
                        var stIdsInTache = tache.SousTaches.Select(st => st.Id).ToList();
                        var testsForTache = await _context.Tests.Where(t => stIdsInTache.Contains(t.SousTacheId)).ToListAsync();
                        if (testsForTache.Any())
                        {
                            _context.Tests.RemoveRange(testsForTache);
                        }

                        var affectationsToRemove = existingAffectations
                            .Where(a => tache.SousTaches.Select(st => st.Id).Contains(a.SousTacheId))
                            .ToList();
                        _context.Affectations.RemoveRange(affectationsToRemove);
                        _context.SousTaches.RemoveRange(tache.SousTaches);
                        _context.Taches.Remove(tache);
                    }
                }
            }

            // Supprimer les phases qui ne sont plus dans le DTO
            var phasesToRemove = projet.Phases
                .Where(p => !dto.Phases.Any(pdto => pdto.TypePhase == p.TypePhase.ToString()))
                .ToList();

            foreach (var phase in phasesToRemove)
            {
                foreach (var tache in phase.Taches)
                {
                    var stIdsInPhase = tache.SousTaches.Select(st => st.Id).ToList();
                    var testsForPhase = await _context.Tests.Where(t => stIdsInPhase.Contains(t.SousTacheId)).ToListAsync();
                    if (testsForPhase.Any())
                    {
                        _context.Tests.RemoveRange(testsForPhase);
                    }

                    var affectationsToRemove = existingAffectations
                        .Where(a => tache.SousTaches.Select(st => st.Id).Contains(a.SousTacheId))
                        .ToList();
                    _context.Affectations.RemoveRange(affectationsToRemove);
                    _context.SousTaches.RemoveRange(tache.SousTaches);
                    _context.Taches.Remove(tache);
                }
                _context.Phases.Remove(phase);
            }

            await _context.SaveChangesAsync();
            await UpdateTaskCompetencesFromDto(projet.Id, dto);
            await _notificationService.NotifierProjetsProchesFin();
            await _syncService.PublierVersCRM(projet, SyncAction.Updated);

            return Ok(new { message = "Projet mis à jour avec succès" });
        }

        private async Task UpdateTaskCompetencesFromDto(int projetId, CreateProjetDto dto)
        {
            var allTasks = await _context.Taches
                .Include(t => t.Phase)
                .Include(t => t.TacheCompetences)
                .Where(t => t.Phase.ProjetId == projetId)
                .ToListAsync();

            var taskDict = new Dictionary<string, Tache>();
            foreach (var task in allTasks)
            {
                var key = $"{task.Phase.TypePhase}_{task.Titre}";
                taskDict[key] = task;
            }
            foreach (var phaseDto in dto.Phases)
            {
                foreach (var tacheDto in phaseDto.Taches)
                {
                    var key = $"{phaseDto.TypePhase}_{tacheDto.Titre}";
                    if (!taskDict.TryGetValue(key, out var task))
                        continue;

                    var existingCompetences = _context.TacheCompetences.Where(tc => tc.TacheId == task.Id);
                    _context.TacheCompetences.RemoveRange(existingCompetences);

                    if (tacheDto.CompetencesRequises != null && tacheDto.CompetencesRequises.Any())
                    {
                        foreach (var compNom in tacheDto.CompetencesRequises)
                        {
                            var competence = await _context.Competences.FirstOrDefaultAsync(c => c.Nom == compNom);
                            if (competence != null)
                            {
                                _context.TacheCompetences.Add(new TacheCompetence
                                {
                                    TacheId = task.Id,
                                    CompetenceId = competence.Id,
                                    NiveauRequis = "Intermédiaire"
                                });
                            }
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        [HttpPost("{id}/membres")]
        [Authorize]
        public async Task<IActionResult> AjouterMembres(int id, [FromBody] List<int> employeIds)
        {
            var projet = await _context.Projets
                .Include(p => p.GroupeEquipe)
                    .ThenInclude(g => g.Employes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null) return NotFound("Projet introuvable");
            if (projet.GroupeEquipe == null) return BadRequest("Ce projet n'a pas d'équipe assignée");

            var equipe = projet.GroupeEquipe;

            var employes = await _context.Employes
                .Where(e => employeIds.Contains(e.Id))
                .ToListAsync();

            foreach (var emp in employes)
            {
                if (!equipe.Employes.Any(e => e.Id == emp.Id))
                {
                    equipe.Employes.Add(emp);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Membres ajoutés à l'équipe du projet" });
        }

        [HttpDelete("{id}/membres/{employeId}")]
        [Authorize]
        public async Task<IActionResult> RemoveMembre(int id, int employeId)
        {
            var projet = await _context.Projets
                .Include(p => p.GroupeEquipe)
                    .ThenInclude(g => g.Employes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (projet == null) return NotFound("Projet introuvable");
            if (projet.GroupeEquipe == null) return BadRequest("Ce projet n'a pas d'équipe assignée");

            var equipe = projet.GroupeEquipe;

            var employe = equipe.Employes.FirstOrDefault(e => e.Id == employeId);

            if (employe == null)
                return BadRequest("Cet employé ne fait pas partie de l'équipe de ce projet");

            equipe.Employes.Remove(employe);

            await _context.SaveChangesAsync();

            return Ok(new { message = "Employé retiré de l'équipe" });
        }
    }
}