using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models;

public class Client
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }
    
    [Required]
    [StringLength(200)]
    [Column("Nom")] 
    public string RaisonSociale { get; set; } = string.Empty;
    
    public string? Secteur { get; set; }
    
    [EmailAddress]
    [StringLength(100)]
    public string? EmailPrincipal { get; set; } = string.Empty;
    
    [Phone]
    [StringLength(20)]
    public string? TelephonePrincipal { get; set; } = string.Empty;
    
    public string? Ville { get; set; }
    public string? Pays { get; set; }
    public string? Statut { get; set; }
    
    public DateTime SyncedAt { get; set; }
    
    public virtual ICollection<Projet> Projets { get; set; } = new List<Projet>();
}