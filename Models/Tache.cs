using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GestionProjet.Enums;
namespace GestionProjet.Models;

public class Tache
{
    [Key]
    public int Id { get; set; }
    
    public int PhaseId { get; set; }
    
    [Required]
    [StringLength(200)]
    public string Titre { get; set; } = string.Empty;
    
    [Required]
    public DateTime DateDebutPrevue { get; set; }
    
    [Required]
    public DateTime DateFinPrevue { get; set; }
    
    [Required]
    [StringLength(50)]
    public StatutTache Statut { get; set; } = StatutTache.ADemarrer;
    
    [ForeignKey("PhaseId")]
    public virtual Phase Phase { get; set; } = null!;
    public int? ResponsableId { get; set; }
public int? TesteurId { get; set; }

[ForeignKey("ResponsableId")]
public Employe? Responsable { get; set; }

[ForeignKey("TesteurId")]
public Employe? Testeur { get; set; }
    public virtual ICollection<TacheCompetence> TacheCompetences { get; set; } = new List<TacheCompetence>();
    public virtual ICollection<SousTache> SousTaches { get; set; } = new List<SousTache>();
    public virtual ICollection<Test> Tests { get; set; } = new List<Test>();
}