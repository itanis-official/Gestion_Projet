namespace GestionProjet.DTOs.AI
{
    public class GeneratePlanningInput
    {
        public string ProjetNom { get; set; } = string.Empty;
        public string ProjetDescription { get; set; } = string.Empty;
        public string TypeProjet { get; set; } = string.Empty;
        public DateTime DateDebut { get; set; }
        public DateTime DateFinPrevue { get; set; }
        public decimal BudgetEstime { get; set; }
        public List<MembreDisponible> EquipeDisponible { get; set; } = new();
        public int? ProjetId { get; set; } 
        public List<string>? JoursFeries { get; set; }
        public string? Model { get; set; } = "llama-3.1-8b-instant";
    }

    public class MembreDisponible
    {
        public int Id { get; set; }
        public string Nom { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public List<string> Competences { get; set; } = new();
    }
}