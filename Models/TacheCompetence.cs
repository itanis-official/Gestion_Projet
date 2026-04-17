using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GestionProjet.Models;

public class TacheCompetence
{
    [Key]
    public int Id { get; set; }
    
    public int TacheId { get; set; }
    public int CompetenceId { get; set; }
    
    [StringLength(20)]
    public string NiveauRequis { get; set; } = "Intermédiaire";
    
    [ForeignKey("TacheId")]
    public virtual Tache Tache { get; set; } = null!;
    
    [ForeignKey("CompetenceId")]
    public virtual Competence Competence { get; set; } = null!;
}