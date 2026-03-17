using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models;

public class DeclarationTemps
{
    [Key]
    public int Id { get; set; }
    
    public int SousTacheId { get; set; }
    
    public int EmployeId { get; set; }
    
    [Required]
    public DateTime Date { get; set; }
    
    [Column(TypeName = "decimal(10,2)")]
    public decimal DureeHeures { get; set; }
    
    [Required]
    [StringLength(20)]
    public string Type { get; set; } = "Travail"; 
    
    [ForeignKey("SousTacheId")]
    public virtual SousTache SousTache { get; set; } = null!;
    
    [ForeignKey("EmployeId")]
    public virtual Employe Employe { get; set; } = null!;
}