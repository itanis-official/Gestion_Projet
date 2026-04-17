using System.ComponentModel.DataAnnotations;


namespace GestionProjet.Models;

public class Competence
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Nom { get; set; } = string.Empty;
    
    [Required]
    [StringLength(50)]
    public string Categorie { get; set; } = string.Empty; 
    
    [Required]
    [StringLength(20)]
    public string Niveau { get; set; } = "Intermédiaire"; 
    
    [StringLength(500)]
    public string? Description { get; set; }
    
    
    public virtual ICollection<EmployeCompetence> EmployeCompetences { get; set; } = new List<EmployeCompetence>();
    
    // Relation avec les tâches
    public virtual ICollection<TacheCompetence> TacheCompetences { get; set; } = new List<TacheCompetence>();
}