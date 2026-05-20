using GestionProjet.Enums;

namespace GestionProjet.DTOs;


public class SousTacheSimplifieDto
{
    public int Id { get; set; }
    public string Titre { get; set; } = string.Empty;
    public StatutSousTache Statut { get; set; }
    public decimal DureeEstimeeHeures { get; set; }
}