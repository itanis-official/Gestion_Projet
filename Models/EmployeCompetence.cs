using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models;

public class EmployeCompetence
{
    [Key]
    public int Id { get; set; }
    
    public int EmployeId { get; set; }
    public int CompetenceId { get; set; }
    
    [StringLength(20)]
    public string Niveau { get; set; } = "Intermédiaire"; // Niveau de maîtrise de l'employé
    
    public DateTime DateAcquisition { get; set; } = DateTime.UtcNow;
    
    [StringLength(500)]
    public string? Certificat { get; set; }
    
    [ForeignKey("EmployeId")]
    public virtual Employe Employe { get; set; } = null!;
    
    [ForeignKey("CompetenceId")]
    public virtual Competence Competence { get; set; } = null!;
}