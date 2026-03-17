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

public class DeclarationTempsDetailDto : DeclarationTempsDto
{
    public string SousTacheTitre { get; set; } = string.Empty;
    public string EmployeNom { get; set; } = string.Empty;
}

public class DeclarationTempsSimplifieDto
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public decimal DureeHeures { get; set; }
    public string Type { get; set; } = string.Empty;
    public string SousTacheTitre { get; set; } = string.Empty;
}