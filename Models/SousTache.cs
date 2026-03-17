using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GestionProjet.Enums;

namespace GestionProjet.Models;

public class SousTache
{
    [Key]
    public int Id { get; set; }
    
    public int TacheId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Titre { get; set; } = string.Empty;
    
    [Column(TypeName = "decimal(10,2)")]
    public decimal DureeEstimeeHeures { get; set; }
    
    public StatutSousTache Statut { get; set; } = StatutSousTache.AFaire;
    
    [ForeignKey("TacheId")]
    public virtual Tache Tache { get; set; } = null!;
    
    public virtual ICollection<Affectation> Affectations { get; set; } = new List<Affectation>();
    public virtual ICollection<DeclarationTemps> DeclarationsTemps { get; set; } = new List<DeclarationTemps>();
}