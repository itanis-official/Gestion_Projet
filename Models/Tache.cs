using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models;

public class Tache
{
    [Key]
    public int Id { get; set; }
    
    public int PhaseId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Titre { get; set; } = string.Empty;
    
    [Required]
    public DateTime DateDebutPrevue { get; set; }
    
    [Required]
    public DateTime DateFinPrevue { get; set; }
    
    [Required]
    [StringLength(50)]
    public string Statut { get; set; } = "À faire";
    
    [ForeignKey("PhaseId")]
    public virtual Phase Phase { get; set; } = null!;
    
    public virtual ICollection<SousTache> SousTaches { get; set; } = new List<SousTache>();
    public virtual ICollection<Test> Tests { get; set; } = new List<Test>();
}