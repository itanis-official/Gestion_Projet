using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models;

public class Client
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Nom { get; set; } = string.Empty;
    
    [StringLength(100)]
    public string Contact { get; set; } = string.Empty;
    
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;
    
    [Phone]
    [StringLength(20)]
    public string Telephone { get; set; } = string.Empty;
    
    public virtual ICollection<Projet> Projets { get; set; } = new List<Projet>();
}