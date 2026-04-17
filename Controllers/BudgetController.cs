using Microsoft.AspNetCore.Mvc;
using GestionProjet.Services;

namespace GestionProjet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BudgetController : ControllerBase
{
    private readonly ProjetService _projetService;

    public BudgetController(ProjetService projetService)
    {
        _projetService = projetService;
    }

    [HttpGet("{projetId}")]
    public async Task<IActionResult> GetBudget(int projetId)
    {
        var budget = await _projetService.CalculerBudget(projetId);
        return Ok(new { budgetReel = budget });
    }

    [HttpPost("{projetId}/recalcul")]
    public async Task<IActionResult> Recalcul(int projetId)
    {
        await _projetService.MettreAJourProjet(projetId);
        return Ok(new { message = "Budget mis à jour", projetId });
    }

    [HttpPost("recalculer-tous")]
    public async Task<IActionResult> RecalculerTous()
    {
        await _projetService.RecalculerTousLesProjets();
        return Ok(new { message = "Tous les budgets ont été recalculés" });
    }
}