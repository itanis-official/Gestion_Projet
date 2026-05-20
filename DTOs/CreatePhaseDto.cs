namespace GestionProjet.DTOs;

public class CreatePhaseDto
{
    public string TypePhase { get; set; } = string.Empty;
    public decimal PourcentageBudget { get; set; }
    public List<CreateTacheDto> Taches { get; set; } = new();
}