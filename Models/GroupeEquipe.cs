using System.ComponentModel.DataAnnotations;

namespace GestionProjet.Models;

public class GroupeEquipe
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Nom { get; set; } = string.Empty;
    
    [StringLength(50)]
    public string? TypeProjetCompatible { get; set; }
    
    public virtual ICollection<Employe> Employes { get; set; } = new List<Employe>();
}