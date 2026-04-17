using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestionProjet.Data;
using GestionProjet.Models;

namespace GestionProjet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmployesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public EmployesController(ApplicationDbContext context)
        {
            _context = context;
        }
        
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Employe>>> GetEmployes()
        {
            var employes = await _context.Employes.ToListAsync();
            return Ok(employes);
        }

    }
}