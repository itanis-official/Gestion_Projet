using Microsoft.AspNetCore.Mvc;
using GestionProjet.Services;

namespace GestionProjet.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LoadBalancingController : ControllerBase
{
    private readonly LoadBalancingService _service;

    public LoadBalancingController(LoadBalancingService service)
    {
        _service = service;
    }


    [HttpGet("overloaded")]
    public IActionResult GetOverloaded()
    {
        return Ok(_service.GetOverloaded());
    }

    
    [HttpGet("all")]
public IActionResult GetAllGlobalLoads()
{
    try
    {
        var data = _service.GetAllGlobalLoads();
        return Ok(data);
    }
    catch (Exception ex)
    {
        
        return StatusCode(500, new { message = "Erreur lors de la récupération des charges globales", error = ex.Message });
    }
}
}