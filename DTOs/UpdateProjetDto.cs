using GestionProjet.DTOs;

public class UpdateProjetDto
{
    public int Id { get; set; }
    public string Nom { get; set; }
    public string Description { get; set; }
    public List<PhaseDto> Phases { get; set; }
}

public class PhaseDto
{
    public int Id { get; set; }
    public string TypePhase { get; set; } = string.Empty;
    public string Statut { get; set; } = string.Empty;
    public List<TacheDetailDto> Taches { get; set; } = new();
}

public class TacheDto
{
    public int? Id { get; set; }
    public string Titre { get; set; }
    public int? ResponsableId { get; set; }
    public int? TesteurId { get; set; }
    public List<SousTacheDto> SousTaches { get; set; }
}

public class SousTacheDto
{
    public int? Id { get; set; }
    public string Titre { get; set; }
}