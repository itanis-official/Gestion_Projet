using GestionProjet.Enums;

namespace GestionProjet.DTOs;

public class PhaseDto
{
    public int Id { get; set; }
    public int ProjetId { get; set; }
    public TypePhase TypePhase { get; set; }
    public decimal PourcentageBudget { get; set; }
    public string Statut { get; set; } = string.Empty;
}

public class PhaseDetailDto : PhaseDto
{
    public List<TacheSimplifieDto> Taches { get; set; } = new();
}