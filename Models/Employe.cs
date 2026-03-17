using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models;

public class Employe
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(150)]
    public string NomComplet { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Role { get; set; } = string.Empty;
    
    public int? GroupeEquipeId { get; set; }
    
    [ForeignKey("GroupeEquipeId")]
    public virtual GroupeEquipe? GroupeEquipe { get; set; }
    
    public virtual ICollection<Affectation> Affectations { get; set; } = new List<Affectation>();
    public virtual ICollection<Test> TestsEffectues { get; set; } = new List<Test>();
    public virtual ICollection<DeclarationTemps> DeclarationsTemps { get; set; } = new List<DeclarationTemps>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}