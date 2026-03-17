using GestionProjet.DTOs;

public class ProjetDto
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public int ClientId { get; set; }
 public string? ClientNom { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Lieu { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime DateFinPrevue { get; set; }
    public DateTime? DateFinReelle { get; set; }
    public decimal BudgetEstime { get; set; }
    public decimal? BudgetReel { get; set; }
    public string TypeProjet { get; set; } = string.Empty;
    public string Statut { get; set; } = string.Empty;
}

public class ProjetDetailDto : ProjetDto
{
    public List<PhaseDto> Phases { get; set; } = new();
    public List<NotificationSimplifieDto> Notifications { get; set; } = new();
    public decimal Avancement
    {
        get
        {
            if (Phases == null || !Phases.Any())
                return 0;
            
            var phasesTerminees = Phases.Count(p => p.Statut == "Terminée");
            return Math.Round((phasesTerminees * 100.0m) / 5, 2); 
        }
    }
}

public class NotificationSimplifieDto
{
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Lu { get; set; }
    public DateTime DateEnvoi { get; set; }
}