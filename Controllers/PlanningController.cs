using Microsoft.AspNetCore.Mvc;
using GestionProjet.DTOs.AI;
using GestionProjet.Services;
using GestionProjet.Data;       
using UglyToad.PdfPig;      
using System.Text.Json;
using System.Text;           
          

namespace GestionProjet.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly GroqService _groq;
    private readonly ApplicationDbContext _db; 
    private readonly ILogger<AiController> _logger;

    public AiController(GroqService groq, ApplicationDbContext db, ILogger<AiController> logger)
    {
        _groq = groq;
        _db = db;
        _logger = logger;
    }

    [HttpPost("generate-planning")]
    public async Task<IActionResult> Generate([FromBody] GeneratePlanningInput input)
    {
        try
        {
            // Validations de base
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

            // ================================================================
            // 1. EXTRACTION DU TEXTE DU PDF (SI UN PROJET EST FOURNI)
            // ================================================================
            string cdcTexte = "";
            if (input.ProjetId.HasValue)
            {
                var projet = await _db.Projets.FindAsync(input.ProjetId.Value);
                if (projet != null && !string.IsNullOrEmpty(projet.CdcFileUrl))
                {
                    _logger.LogInformation("📄 Extraction du CDC depuis l'URL : {Url}", projet.CdcFileUrl);
                    try
                    {
                        cdcTexte = ExtractTextFromPdf(projet.CdcFileUrl);
                        _logger.LogInformation("✅ PDF extrait avec succès ({Length} caractères).", cdcTexte.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Erreur lors de l'extraction du PDF. Génération sans contexte CDC.");
                        cdcTexte = ""; // On continue sans le texte du PDF s'il y a une erreur
                    }
                }
                else
                {
                    _logger.LogInformation("ℹ️ Aucun projet ou aucun fichier CDC trouvé pour l'ID {Id}.", input.ProjetId.Value);
                }
            }
            // ================================================================

            // 2. Construction du Prompt
            var prompt = BuildPrompt(input, cdcTexte);
            _logger.LogDebug("Prompt envoyé à Groq (longueur: {Length})", prompt.Length);

            // 3. Appel à l'API IA (Groq)
            var jsonRaw = await _groq.GeneratePlanning(prompt);
            _logger.LogDebug("Réponse Groq reçue (longueur: {Length})", jsonRaw.Length);

            // 4. Parsing de la réponse JSON de l'IA
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

            // 5. Validation de la structure interne (Phase, Taches...)
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

            // Retourner le JSON brut au Frontend
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

    // ========================================================================
    // MÉTHODE D'EXTRACTION PDF (VERSION ROBUSTE)
    // ========================================================================
    
    /// <summary>
    /// Ouvre le fichier PDF sur le disque et en extrait tout le texte.
    /// Utilise GetWords() pour une compatibilité maximale avec toutes les versions de PdfPig.
    /// </summary>
    private string ExtractTextFromPdf(string relativeUrl)
    {
        try
        {
            // 1. Construire le chemin physique du fichier
            var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            
            // Sécurisation du nom du fichier
            var fileName = Path.GetFileName(relativeUrl);
            var filePath = Path.Combine(wwwrootPath, "uploads", "cdc", fileName);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("📂 Fichier PDF non trouvé sur le disque : {Path}", filePath);
                return string.Empty;
            }

            // 2. Utiliser PdfPig pour extraire le texte via GetWords()
            var texte = new StringBuilder();
            using (var document = PdfDocument.Open(filePath))
            {
                foreach (var page in document.GetPages())
                {
                    // GetWords() retourne une liste de mots
                    var words = page.GetWords();
                    
                    // On joint les mots avec un espace
                    foreach (var word in words)
                    {
                        texte.Append(word.Text).Append(" ");
                    }
                    
                    // Ajout d'un saut de ligne à la fin de chaque page
                    texte.Append("\n\n"); 
                }
            }

            return texte.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur technique lors de la lecture du fichier PDF");
            return string.Empty;
        }
    }

    // ========================================================================
    // CONSTRUCTION DU PROMPT (Mis à jour pour inclure le texte CDC)
    // ========================================================================

   private string BuildPrompt(GeneratePlanningInput input, string cdcTexte)
{
    var equipeText = BuildEquipeText(input.EquipeDisponible);
    var startDateStr = input.DateDebut.ToString("yyyy-MM-dd");
    var endDateStr = input.DateFinPrevue.ToString("yyyy-MM-dd");

    // Insertion du contexte CDC dans le prompt si disponible
    string contexteCdc = "";
    if (!string.IsNullOrEmpty(cdcTexte))
    {
        // On tronque à 3000 caractères si trop long pour éviter de dépasser le contexte de l'IA
        var textToUse = cdcTexte.Length > 3000 ? cdcTexte.Substring(0, 3000) + "...[tronqué]" : cdcTexte;
        
        contexteCdc = $@"--- CAHIER DES CHARGES (EXTRAIT DU PDF) ---
Basé sur le document suivant, décris les tâches spécifiques et fonctionnalités requises. Si le document contient des phases ou des livrables listés, respecte-les rigoureusement.

[CONTENU DU PDF COMMENCE ICI]
{textToUse}
[CONTENU DU PDF SE TERMINE ICI]
";
    }

    return $@"Tu es un Expert PMP. Génère un planning IT JSON pour '{input.ProjetNom}'.

{contexteCdc}

--- CONTRAINTES DE DATES IMPÉRATIVES (À RESPECTER STRICTEMENT) ---
⚠️ RÈGLE ABSOLUE ⚠️ : TOUTES les dates des tâches DOIVENT être comprises entre :
- Date de début du projet : {startDateStr}
- Date de fin du projet : {endDateStr}

Aucune tâche ne peut commencer avant {startDateStr}
Aucune tâche ne peut se terminer après {endDateStr}

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

--- FORMAT JSON (TOUJOURS VALIDE) ---
{{
  ""phases"": [
    {{
      ""typePhase"": ""Analyse"",
      ""taches"": [
        {{
          ""titre"": ""Analyse des besoins"",
          ""dateDebutPrevue"": ""{startDateStr}"",
          ""dateFinPrevue"": ""2026-05-05"",
          ""responsableId"": null,
          ""competencesRequises"": [],
          ""sousTaches"": [
            {{ ""titre"": ""Rédaction specs"", ""dureeEstimeeHeures"": 16 }}
          ]
        }}
      ]
    }},
    {{
      ""typePhase"": ""MiseEnOeuvre"",
      ""taches"": [
        {{
          ""titre"": ""Développement Frontend"",
          ""dateDebutPrevue"": ""2026-05-06"",
          ""dateFinPrevue"": ""{endDateStr}"",
          ""responsableId"": 1,
          ""competencesRequises"": [""Angular""],
          ""sousTaches"": [
            {{ ""titre"": ""Composant Home"", ""dureeEstimeeHeures"": 16 }}
          ]
        }}
      ]
    }}
  ],
  ""resume"": ""Explication de la stratégie de lissage de charge et respect des dates du projet.""
}}

IMPORTANT : 
- La date de début de la première tâche doit être >= {startDateStr}
- La date de fin de la dernière tâche doit être <= {endDateStr}
- Répartis les tâches sur toute la durée du projet ({startDateStr} → {endDateStr})";
}

    // ========================================================================
    // METHODES UTILITAIRES
    // ========================================================================

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