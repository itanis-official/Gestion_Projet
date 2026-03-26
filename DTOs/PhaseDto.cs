using GestionProjet.Enums;

namespace GestionProjet.DTOs;

public class PhaseDetailDto
{
    public int Id { get; set; }
    public string TypePhase { get; set; } = string.Empty;
    public string Statut { get; set; } = string.Empty;

    public List<TacheDetailDto> Taches { get; set; } = new();
}

