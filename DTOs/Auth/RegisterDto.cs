using System.ComponentModel.DataAnnotations;

namespace GestionProjet.DTOs.Auth;

public class RegisterDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    [Compare("Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
    
    [Required]
    public string NomComplet { get; set; } = string.Empty;
    
    public string? Poste { get; set; }
    
    public string? Departement { get; set; }
      public int? EmployeId { get; set; }
}