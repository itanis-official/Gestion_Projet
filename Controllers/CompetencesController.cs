using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestionProjet.Data;
using GestionProjet.Models;
using GestionProjet.DTOs;

namespace GestionProjet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompetencesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CompetencesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var competences = await _context.Competences
            .Select(c => new CompetenceDto
            {
                Id = c.Id,
                Nom = c.Nom,
                Categorie = c.Categorie,
                Niveau = c.Niveau,
                Description = c.Description
            })
            .ToListAsync();
        
        return Ok(competences);
    }

    [HttpGet("employe/{employeId}")]
    public async Task<IActionResult> GetByEmploye(int employeId)
    {
        var competences = await _context.EmployeCompetences
            .Include(ec => ec.Competence)
            .Where(ec => ec.EmployeId == employeId)
            .Select(ec => new EmployeCompetenceDto
            {
                Id = ec.Id,
                EmployeId = ec.EmployeId,
                EmployeNom = ec.Employe.NomComplet,
                CompetenceId = ec.CompetenceId,
                CompetenceNom = ec.Competence.Nom,
                CompetenceCategorie = ec.Competence.Categorie,
                Niveau = ec.Niveau,
                DateAcquisition = ec.DateAcquisition,
                Certificat = ec.Certificat
            })
            .ToListAsync();
        
        return Ok(competences);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CompetenceDto dto)
    {
        var competence = new Competence
        {
            Nom = dto.Nom,
            Categorie = dto.Categorie,
            Niveau = dto.Niveau,
            Description = dto.Description
        };
        
        _context.Competences.Add(competence);
        await _context.SaveChangesAsync();
        
        return Ok(new CompetenceDto
        {
            Id = competence.Id,
            Nom = competence.Nom,
            Categorie = competence.Categorie,
            Niveau = competence.Niveau,
            Description = competence.Description
        });
    }

    [HttpPost("assigner")]
    public async Task<IActionResult> AssignerCompetence([FromBody] AssignCompetenceDto dto)
    {
        var existant = await _context.EmployeCompetences
            .FirstOrDefaultAsync(ec => ec.EmployeId == dto.EmployeId && ec.CompetenceId == dto.CompetenceId);
        
        if (existant != null)
        {
            existant.Niveau = dto.Niveau;
            existant.Certificat = dto.Certificat;
            existant.DateAcquisition = DateTime.UtcNow;
        }
        else
        {
            var employeCompetence = new EmployeCompetence
            {
                EmployeId = dto.EmployeId,
                CompetenceId = dto.CompetenceId,
                Niveau = dto.Niveau,
                Certificat = dto.Certificat,
                DateAcquisition = DateTime.UtcNow
            };
            _context.EmployeCompetences.Add(employeCompetence);
        }
        
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var competence = await _context.Competences.FindAsync(id);
        if (competence == null) return NotFound();
        
        _context.Competences.Remove(competence);
        await _context.SaveChangesAsync();
        
        return Ok();
    }

    [HttpDelete("employe/{employeId}/competence/{competenceId}")]
    public async Task<IActionResult> RetirerCompetenceEmploye(int employeId, int competenceId)
    {
        var employeCompetence = await _context.EmployeCompetences
            .FirstOrDefaultAsync(ec => ec.EmployeId == employeId && ec.CompetenceId == competenceId);
        
        if (employeCompetence == null) return NotFound();
        
        _context.EmployeCompetences.Remove(employeCompetence);
        await _context.SaveChangesAsync();
        
        return Ok();
    }
}

public class AssignCompetenceDto
{
    public int EmployeId { get; set; }
    public int CompetenceId { get; set; }
    public string Niveau { get; set; } = "Intermédiaire";
    public string? Certificat { get; set; }
}