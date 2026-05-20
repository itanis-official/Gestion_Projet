using Microsoft.AspNetCore.Identity;

namespace GestionProjet.Models;

public class Utilisateur : IdentityUser
{
    public string NomComplet { get; set; } = string.Empty;
    public string? Poste { get; set; }
    public string? Departement { get; set; }
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public bool EstActif { get; set; } = true;
    
    public int? EmployeId { get; set; }
    public virtual Employe? Employe { get; set; }
}