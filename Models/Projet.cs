using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GestionProjet.Enums;

namespace GestionProjet.Models;

public class Projet
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Nom { get; set; } = string.Empty;
    
    public int ClientId { get; set; }
    
    public string Description { get; set; } = string.Empty;
    
    [StringLength(255)]
    public string? Lieu { get; set; }
    
    [Required]
    public DateTime DateDebut { get; set; }
    
    [Required]
    public DateTime DateFinPrevue { get; set; }
    
    public DateTime? DateFinReelle { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal BudgetEstime { get; set; }
    
    [Column(TypeName = "decimal(18,2)")]
    public decimal? BudgetReel { get; set; }
    
    [StringLength(50)]
    public string TypeProjet { get; set; } = string.Empty;
    
    public StatutProjet Statut { get; set; } = StatutProjet.Planifie;
    
    [ForeignKey("ClientId")]
    public virtual Client Client { get; set; } = null!;
public int? GroupeEquipeId { get; set; }   
[ForeignKey("GroupeEquipeId")]
public virtual GroupeEquipe? GroupeEquipe { get; set; }
    public virtual ICollection<Phase> Phases { get; set; } = new List<Phase>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}