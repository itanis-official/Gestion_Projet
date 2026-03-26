using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.Enums;
using GestionProjet.DTOs;
namespace GestionProjet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProjetsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProjetsController(ApplicationDbContext context)
        {
            _context = context;
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
        .Where(p => p.GroupeEquipe != null &&
                    p.GroupeEquipe.ChefEquipeId == empId)
        .ToListAsync();

   var result = projets.Select(p => new
{
    p.Id,
    p.Nom,
    p.Description,

    p.Lieu,
    p.DateDebut,
    p.DateFinPrevue,
    p.DateFinReelle,
    p.BudgetEstime,
    p.BudgetReel,
    p.TypeProjet,
    Statut = p.Statut.ToString(),

    Client = new
    {
        p.Client.Id,
        p.Client.Nom
    },

    GroupeEquipe = p.GroupeEquipe == null ? null : new
    {
        p.GroupeEquipe.Id,
        p.GroupeEquipe.Nom,

        Employes = p.GroupeEquipe.Employes.Select(e => new
        {
            e.Id,
            e.NomComplet
        })
    },

    Phases = p.Phases.Select(ph => new
    {
        ph.Id,
        TypePhase = ph.TypePhase.ToString(),
        ph.Statut
    })
});
    return Ok(result);
}
[HttpPut("{id}")]
[Authorize]
public async Task<ActionResult> UpdateProjet(int id, [FromBody] CreateProjetDto dto)
{
    var projet = await _context.Projets
        .Include(p => p.Phases)
            .ThenInclude(p => p.Taches)
                .ThenInclude(t => t.SousTaches)
        .FirstOrDefaultAsync(p => p.Id == id);

    if (projet == null)
        return NotFound("Projet introuvable");

    projet.Nom = dto.Nom;
    projet.Description = dto.Description;
    projet.ClientId = dto.ClientId;
    projet.Lieu = dto.Lieu;
    projet.DateDebut = dto.DateDebut;
    projet.DateFinPrevue = dto.DateFinPrevue;
    projet.BudgetEstime = dto.BudgetEstime;
    projet.BudgetReel = dto.BudgetReel;
    projet.TypeProjet = dto.TypeProjet;

    // 🔥 suppression propre
    foreach (var phase in projet.Phases)
    {
        foreach (var tache in phase.Taches)
        {
            _context.SousTaches.RemoveRange(tache.SousTaches);
        }
        _context.Taches.RemoveRange(phase.Taches);
    }
    _context.Phases.RemoveRange(projet.Phases);

    // 🔥 reconstruction
    projet.Phases = dto.Phases.Select(p => new Phase
    {
        TypePhase = Enum.TryParse<TypePhase>(p.TypePhase, true, out var parsed)
            ? parsed
            : TypePhase.Analyse,

        PourcentageBudget = p.PourcentageBudget,
        Statut = "À démarrer",

        Taches = p.Taches.Select(t => new Tache
        {
            Titre = t.Titre,
            DateDebutPrevue = t.DateDebutPrevue,
            DateFinPrevue = t.DateFinPrevue,
            ResponsableId = t.ResponsableId,
            TesteurId = t.TesteurId,

            SousTaches = t.SousTaches.Select(st => new SousTache
            {
                Titre = st.Titre,
                DureeEstimeeHeures = st.DureeEstimeeHeures
            }).ToList()
        }).ToList()
    }).ToList();

    await _context.SaveChangesAsync();

    return Ok(new { message = "Projet mis à jour" });
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
        .FirstOrDefaultAsync(p => p.Id == id);

    if (projet == null)
        return NotFound();

  var dto = new ProjetDetailDto
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
    Statut = projet.Statut.ToString(),

    Client = new ClientDto
    {
        Id = projet.Client.Id,
        Nom = projet.Client.Nom
    },

    GroupeEquipe = projet.GroupeEquipe == null ? null : new GroupeEquipeDetailDto
    {
        Id = projet.GroupeEquipe.Id,
        Nom = projet.GroupeEquipe.Nom,
        TypeProjetCompatible = projet.GroupeEquipe.TypeProjetCompatible,

       Employes = projet.GroupeEquipe.Employes.Select(e => new EmployeSimplifieDto
{
    Id = e.Id,
    NomComplet = e.NomComplet,
    Role = e.Role.ToString(),
   
}).ToList()
    },

    Phases = projet.Phases.Select(ph => new PhaseDetailDto
    {
        Id = ph.Id,
        TypePhase = ph.TypePhase.ToString(),
        Statut = ph.Statut,
Taches = ph.Taches.Select(t => new TacheDetailDto
{
    Id = t.Id,
    Titre = t.Titre,
    Statut = t.Statut.ToString(),

    // ✅ AJOUT ICI
    DateDebutPrevue = t.DateDebutPrevue,
    DateFinPrevue = t.DateFinPrevue,

    Responsable = new EmployeDto
    {
        Id = t.Responsable.Id,
        NomComplet = t.Responsable.NomComplet,
        Email = t.Responsable.Email,
        Role = t.Responsable.Role.ToString()
    },

    Testeur = new EmployeDto
    {
        Id = t.Testeur.Id,
        NomComplet = t.Testeur.NomComplet,
        Email = t.Testeur.Email,
        Role = t.Testeur.Role.ToString()
    },

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

    return Ok(dto);
}
[HttpGet("mes-sous-taches")]
[Authorize]
public async Task<ActionResult> GetMesSousTaches([FromQuery] int? projetId)
{
    var employeId = User.FindFirst("EmployeId")?.Value;

    if (string.IsNullOrEmpty(employeId))
        return Unauthorized("EmployeId introuvable");

    int empId = int.Parse(employeId);

    var query = _context.SousTaches
        .Include(st => st.Tache)
            .ThenInclude(t => t.Phase)
                .ThenInclude(p => p.Projet)
        .Where(st => st.Tache.ResponsableId == empId 
                  || st.Tache.TesteurId == empId)
        .AsQueryable();

    // ✅ filtre par projet
    if (projetId.HasValue)
    {
        query = query.Where(st => st.Tache.Phase.Projet.Id == projetId.Value);
    }

    var sousTaches = await query.Select(st => new
    {
        st.Id,
        st.Titre,
        st.DureeEstimeeHeures,

        TacheId = st.Tache.Id,
        TacheTitre = st.Tache.Titre,

        ProjetId = st.Tache.Phase.Projet.Id,
        ProjetNom = st.Tache.Phase.Projet.Nom
    }).ToListAsync();

    return Ok(sousTaches);
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
        emp.GroupeEquipeId = id; // ✅ affectation
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

    employe.GroupeEquipeId = null; // ✅ retirer de l'équipe

    await _context.SaveChangesAsync();

    return Ok(new { message = "Employé retiré de l'équipe" });
}

    }
    
}