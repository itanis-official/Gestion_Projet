using Microsoft.AspNetCore.Mvc;
using GestionProjet.DTOs.AI;
using System.Text;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly GroqService _groq;

    public AiController(GroqService groq)
    {
        _groq = groq;
    }

    [HttpPost("generate-planning")]
    public async Task<IActionResult> Generate([FromBody] GeneratePlanningInput input)
    {
        var prompt = BuildPrompt(input);
        var jsonRaw = await _groq.GeneratePlanning(prompt);

        using var doc = System.Text.Json.JsonDocument.Parse(jsonRaw);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return Content(content, "application/json");
    }

  private string BuildPrompt(GeneratePlanningInput input)
{
    var equipeText = BuildEquipeText(input.EquipeDisponible);

    var startDateStr = input.DateDebut.ToString("yyyy-MM-dd");
    var endDateStr = input.DateFinPrevue.ToString("yyyy-MM-dd");
    var totalDays = (input.DateFinPrevue - input.DateDebut).Days;

    var holidaysText = input.JoursFeries != null && input.JoursFeries.Any()
        ? string.Join(", ", input.JoursFeries)
        : "Aucun";

    return $@"
Tu es un EXPERT en gestion de projets IT (niveau senior).

🎯 OBJECTIF :
Générer un planning COMPLET, RÉALISTE et OPTIMISÉ.

========================
📌 INFORMATIONS PROJET
========================
Nom: {input.ProjetNom}
Description: {input.ProjetDescription ?? "Non spécifiée"}
Type: {input.TypeProjet}
Date début: {startDateStr}
Date fin: {endDateStr}
Durée totale: {totalDays} jours
Budget: {input.BudgetEstime}

========================
👥 ÉQUIPE DISPONIBLE
========================
{equipeText}

========================
📅 JOURS FÉRIÉS
========================
{holidaysText}

========================
⚙️ RÈGLES STRICTES
========================
1. Générer TOUT le planning dynamiquement (phases, tâches, sous-tâches)
2. Ne JAMAIS copier un exemple
3. Adapter les tâches au type de projet: {input.TypeProjet}
4. Utiliser UNIQUEMENT les IDs fournis
5. Ne jamais inventer de membre
6. Assigner selon compétences réelles
7. Répartir la charge équitablement entre les membres
8. Maximum 7h de travail par jour par personne
9. Respecter STRICTEMENT les dates du projet
10. Ne pas planifier pendant weekends et jours fériés
11. Chaque tâche doit contenir 2 à 4 sous-tâches
12. Ajouter des dépendances logiques entre tâches si nécessaire

========================
📊 STRUCTURE OBLIGATOIRE
========================
Phases dans cet ordre EXACT :
- Analyse (15%)
- Conception (20%)
- MiseEnOeuvre (40%)
- Validation (15%)
- MiseEnService (10%)

========================
🧠 INTELLIGENCE ATTENDUE
========================
- Si projet Web → inclure frontend, backend, API
- Si Mobile → inclure UI mobile, tests device
- Si AI → inclure data, training, évaluation
- Si Microservices → inclure architecture distribuée
========================
⚖️ RÉPARTITION DES TÂCHES (OBLIGATOIRE)
========================

- INTERDICTION d’assigner toutes les tâches à une seule personne
- Répartir les tâches entre TOUS les membres disponibles
- Chaque membre doit avoir au moins une tâche si possible

- Calculer la charge de travail :
  charge = somme(dureeEstimeHeures des tâches assignées)

- Ne jamais dépasser :
  7 heures par jour × nombre de jours travaillés

- Si un membre dépasse la charge :
  ➜ assigner automatiquement la tâche à un autre membre

- Priorité d’assignation :
  1. membre avec compétence correspondante
  2. membre avec la PLUS FAIBLE charge actuelle

- IMPORTANT :
  Toujours équilibrer les tâches même si les compétences sont similaires

========================
📦 FORMAT DE RÉPONSE (JSON STRICT)
========================
IMPORTANT:
- Répond UNIQUEMENT avec du JSON
- AUCUN texte avant ou après
- JSON valide (commence par {{ et finit par }})
- Toutes les propriétés doivent être présentes

{{
  ""phases"": [
    {{
      ""typePhase"": ""Analyse"",
      ""pourcentageBudget"": 15,
      ""description"": ""string"",
      ""taches"": [
        {{
          ""titre"": ""string"",
          ""dateDebutPrevue"": ""yyyy-MM-dd"",
          ""dateFinPrevue"": ""yyyy-MM-dd"",
          ""dureeEstimeHeures"": number,
          ""competencesRequises"": [""string""],
          ""responsableId"": number,
          ""responsableNom"": ""string"",
          ""testeurId"": number,
          ""testeurNom"": ""string"",
          ""dependances"": [""string""],
          ""priorite"": ""Haute"",
          ""sousTaches"": [
            {{
              ""titre"": ""string"",
              ""dureeEstimeeHeures"": number,
              ""statut"": ""AFaire"",
              ""description"": ""string""
            }}
          ]
        }}
      ]
    }}
  ],
  ""resume"": ""string"",
  ""suggestions"": {{
    ""alertes"": [""string""],
    ""recommandations"": [""string""],
    ""risques"": [""string""]
  }},
  ""statsGlobales"": {{
    ""totalTaches"": number,
    ""totalSousTaches"": number,
    ""totalHeures"": number,
    ""tauxCouvertureEquipe"": number
  }}
}}";
}
    private string BuildEquipeText(List<MembreDisponible> equipe)
    {
        if (equipe == null || !equipe.Any())
        {
            return "Aucun membre disponible - laisser responsableId = null";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"L'équipe dispose de {equipe.Count} membre(s) :");
        foreach (var m in equipe)
        {
            var competences = m.Competences != null && m.Competences.Any() 
                ? string.Join(", ", m.Competences) 
                : "Aucune compétence spécifiée";
            sb.AppendLine($"- ID: {m.Id} | {m.Nom} | Rôle: {m.Role} | Compétences: {competences}");
        }
        return sb.ToString();
    }

    private int GetFirstMemberId(List<MembreDisponible> equipe)
    {
        return equipe != null && equipe.Any() ? equipe.First().Id : 0;
    }

    private string GetFirstMemberName(List<MembreDisponible> equipe)
    {
        return equipe != null && equipe.Any() ? equipe.First().Nom : "Non assigné";
    }

    private int GetSecondMemberId(List<MembreDisponible> equipe)
    {
        if (equipe == null || equipe.Count < 2) return GetFirstMemberId(equipe);
        return equipe.Skip(1).First().Id;
    }

    private string GetSecondMemberName(List<MembreDisponible> equipe)
    {
        if (equipe == null || equipe.Count < 2) return GetFirstMemberName(equipe);
        return equipe.Skip(1).First().Nom;
    }

    private int GetDesignerId(List<MembreDisponible> equipe)
    {
        if (equipe == null || !equipe.Any()) return 0;
        
        var designer = equipe.FirstOrDefault(m => 
            m.Competences != null && 
            m.Competences.Any(c => c.Contains("UI", StringComparison.OrdinalIgnoreCase) ||
                                   c.Contains("UX", StringComparison.OrdinalIgnoreCase) ||
                                   c.Contains("Design", StringComparison.OrdinalIgnoreCase)));
        
        return designer?.Id ?? GetFirstMemberId(equipe);
    }

    private string GetDesignerName(List<MembreDisponible> equipe)
    {
        if (equipe == null || !equipe.Any()) return "Non assigné";
        
        var designer = equipe.FirstOrDefault(m => 
            m.Competences != null && 
            m.Competences.Any(c => c.Contains("UI", StringComparison.OrdinalIgnoreCase) ||
                                   c.Contains("UX", StringComparison.OrdinalIgnoreCase) ||
                                   c.Contains("Design", StringComparison.OrdinalIgnoreCase)));
        
        return designer?.Nom ?? GetFirstMemberName(equipe);
    }
}