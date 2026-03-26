using GestionProjet.DTOs;

public class ProjetDetailDto
{
    public int Id { get; set; }
    public string Nom { get; set; }
    public string Description { get; set; }

    public string? Lieu { get; set; }
    public DateTime DateDebut { get; set; }
    public DateTime DateFinPrevue { get; set; }
    public DateTime? DateFinReelle { get; set; }

    public decimal BudgetEstime { get; set; }
    public decimal? BudgetReel { get; set; }

    public string TypeProjet { get; set; }
    public string Statut { get; set; }

    public ClientDto Client { get; set; }

    public GroupeEquipeDetailDto? GroupeEquipe { get; set; }

    public List<PhaseDetailDto> Phases { get; set; }
}
public class ClientDto
{
    public int Id { get; set; }
    public string Nom { get; set; }
}
