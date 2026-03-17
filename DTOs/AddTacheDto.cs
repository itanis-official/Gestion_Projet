using System.ComponentModel.DataAnnotations;

namespace GestionProjet.DTOs;

public class AddTacheDto
{
    [Required]
    public int PhaseId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Titre { get; set; } = string.Empty;
    
    [Required]
    public DateTime DateDebutPrevue { get; set; }
    
    [Required]
    public DateTime DateFinPrevue { get; set; }
    
    public string Statut { get; set; } = "À faire";
}