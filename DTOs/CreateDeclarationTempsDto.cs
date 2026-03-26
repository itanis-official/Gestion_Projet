public class CreateDeclarationTempsDto
{
    public int SousTacheId { get; set; }
    public DateTime Date { get; set; }
    public decimal DureeHeures { get; set; }
    public string Type { get; set; } = "Travail";
}