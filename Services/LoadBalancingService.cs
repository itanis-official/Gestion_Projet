using Microsoft.EntityFrameworkCore;
using GestionProjet.Data;
using GestionProjet.Enums;

namespace GestionProjet.Services;

public class LoadBalancingService
{
    private readonly ApplicationDbContext _context;
    private const decimal MAX_HOURS_PER_DAY = 8;

    public LoadBalancingService(ApplicationDbContext context)
    {
        _context = context;
    }

    
    public List<EmployeDailyLoadDto> GetDailyLoads()
    {
        return _context.Affectations
            .Include(a => a.SousTache)
                .ThenInclude(st => st.Tache)
            .Include(a => a.Employe)
            .Where(a => a.SousTache.Statut != StatutSousTache.Validee)
            .Select(a => new
            {
                a.EmployeId,
                a.Employe.NomComplet,
                Date = a.SousTache.Tache.DateDebutPrevue.Date,
                a.SousTache.DureeEstimeeHeures
            })
            .GroupBy(x => new { x.EmployeId, x.NomComplet, x.Date })
            .Select(g => new EmployeDailyLoadDto
            {
                EmployeId = g.Key.EmployeId,
                Nom = g.Key.NomComplet,
                Date = g.Key.Date,
                ChargeJour = g.Sum(x => x.DureeEstimeeHeures)
            })
            .ToList();
    }

    public List<EmployeDailyLoadDto> GetOverloaded(decimal maxHours = 8)
    {
        return GetDailyLoads()
            .Where(x => x.ChargeJour > maxHours)
            .ToList();
    }

    public List<GlobalLoadDto> GetAllGlobalLoads()
{
 
    var assignments = _context.Affectations
        .Include(a => a.SousTache)
            .ThenInclude(st => st.Tache)
                .ThenInclude(t => t.Phase)
                    .ThenInclude(p => p.Projet)
        .Include(a => a.Employe)
        .Where(a => a.SousTache.Statut != StatutSousTache.Validee)
        .AsEnumerable()
        .ToList();

    var result = new List<GlobalLoadDto>();

    foreach (var assignment in assignments)
    {
        var task = assignment.SousTache.Tache;
        var start = task.DateDebutPrevue.Date;
        var end = task.DateFinPrevue.Date;
       
        int workDaysCount = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
            {
                workDaysCount++;
            }
        }
        
        if (workDaysCount == 0) workDaysCount = 1; 

      
        var dailyHours = assignment.SousTache.DureeEstimeeHeures / workDaysCount;

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            result.Add(new GlobalLoadDto
            {
                EmployeId = assignment.EmployeId,
                EmployeNom = assignment.Employe.NomComplet,
                Date = d.ToString("yyyy-MM-dd"),
                TotalHeures = dailyHours,
                ProjetId = task.Phase.ProjetId,
                ProjetNom = task.Phase.Projet.Nom,
                TaskId = task.Id.ToString(),
                TaskTitre = task.Titre,
                SousTacheId = assignment.SousTacheId.ToString()
            });
        }
    }

    return result;
}
}


public class EmployeDailyLoadDto
{
    public int EmployeId { get; set; }
    public string Nom { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal ChargeJour { get; set; }
}

public class GlobalLoadDto
{
    public int EmployeId { get; set; }
    public string EmployeNom { get; set; } = string.Empty;
    public string Date { get; set; } 
    public decimal TotalHeures { get; set; } 
    public int ProjetId { get; set; }
    public string ProjetNom { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty; 
    public string TaskTitre { get; set; } = string.Empty;
    public string SousTacheId { get; set; } = string.Empty; 
}