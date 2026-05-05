using Microsoft.AspNetCore.Mvc;
using GestionProjet.DTOs.AI;
using GestionProjet.Services;
using GestionProjet.Data;       
using UglyToad.PdfPig;      
using System.Text.Json;
using System.Text;
using DocumentFormat.OpenXml.Packaging; 

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

    [HttpGet("models")]
    public IActionResult GetAvailableModels()
    {
        var models = _groq.GetAvailableModels();
        return Ok(models.Select(m => new { id = m.Key, name = m.Value }));
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
                "🚀 Génération planning IA - Projet: {Nom}, Modèle: {Model}",
                input.ProjetNom, input.Model ?? "default"
            );

            string cdcTexte = "";

            if (!input.ProjetId.HasValue)
            {
                _logger.LogWarning("🚫 Échec extraction : Le champ 'ProjetId' est NULL dans la requête envoyée par le frontend.");
            }
            else
            {
                _logger.LogInformation("📥 Recherche du projet ID {ProjetId} en base de données...", input.ProjetId.Value);
                
                var projet = await _db.Projets.FindAsync(input.ProjetId.Value);

                if (projet == null)
                {
                    _logger.LogError("❌ Échec extraction : Aucun projet trouvé en base avec l'ID {ProjetId}.", input.ProjetId.Value);
                }
                else if (string.IsNullOrEmpty(projet.CdcFileUrl))
                {
                    _logger.LogWarning("❌ Échec extraction : Le projet existe (ID: {Id}), mais la colonne 'CdcFileUrl' est VIDE ou NULL.", projet.Id);
                }
                else
                {
                    _logger.LogInformation("✅ Projet trouvé. URL du fichier stockée en BDD : {Url}", projet.CdcFileUrl);
                    
                    try
                    {
                        cdcTexte = ExtractTextFromFile(projet.CdcFileUrl);
                        
                        if (string.IsNullOrEmpty(cdcTexte))
                        {
                            _logger.LogWarning("❌ Échec extraction : Aucun texte n'a pu être extrait du fichier.");
                        }
                        else
                        {
                            _logger.LogInformation("✅ SUCCÈS : Fichier extrait ({Length} caractères).", cdcTexte.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "💥 Exception critique lors de l'extraction du fichier");
                    }
                }
            }

            var prompt = BuildPrompt(input, cdcTexte);
            
            _logger.LogInformation("📏 Taille du prompt envoyé à l'IA : {Length} caractères.", prompt.Length);
            
            var model = string.IsNullOrEmpty(input.Model) ? "llama-3.1-8b-instant" : input.Model;
            var jsonRaw = await _groq.GeneratePlanning(prompt, model);

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
                        message = "L'IA n'a pas généré la structure attendue.",
                        preview = content.Length > 300 ? content.Substring(0, 300) + "..." : content
                    });
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Le contenu généré n'est pas du JSON valide");
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
            return StatusCode(500, new
            {
                message = "Erreur interne lors de la génération du planning.",
                detail = ex.Message
            });
        }
    }
    
    private string ExtractTextFromFile(string relativeUrl)
    {
        try
        {
            var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            
            var fileName = Path.GetFileName(relativeUrl);
            var filePath = Path.Combine(wwwrootPath, "uploads", "cdc", fileName);
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            _logger.LogInformation("🔍 Tentative d'extraction : {Path} (Format: {Extension})", filePath, extension);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("📂 Le fichier n'existe PAS à cet emplacement.");
                return string.Empty;
            }

            var texte = new StringBuilder();

            if (extension == ".pdf")
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var document = PdfDocument.Open(fileStream))
                {
                    foreach (var page in document.GetPages())
                    {
                        var words = page.GetWords();
                        foreach (var word in words)
                        {
                            texte.Append(word.Text).Append(" ");
                        }
                        texte.Append("\n\n"); 
                    }
                }
            }
           
            else if (extension == ".docx")
            {
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var wordDocument = WordprocessingDocument.Open(fileStream, false))
                {
                    var mainPart = wordDocument.MainDocumentPart;
                    if (mainPart != null)
                    {
                        var bodyText = mainPart.Document.Body.InnerText;
                        texte.Append(bodyText);
                    }
                }
            }
            else
            {
                _logger.LogWarning("⚠️ Format de fichier non supporté : {Extension}. Seuls PDF et DOCX sont acceptés.", extension);
                return string.Empty;
            }

            return texte.ToString();
        }
        catch (System.IO.IOException ex)
        {
            _logger.LogError(ex, "💥 Erreur d'accès fichier (vérifiez les permissions)");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur technique lors de la lecture du fichier");
            return string.Empty;
        }
    }

    private string BuildPrompt(GeneratePlanningInput input, string cdcTexte)
    {
        var equipeText = BuildEquipeText(input.EquipeDisponible);
        var startDateStr = input.DateDebut.ToString("yyyy-MM-dd");
        var endDateStr = input.DateFinPrevue.ToString("yyyy-MM-dd");

        string contexteCdc = "";
        if (!string.IsNullOrEmpty(cdcTexte))
        {
           
            var textToUse = cdcTexte.Length > 3000 ? cdcTexte.Substring(0, 3000) + "...[tronqué]" : cdcTexte;
                 _logger.LogInformation("📄 EXTRAIT DU CDC (100 premiers caractères): {Extrait}", 
            cdcTexte.Length > 100 ? cdcTexte.Substring(0, 100) : cdcTexte);
            contexteCdc = $@"--- CAHIER DES CHARGES (EXTRAIT DU FICHIER) ---
Basé sur le document suivant, décris les tâches spécifiques et fonctionnalités requises. Si le document contient des phases ou des livrables listés, respecte-les rigoureusement.

[CONTENU DU FICHIER COMMENCE ICI]
{textToUse}
[CONTENU DU FICHIER SE TERMINE ICI]
";
        }
        else
        {
            _logger.LogWarning("⚠️ AUCUN TEXTE EXTRAIT. L'IA travaillera sans contexte CDC.");
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
2.Au moins une sous-tâche est requisee pour chaque tâche.
--- ÉQUIPE ---
{equipeText}
--- COMPÉTENCES PAR PHASE ---
- Phase Analyse : AUCUNE compétence technique de programmation

--- JOURS FÉRIÉS ---
{string.Join(", ", input.JoursFeries ?? new List<string>())}.
--- FORMAT JSON (TOUJOURS VALIDE) ---
{{
  ""phases"": [
    {{
      ""typePhase"": "",
      ""taches"": [
        {{
          ""titre"": "",
          ""dateDebutPrevue"": "",
          ""dateFinPrevue"": "",
          ""responsableId"": null,
          ""competencesRequises"": [],
          ""sousTaches"": [
            {{ ""titre"": "", ""dureeEstimeeHeures"":  }}
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