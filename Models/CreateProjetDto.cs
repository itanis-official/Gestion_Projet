using System.ComponentModel.DataAnnotations;

namespace GestionProjet.DTOs;

public class CreateProjetDto
{
    [Required]
    public string Nom { get; set; } = string.Empty;
    
    [Required]
    public int ClientId { get; set; }
    
    public string Description { get; set; } = string.Empty;
    
    public string? Lieu { get; set; }
    
    [Required]
    public DateTime DateDebut { get; set; }
    
    [Required]
    public DateTime DateFinPrevue { get; set; }
    
    [Range(0, double.MaxValue)]
    public decimal BudgetEstime { get; set; }
    
    public string TypeProjet { get; set; } = string.Empty;
    
    public int Statut { get; set; }
}