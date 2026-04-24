using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
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

public SousTachesController(ApplicationDbContext context, StatutService statutService,NotificationService service)
{
    _context = context;
    _statutService = statutService;
    _notificationService = service;
}
[HttpGet("sous-tache/{subTaskId}")]
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
    [Authorize]
    public async Task<IActionResult> GetSousTachesATester([FromQuery] int? projetId)
    {
        var employeId = int.Parse(User.FindFirst("EmployeId").Value);

        var query = _context.SousTaches
            .Include(st => st.Tache)
                .ThenInclude(t => t.Phase)
                    .ThenInclude(p => p.Projet)
            .Where(st =>
                st.Tache.TesteurId == employeId &&
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
[Authorize]
public async Task<IActionResult> ValiderSousTache(int id)
{
    var empId = int.Parse(User.FindFirst("EmployeId").Value);

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
[Authorize]
public async Task<IActionResult> RejeterSousTache(int id, [FromBody] RejetSousTacheDto dto)
{
    var empId = int.Parse(User.FindFirst("EmployeId").Value);

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
   [HttpPatch("{id}/statut")]
[Authorize]
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
        // On vérifie aussi que le testeur n'est pas la personne qui a mis à jour (optionnel)
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
[Authorize]
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
[Authorize]
public async Task<ActionResult> GetMesSousTachesResponsable([FromQuery] int? projetId)
{
    var employeId = User.FindFirst("EmployeId")?.Value;

    if (string.IsNullOrEmpty(employeId))
        return Unauthorized("EmployeId introuvable");

    int empId = int.Parse(employeId);

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