using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GestionProjet.Enums;

namespace GestionProjet.Models;

public class Test
{
    [Key]
    public int Id { get; set; }
    
    public int TacheId { get; set; }
    
    public int EmployeId { get; set; }
    
    [Required]
    public DateTime DateTest { get; set; }
    
    public ResultatTest Resultat { get; set; }
    
    public string? Commentaire { get; set; }
    
    [ForeignKey("TacheId")]
    public virtual Tache Tache { get; set; } = null!;
    
    [ForeignKey("EmployeId")]
    public virtual Employe Employe { get; set; } = null!;
}