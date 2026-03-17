namespace GestionProjet.DTOs;

public class GroupeEquipeDto
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string? TypeProjetCompatible { get; set; }
}

public class GroupeEquipeDetailDto : GroupeEquipeDto
{
    public List<EmployeSimplifieDto> Employes { get; set; } = new();
}