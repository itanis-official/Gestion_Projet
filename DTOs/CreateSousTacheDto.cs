namespace GestionProjet.DTOs;

public class CreateSousTacheDto
{
    public string Titre { get; set; } = string.Empty;
    public decimal DureeEstimeeHeures { get; set; }
    public string Statut { get; set; }
}