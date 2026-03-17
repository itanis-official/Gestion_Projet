namespace GestionProjet.DTOs;

public class TacheDto
{
    public int Id { get; set; }
    public int PhaseId { get; set; }
    public string Titre { get; set; } = string.Empty;
    public DateTime DateDebutPrevue { get; set; }
    public DateTime DateFinPrevue { get; set; }
    public string Statut { get; set; } = string.Empty;
}

public class TacheDetailDto : TacheDto
{
    public List<SousTacheSimplifieDto> SousTaches { get; set; } = new();
    public List<TestSimplifieDto> Tests { get; set; } = new();
}

public class TacheSimplifieDto
{
    public int Id { get; set; }
    public string Titre { get; set; } = string.Empty;
    public DateTime DateFinPrevue { get; set; }
    public string Statut { get; set; } = string.Empty;
}