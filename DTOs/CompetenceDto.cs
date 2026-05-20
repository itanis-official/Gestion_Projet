namespace GestionProjet.DTOs;

public class CompetenceDto
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Categorie { get; set; } = string.Empty;
    public string Niveau { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class EmployeCompetenceDto
{
    public int Id { get; set; }
    public int EmployeId { get; set; }
    public string EmployeNom { get; set; } = string.Empty;
    public int CompetenceId { get; set; }
    public string CompetenceNom { get; set; } = string.Empty;
    public string CompetenceCategorie { get; set; } = string.Empty;
    public string Niveau { get; set; } = string.Empty;
    public DateTime DateAcquisition { get; set; }
    public string? Certificat { get; set; }
}

