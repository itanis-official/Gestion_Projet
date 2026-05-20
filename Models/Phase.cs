using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GestionProjet.Enums;


namespace GestionProjet.Models;

public class Phase
{
    [Key]
    public int Id { get; set; }
    
    public int ProjetId { get; set; }
    
    [Required]
    public TypePhase TypePhase { get; set; }
    
    [Column(TypeName = "decimal(5,2)")]
    public decimal PourcentageBudget { get; set; }
    
    [Required]
    public StatutPhase Statut { get; set; } = StatutPhase.ADemarrer;
   
    [ForeignKey("ProjetId")]
    public virtual Projet Projet { get; set; } = null!;
    
    public virtual ICollection<Tache> Taches { get; set; } = new List<Tache>();
}