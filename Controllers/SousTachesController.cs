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
public class SousTachesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SousTachesController(ApplicationDbContext context)
    {
        _context = context;
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

        // 🎯 filtre projet optionnel
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
}}