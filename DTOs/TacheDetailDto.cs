namespace GestionProjet.DTOs;

public class TacheDetailDto
{
    public int Id { get; set; }
    public string Titre { get; set; }
    public string Statut { get; set; }
    public DateTime? DateDebutPrevue { get; set; }
    public DateTime? DateFinPrevue { get; set; }
    public required EmployeDto Responsable { get; set; }
    public EmployeDto Testeur { get; set; }

    public List<SousTacheSimplifieDto> SousTaches { get; set; } = new();
}

