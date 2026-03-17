using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models;

public class Affectation
{
    [Key]
    public int Id { get; set; }
    
    public int SousTacheId { get; set; }
    
    public int EmployeId { get; set; }
    
    [ForeignKey("SousTacheId")]
    public virtual SousTache SousTache { get; set; } = null!;
    
    [ForeignKey("EmployeId")]
    public virtual Employe Employe { get; set; } = null!;
}