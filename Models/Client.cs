using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models;

public class Client
{
    [Key]
    public int Id { get; set; }
    
    // ⚠️ ASTUCE : On appelle la variable "RaisonSociale" dans le code C#, 
    // mais on garde le nom "Nom" dans la base de données SQL pour ne pas perdre tes données existantes !
    [Required]
    [StringLength(200)]
    [Column("Nom")] 
    public string RaisonSociale { get; set; } = string.Empty;
    
    // Nouvelles propriétés remontées du CRM
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
    
    // ⚠️⭐️⭐️ OBLIGATOIRE : Ne surtout pas supprimer cette ligne ! ⭐️⭐️⚠️
    public virtual ICollection<Projet> Projets { get; set; } = new List<Projet>();
}