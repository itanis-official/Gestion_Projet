namespace GestionProjet.DTOs;

public class AffectationSimplifieDto
{
    public int Id { get; set; }
    public int SousTacheId { get; set; }
    public string SousTacheTitre { get; set; } = string.Empty;
    public int EmployeId { get; set; }
    public string EmployeNom { get; set; } = string.Empty;
    public DateTime? DateDebut { get; set; }
    public DateTime? DateFin { get; set; }
}