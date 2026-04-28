using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using GestionProjet.Data;
using GestionProjet.Models;  

namespace GestionProjet.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TypesProjetController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        
        public TypesProjetController(ApplicationDbContext context)
        {
            _context = context;
        }
        
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TypeProjet>>> GetTypesProjet()
        {
            var types = await _context.TypesProjet
                .Where(t => t.IsActive)
                .OrderBy(t => t.Ordre)
                .ToListAsync();
            
            return Ok(types);
        }
    }
}