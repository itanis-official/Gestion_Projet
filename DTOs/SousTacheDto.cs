using GestionProjet.Enums;

namespace GestionProjet.DTOs;

public class SousTacheDto
{
    public int Id { get; set; }
    public int TacheId { get; set; }
    public string Titre { get; set; } = string.Empty;
    public decimal DureeEstimeeHeures { get; set; }
    public StatutSousTache Statut { get; set; }
}

public class SousTacheDetailDto : SousTacheDto
{
    public List<AffectationSimplifieDto> Affectations { get; set; } = new();
    public List<DeclarationTempsSimplifieDto> DeclarationsTemps { get; set; } = new();
}

public class SousTacheSimplifieDto
{
    public int Id { get; set; }
    public string Titre { get; set; } = string.Empty;
    public StatutSousTache Statut { get; set; }
    public decimal DureeEstimeeHeures { get; set; }
}