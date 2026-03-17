using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models;

public class Notification
{
    [Key]
    public int Id { get; set; }
    
    public int EmployeId { get; set; }
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty; 
    [Required]
    public DateTime DateEnvoi { get; set; }
    
    public bool Lu { get; set; } = false;
    
    [ForeignKey("EmployeId")]
    public virtual Employe Employe { get; set; } = null!;
}