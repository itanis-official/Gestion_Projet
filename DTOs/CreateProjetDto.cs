namespace GestionProjet.DTOs;

public class CreateProjetDto
{
    public string Nom { get; set; } = string.Empty;
    public int ClientId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Lieu { get; set; }

    public DateTime DateDebut { get; set; }
    public DateTime DateFinPrevue { get; set; }

    public decimal BudgetEstime { get; set; }
    public decimal? BudgetReel { get; set; }

    public string TypeProjet { get; set; } = string.Empty;

    public List<CreatePhaseDto> Phases { get; set; } = new();
}