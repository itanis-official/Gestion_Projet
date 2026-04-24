using Microsoft.AspNetCore.Mvc;
using GestionProjet.DTOs.AI;
using GestionProjet.Services;
using System.Text.Json;

namespace GestionProjet.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly GroqService _groq;
    private readonly ILogger<AiController> _logger;

    public AiController(GroqService groq, ILogger<AiController> logger)
    {
        _groq = groq;
        _logger = logger;
    }

    [HttpPost("generate-planning")]
    public async Task<IActionResult> Generate([FromBody] GeneratePlanningInput input)
    {
        try
        {
      
            if (string.IsNullOrWhiteSpace(input.ProjetNom))
            {
                return BadRequest(new { message = "Le nom du projet est requis." });
            }

            if (input.DateDebut == default || input.DateFinPrevue == default)
            {
                return BadRequest(new { message = "Les dates du projet sont requises." });
            }

            if (input.DateFinPrevue <= input.DateDebut)
            {
                return BadRequest(new { message = "La date de fin doit être après la date de début." });
            }

            _logger.LogInformation(
                "Génération planning IA - Projet: {Nom}, Début: {Debut}, Fin: {Fin}, Membres: {Membres}",
                input.ProjetNom, input.DateDebut.ToString("yyyy-MM-dd"),
                input.DateFinPrevue.ToString("yyyy-MM-dd"),
                input.EquipeDisponible?.Count ?? 0
            );

           
            var prompt = BuildPrompt(input);

            _logger.LogDebug("Prompt envoyé à Groq (longueur: {Length})", prompt.Length);

           
            var jsonRaw = await _groq.GeneratePlanning(prompt);

            _logger.LogDebug("Réponse Groq reçue (longueur: {Length})", jsonRaw.Length);

            
            string? content;
            try
            {
                using var doc = JsonDocument.Parse(jsonRaw);
                var choices = doc.RootElement.GetProperty("choices");

                if (choices.GetArrayLength() == 0)
                {
                    return StatusCode(502, new { message = "L'IA n'a retourné aucune réponse." });
                }

                content = choices[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                if (string.IsNullOrWhiteSpace(content))
                {
                    return StatusCode(502, new { message = "L'IA a retourné une réponse vide." });
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erreur parsing réponse Groq");
                return StatusCode(502, new
                {
                    message = "La réponse de l'IA n'est pas un JSON valide.",
                    detail = ex.Message
                });
            }

            try
            {
                using var validateDoc = JsonDocument.Parse(content);
                if (!validateDoc.RootElement.TryGetProperty("phases", out _))
                {
                    return StatusCode(502, new
                    {
                        message = "L'IA n'a pas généré la structure attendue (phases manquantes).",
                        preview = content.Length > 300 ? content.Substring(0, 300) + "..." : content
                    });
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Le contenu généré par l'IA n'est pas du JSON valide");
                return StatusCode(502, new
                {
                    message = "L'IA n'a pas généré du JSON valide.",
                    preview = content.Length > 500 ? content.Substring(0, 500) + "..." : content
                });
            }
            

            return Content(content, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur génération planning IA");

            var message = ex.Message;

            if (message.Contains("Groq API Error"))
            {
                return StatusCode(502, new
                {
                    message = "Erreur de communication avec l'IA (Groq).",
                    detail = message
                });
            }

            if (message.Contains("ApiKey") || message.Contains("401"))
            {
                return StatusCode(500, new
                {
                    message = "Clé API Groq invalide ou manquante. Vérifiez la configuration."
                });
            }

            if (message.Contains("429"))
            {
                return StatusCode(429, new
                {
                    message = "Trop de requêtes vers l'IA. Patientez quelques secondes et réessayez."
                });
            }

            if (message.Contains("timeout") || message.Contains("Task canceled"))
            {
                return StatusCode(504, new
                {
                    message = "L'IA met trop longtemps à répondre. Réessayez."
                });
            }

            return StatusCode(500, new
            {
                message = "Erreur interne lors de la génération du planning.",
                detail = message
            });
        }
    }

private string BuildPrompt(GeneratePlanningInput input)
{
    var equipeText = BuildEquipeText(input.EquipeDisponible);
    var startDateStr = input.DateDebut.ToString("yyyy-MM-dd");
    var endDateStr = input.DateFinPrevue.ToString("yyyy-MM-dd");

    return $@"Tu es un Expert PMP. Génère un planning IT JSON pour '{input.ProjetNom}'.

--- RÈGLES DE CAPACITÉ STRICTES (ANTI-OVERLOAD) ---
1. RÈGLE DES 8H : Un membre ne peut pas travailler plus de 8h par jour, TOUTES TÂCHES CONFONDUES.
2. SÉQUENÇAGE : Si un membre a plusieurs tâches, elles ne doivent pas se chevaucher. La tâche B commence après la fin de la tâche A.
3. CALCUL DE DURÉE : Pour une tâche de X heures, dateFin = dateDebut + (X / 8) jours ouvrés.
4. SI SURCHARGE : Si le temps est trop court pour respecter les 8h/jour, tu DOIS réduire le nombre de sous-tâches ou alerter dans le 'resume'.

--- ÉQUIPE ---
{equipeText}

--- LOGIQUE TECHNIQUE ---
- Phase Analyse : Focus sur les Specs. Compétences techniques [] souvent vides ici.
- Phase MiseEnOeuvre : Utilise les compétences tech fournies (ex: TypeScript, Angular).
- Jours Fériés à exclure : {string.Join(", ", input.JoursFeries ?? new List<string>())}.

--- FORMAT JSON ---
{{
  ""phases"": [
    {{
      ""typePhase"": ""MiseEnOeuvre"",
      ""taches"": [
        {{
          ""titre"": ""Développement Frontend"",
          ""dateDebutPrevue"": ""yyyy-MM-dd"",
          ""dateFinPrevue"": ""yyyy-MM-dd"",
          ""responsableId"": 1,
          ""competencesRequises"": [""Angular""],
          ""sousTaches"": [
            {{ ""titre"": ""Composant Home"", ""dureeEstimeeHeures"": 16 }}
          ]
        }}
      ]
    }}
  ],
  ""resume"": ""Explication de la stratégie de lissage de charge.""
}}";
}
private int CalculateWorkDays(DateTime start, DateTime end, List<string>? joursFeries)
{
    int count = 0;
    var current = start;
    var holidays = new HashSet<string>(joursFeries ?? new List<string>());

    while (current <= end)
    {
        var isWeekend = current.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var dateStr = current.ToString("yyyy-MM-dd");
        var isHoliday = holidays.Contains(dateStr);

        if (!isWeekend && !isHoliday) count++;
        current = current.AddDays(1);
    }

    return Math.Max(1, count);
}

private string BuildEquipeText(List<MembreDisponible>? equipe)
{
    if (equipe == null || equipe.Count == 0)
        return "Aucun membre - mettre responsableId = null";

    var lines = equipe.Select(m =>
    {
        var comps = m.Competences?.Any() == true
            ? string.Join(", ", m.Competences)
            : "Aucune";
        return $"- ID:{m.Id} | {m.Nom} | {m.Role} | {comps}";
    });

    return string.Join("\n", lines);
}
}