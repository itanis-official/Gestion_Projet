namespace GestionProjet.DTOs;

public class EmployeDto
{
    public int Id { get; set; }
    public string NomComplet { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? GroupeEquipeId { get; set; }
}

public class EmployeSimplifieDto
{
    public int Id { get; set; }
    public string NomComplet { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}