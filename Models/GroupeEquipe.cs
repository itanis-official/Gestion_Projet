using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

    public int? ChefEquipeId { get; set; }

    [ForeignKey("ChefEquipeId")]
    public virtual Employe? ChefEquipe { get; set; }
    
    public virtual ICollection<Employe> Employes { get; set; } = new List<Employe>();
}