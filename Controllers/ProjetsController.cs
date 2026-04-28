using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
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
        public ProjetsController(
            ApplicationDbContext context,
            NotificationService service,
            ProjetSyncService syncService)                // ← AJOUT
        {
            _context = context;
            _notificationService = service;
            _syncService = syncService;
        }

        [HttpGet("mes-projets-chef")]
        [Authorize]
        public async Task<ActionResult> GetMesProjetsChef()
        {
            var employeId = User.FindFirst("EmployeId")?.Value;

            if (string.IsNullOrEmpty(employeId))
                return Unauthorized();

            int empId = int.Parse(employeId);

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
            var employeId = User.FindFirst("EmployeId")?.Value;

            if (string.IsNullOrEmpty(employeId))
                return Unauthorized();

            int empId = int.Parse(employeId);

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
        // ⭐ AJOUT CRITIQUE : Charger les compétences des tâches
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
                // ⭐ AJOUTER CETTE LIGNE POUR LES COMPÉTENCES REQUISES
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

    // ═══ GESTION DES PHASES ET TÂCHES (SANS COMPÉTENCES) ═══
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

                // ⚠️ NE RIEN FAIRE AVEC LES COMPÉTENCES ICI
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
                                EmployeId = tacheDto.ResponsableId.Value,
                            };
                            _context.Affectations.Add(affectation);
                        }
                    }

                    // ⚠️ NE RIEN FAIRE AVEC LES COMPÉTENCES ICI
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
                                    EmployeId = tacheDto.ResponsableId.Value,
                                };
                                _context.Affectations.Add(newAffectation);
                            }
                        }
                    }

                    // ⚠️ NE RIEN FAIRE AVEC LES COMPÉTENCES ICI - ON LES GÈRE APRÈS SAVE

                    var stToRemove = existingTache.SousTaches
                        .Where(st => !tacheDto.SousTaches.Any(stdto => stdto.Titre == st.Titre))
                        .ToList();

                    foreach (var st in stToRemove)
                    {
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

            var tachesToRemove = existingPhase.Taches
                .Where(t => !phaseDto.Taches.Any(tdto => tdto.Titre == t.Titre))
                .ToList();

            foreach (var tache in tachesToRemove)
            {
                var affectationsToRemove = existingAffectations
                    .Where(a => tache.SousTaches.Select(st => st.Id).Contains(a.SousTacheId))
                    .ToList();
                _context.Affectations.RemoveRange(affectationsToRemove);
                _context.SousTaches.RemoveRange(tache.SousTaches);
                _context.Taches.Remove(tache);
            }
        }
    }

    var phasesToRemove = projet.Phases
        .Where(p => !dto.Phases.Any(pdto => pdto.TypePhase == p.TypePhase.ToString()))
        .ToList();

    foreach (var phase in phasesToRemove)
    {
        foreach (var tache in phase.Taches)
        {
            var affectationsToRemove = existingAffectations
                .Where(a => tache.SousTaches.Select(st => st.Id).Contains(a.SousTacheId))
                .ToList();
            _context.Affectations.RemoveRange(affectationsToRemove);
            _context.SousTaches.RemoveRange(tache.SousTaches);
            _context.Taches.Remove(tache);
        }
        _context.Phases.Remove(phase);
    }

    // ═══ PREMIÈRE SAUVEGARDE - ICI les IDs réels sont générés ═══
    await _context.SaveChangesAsync();

    // ═══ MAINTENANT, ON GÈRE LES COMPÉTENCES APRÈS LA SAUVEGARDE ═══
    await UpdateTaskCompetencesFromDto(projet.Id, dto);

    await _notificationService.NotifierProjetsProchesFin();
    await _syncService.PublierVersCRM(projet, SyncAction.Updated);

    return Ok(new { message = "Projet mis à jour avec succès" });
}

// Méthode séparée pour gérer les compétences APRÈS la sauvegarde
private async Task UpdateTaskCompetencesFromDto(int projetId, CreateProjetDto dto)
{
    // Récupérer toutes les tâches du projet avec leurs compétences actuelles
    var allTasks = await _context.Taches
        .Include(t => t.Phase)
        .Include(t => t.TacheCompetences)
        .Where(t => t.Phase.ProjetId == projetId)
        .ToListAsync();

    // Créer un dictionnaire pour retrouver les tâches par (typePhase, titre)
    var taskDict = new Dictionary<string, Tache>();
    foreach (var task in allTasks)
    {
        var key = $"{task.Phase.TypePhase}_{task.Titre}";
        taskDict[key] = task;
    }

    // Parcourir chaque phase du DTO et mettre à jour les compétences
    foreach (var phaseDto in dto.Phases)
    {
        foreach (var tacheDto in phaseDto.Taches)
        {
            var key = $"{phaseDto.TypePhase}_{tacheDto.Titre}";
            if (!taskDict.TryGetValue(key, out var task))
                continue;

            // Supprimer les anciennes compétences
            var existingCompetences = _context.TacheCompetences.Where(tc => tc.TacheId == task.Id);
            _context.TacheCompetences.RemoveRange(existingCompetences);

            // Ajouter les nouvelles compétences
            if (tacheDto.CompetencesRequises != null && tacheDto.CompetencesRequises.Any())
            {
                foreach (var compNom in tacheDto.CompetencesRequises)
                {
                    var competence = await _context.Competences.FirstOrDefaultAsync(c => c.Nom == compNom);
                    if (competence != null)
                    {
                        _context.TacheCompetences.Add(new TacheCompetence
                        {
                            TacheId = task.Id,  // Maintenant c'est un vrai ID !
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
        public async Task<IActionResult> AjouterMembres(int id, [FromBody] List<int> employeIds)
        {
            var equipe = await _context.GroupesEquipe
                .Include(g => g.Employes)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (equipe == null)
                return NotFound("Equipe introuvable");

            var employes = await _context.Employes
                .Where(e => employeIds.Contains(e.Id))
                .ToListAsync();

            foreach (var emp in employes)
            {
                emp.GroupeEquipeId = id;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Membres ajoutés à l'équipe" });
        }

        [HttpDelete("{id}/membres/{employeId}")]
        public async Task<IActionResult> RemoveMembre(int id, int employeId)
        {
            var employe = await _context.Employes.FindAsync(employeId);

            if (employe == null)
                return NotFound("Employé introuvable");

            if (employe.GroupeEquipeId != id)
                return BadRequest("Cet employé n'appartient pas à cette équipe");

            employe.GroupeEquipeId = null;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Employé retiré de l'équipe" });
        }
    }
}