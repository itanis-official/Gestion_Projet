namespace GestionProjet.DTOs;

public class DeclarationTempsDto
{
    public int Id { get; set; }
    public int SousTacheId { get; set; }
    public int EmployeId { get; set; }
    public DateTime Date { get; set; }
    public decimal DureeHeures { get; set; }
    public string Type { get; set; } = string.Empty;
}

