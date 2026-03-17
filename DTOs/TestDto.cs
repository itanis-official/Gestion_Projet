using GestionProjet.Enums;

namespace GestionProjet.DTOs;

public class TestDto
{
    public int Id { get; set; }
    public int TacheId { get; set; }
    public int EmployeId { get; set; }
    public DateTime DateTest { get; set; }
    public ResultatTest Resultat { get; set; }
    public string? Commentaire { get; set; }
}

public class TestDetailDto : TestDto
{
    public string TacheTitre { get; set; } = string.Empty;
    public string EmployeNom { get; set; } = string.Empty;
}

public class TestSimplifieDto
{
    public int Id { get; set; }
    public DateTime DateTest { get; set; }
    public ResultatTest Resultat { get; set; }
    public string TacheTitre { get; set; } = string.Empty;
}