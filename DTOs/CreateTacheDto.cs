namespace GestionProjet.DTOs;

public class CreateTacheDto
{
    public string Titre { get; set; } = string.Empty;
    public DateTime DateDebutPrevue { get; set; }
    public DateTime DateFinPrevue { get; set; }

    public int? ResponsableId { get; set; }
    public int? TesteurId { get; set; }

    public List<CreateSousTacheDto> SousTaches { get; set; } = new();
}