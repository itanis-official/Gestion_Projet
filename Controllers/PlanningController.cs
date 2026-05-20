using Microsoft.AspNetCore.Mvc;
using GestionProjet.DTOs.AI;
using GestionProjet.Services;
using GestionProjet.Data;
using UglyToad.PdfPig;
using System.Text.Json;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using System.Text.RegularExpressions; // ✅ NÉCESSAIRE pour CleanJsonContent

namespace GestionProjet.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly GroqService _groq;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AiController> _logger;
    private readonly PlanningValidationService _validationService;


    public AiController(
        GroqService groq,
        ApplicationDbContext db,
        ILogger<AiController> logger,
        PlanningValidationService validationService)
    {
        _groq = groq;
        _db = db;
        _logger = logger;
        _validationService = validationService;
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
                return BadRequest(new { message = "Le nom du projet est requis." });

            if (input.DateDebut == default || input.DateFinPrevue == default)
                return BadRequest(new { message = "Les dates du projet sont requises." });

            if (input.DateFinPrevue <= input.DateDebut)
                return BadRequest(new { message = "La date de fin doit être après la date de début." });

            _logger.LogInformation("🚀 Génération planning IA - Projet: {Nom}, Modèle: {Model}", input.ProjetNom, input.Model ?? "default");

            // 1. Extraction du CDC
            string cdcTexte = "";
            if (input.ProjetId.HasValue)
            {
                var projet = await _db.Projets.FindAsync(input.ProjetId.Value);
                if (projet != null && !string.IsNullOrEmpty(projet.CdcFileUrl))
                {
                    try { cdcTexte = ExtractTextFromFile(projet.CdcFileUrl); }
                    catch (Exception ex) { _logger.LogWarning(ex, "💥 Exception extraction fichier"); }
                }
            }

            // 2. Génération IA brute
            var prompt = BuildPrompt(input, cdcTexte);
            var model = string.IsNullOrEmpty(input.Model) ? "llama-3.1-8b-instant" : input.Model;
            var jsonRaw = await _groq.GeneratePlanning(prompt, model);

            string content;
            try
            {
                using var doc = JsonDocument.Parse(jsonRaw);
                var choices = doc.RootElement.GetProperty("choices");
                if (choices.GetArrayLength() == 0)
                    return StatusCode(502, new { message = "L'IA n'a retourné aucune réponse." });

                content = choices[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                if (string.IsNullOrWhiteSpace(content))
                    return StatusCode(502, new { message = "L'IA a retourné une réponse vide." });
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erreur parsing réponse Groq");
                return StatusCode(502, new { message = "Réponse IA non JSON valide.", detail = ex.Message });
            }

            // ✅ FIX 1 : NETTOYAGE DE LA RÉPONSE (Pour gérer le markdown ```json ...)
            content = CleanJsonContent(content);

            // 3. Variables pour stocker les données extraites
            string? aiResume = null;
            var validationRequest = new ValidatePlanningRequest
            {
                ProjetNom = input.ProjetNom,
                DateDebut = input.DateDebut,
                DateFinPrevue = input.DateFinPrevue,
                EquipeDisponible = input.EquipeDisponible,
                JoursFeries = input.JoursFeries,
                Phases = new List<PhaseGenereeInput>()
            };

            // 4. EXTRACTION COMPLÈTE DANS UN SEUL BLOC USING
            try
            {
                using var generatedDoc = JsonDocument.Parse(content);

                if (!generatedDoc.RootElement.TryGetProperty("phases", out var phasesElement))
                    return StatusCode(502, new { message = "Structure 'phases' manquante.", preview = content.Substring(0, Math.Min(300, content.Length)) });

                if (generatedDoc.RootElement.TryGetProperty("resume", out var resumeEl))
                    aiResume = resumeEl.GetString();

                _logger.LogInformation("🔍 Application de la correction automatique silencieuse...");

                foreach (var phaseEl in phasesElement.EnumerateArray())
                {
                    var phaseInput = new PhaseGenereeInput
                    {
                        TypePhase = phaseEl.GetProperty("typePhase").GetString() ?? "",
                        Taches = new List<TacheGenereeInput>()
                    };

                    if (phaseEl.TryGetProperty("taches", out var tachesEl))
                    {
                        foreach (var tacheEl in tachesEl.EnumerateArray())
                        {
                            var tacheInput = new TacheGenereeInput
                            {
                                Titre = tacheEl.GetProperty("titre").GetString() ?? "",
                                DateDebutPrevue = tacheEl.GetProperty("dateDebutPrevue").GetString() ?? "",
                                DateFinPrevue = tacheEl.GetProperty("dateFinPrevue").GetString() ?? "",
                                CompetencesRequises = new List<string>(),
                                SousTaches = new List<SousTacheGenereeInput>()
                            };

                            // ✅ FIX 2 : PARSING ROBUSTE POUR RESPONSABLEID (Gère String ou Number)
                            if (tacheEl.TryGetProperty("responsableId", out var respIdEl) && respIdEl.ValueKind != JsonValueKind.Null)
                            {
                                if (respIdEl.ValueKind == JsonValueKind.Number)
                                {
                                    tacheInput.ResponsableId = respIdEl.GetInt32();
                                }
                                else if (respIdEl.ValueKind == JsonValueKind.String)
                                {
                                    // Le modèle 8b renvoie parfois l'ID en string "5"
                                    if (int.TryParse(respIdEl.GetString(), out var idStr))
                                        tacheInput.ResponsableId = idStr;
                                }
                            }

                            if (tacheEl.TryGetProperty("competencesRequises", out var compEl))
                                foreach (var comp in compEl.EnumerateArray())
                                    tacheInput.CompetencesRequises.Add(comp.GetString() ?? "");

                            if (tacheEl.TryGetProperty("sousTaches", out var stEl))
                            {
                                foreach (var st in stEl.EnumerateArray())
                                {
                                    // ✅ FIX 3 : PARSING ROBUSTE POUR DURÉE (Gère String ou Number)
                                    decimal duree = 0m;
                                    if (st.TryGetProperty("dureeEstimeeHeures", out var dureeEl))
                                    {
                                        if (dureeEl.ValueKind == JsonValueKind.Number)
                                            duree = dureeEl.GetDecimal();
                                        else if (dureeEl.ValueKind == JsonValueKind.String)
                                        {
                                            if (decimal.TryParse(dureeEl.GetString(), out var dureeStr))
                                                duree = dureeStr;
                                        }
                                    }

                                    tacheInput.SousTaches.Add(new SousTacheGenereeInput
                                    {
                                        Titre = st.GetProperty("titre").GetString() ?? "",
                                        DureeEstimeeHeures = duree
                                    });
                                }
                            }

                            phaseInput.Taches.Add(tacheInput);
                        }
                    }
                    validationRequest.Phases.Add(phaseInput);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON invalide lors de l'extraction");
                return StatusCode(502, new { message = "JSON invalide.", preview = content.Substring(0, Math.Min(500, content.Length)) });
            }

            var validationResult = _validationService.ValidateAndCorrect(validationRequest);

            if (validationResult.Statistics.AutoCorrected > 0)
            {
                _logger.LogInformation("✅ {Count} correction(s) appliquée(s) silencieusement", validationResult.Statistics.AutoCorrected);
            }

            var finalPhases = validationResult.CorrectedPhases.Select(p => new {
                typePhase = p.TypePhase,
                taches = p.Taches.Select(t => new {
                    titre = t.Titre,
                    dateDebutPrevue = t.DateDebutPrevue,
                    dateFinPrevue = t.DateFinPrevue,
                    responsableId = t.ResponsableId,
                    competencesRequises = t.CompetencesRequises,
                    sousTaches = t.SousTaches.Select(st => new {
                        titre = st.Titre,
                        dureeEstimeeHeures = st.DureeEstimeeHeures
                    })
                })
            });
            var totalTasks = validationResult.CorrectedPhases.Sum(p => p.Taches.Count);
            var totalSubTasks = validationResult.CorrectedPhases.Sum(p => p.Taches.Sum(t => t.SousTaches.Count));
            var totalHours = validationResult.CorrectedPhases.Sum(p => p.Taches.Sum(t => t.SousTaches.Sum(st => st.DureeEstimeeHeures)));

            var finalResponse = new
            {
                resume = aiResume,
                phases = finalPhases,
                statsGlobales = new
                {
                    totalTaches = totalTasks,
                    totalSousTaches = totalSubTasks,
                    totalHeures = totalHours
                }
            };

            return Ok(finalResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur génération planning IA");
            return StatusCode(500, new { message = "Erreur interne.", detail = ex.Message });
        }
    }

    // --- MÉTHODES PRIVÉES ---

    /// <summary>
    /// Nettoie la réponse JSON de l'IA pour supprimer les balises Markdown (```json ... ```)
    /// </summary>
    private string CleanJsonContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return content;

        // 1. Chercher un bloc de code Markdown
        var match = Regex.Match(content, @"```json\s*(?<json>[\s\S]*?)\s*```", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups["json"].Value.Trim();
        }

        // 2. Sinon, chercher le premier { et le dernier }
        var firstBrace = content.IndexOf('{');
        var lastBrace = content.LastIndexOf('}');

        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return content.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return content.Trim();
    }

    private string ExtractTextFromFile(string relativeUrl)
    {
        try
        {
            var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var fileName = Path.GetFileName(relativeUrl);
            var filePath = Path.Combine(wwwrootPath, "uploads", "cdc", fileName);
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            _logger.LogInformation("🔍 Extraction : {Path} ({Extension})", filePath, extension);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("📂 Fichier introuvable.");
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
                        foreach (var word in page.GetWords())
                            texte.Append(word.Text).Append(" ");
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
                        texte.Append(mainPart.Document.Body.InnerText);
                }
            }
            else
            {
                _logger.LogWarning("⚠️ Format non supporté : {Extension}", extension);
                return string.Empty;
            }

            return texte.ToString();
        }
        catch (System.IO.IOException ex)
        {
            _logger.LogError(ex, "💥 Erreur accès fichier");
            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lecture fichier");
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
            _logger.LogInformation("📄 Extrait CDC : {Extrait}", cdcTexte.Length > 100 ? cdcTexte.Substring(0, 100) : cdcTexte);
            contexteCdc = $@"--- CAHIER DES CHARGES ---
{textToUse}
";
        }
        else
        {
            _logger.LogWarning("⚠️ AUCUN TEXTE CDC.");
        }

        return $@"Tu es un Expert PMP. Génère un planning IT JSON pour '{input.ProjetNom}'.

{contexteCdc}
--- DATES : {startDateStr} → {endDateStr} ---
RÈGLE 8H/JOUR MAX PAR MEMBRE : La charge totale d'un membre par jour ne doit jamais excéder 8 heures.
--- AU MOINS 1 SOUS-TÂCHE PAR TÂCHE ---
--- ÉQUIPE ---
{equipeText}
--- RÈGLES STRICTES SUR LES COMPÉTENCES (À APPLIQUER ABSOLUMENT) ---
Le champ ""competencesRequises"" doit contenir les technologies qui sont RÉELLEMENT utilisées pour faire la tâche. Tu ne dois JAMAIS mélanger le Front-end et le Back-end.
 Ne JAMAIS mettre une compétence de programmation dans les phases Analyse ou Conception
 Avant de définir ""competencesRequises"" pour une tâche, tu DOIS obligatoirement lire son titre et son contenu pour déterminer les compétences nécessaires. SI tu ne trouves aucune compétence spécifique, LAISSE LA LISTE VIDE.
 RÈGLE D'OR : Tu ne mets un langage de programmation (Java, TypeScript, React...) dans ""competencesRequises"" QUE SI le titre de la tâche implique directement d'écrire du code source (ex: Développer, Coder, Implémenter, Programmer).
--- FORMAT JSON ---
{{
  ""phases"": [
    {{
      ""typePhase"": """",
      ""taches"": [
        {{
          ""titre"": """",
          ""dateDebutPrevue"": """",
          ""dateFinPrevue"": """",
          ""responsableId"": null,
          ""competencesRequises"": [],
          ""sousTaches"": [{{ ""titre"": """", ""dureeEstimeeHeures"": 0 }}]
        }}
      ]
    }}
  ],
  ""resume"": """"
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
            var isHoliday = holidays.Contains(current.ToString("yyyy-MM-dd"));
            if (!isWeekend && !isHoliday) count++;
            current = current.AddDays(1);
        }
        return Math.Max(1, count);
    }

    private string BuildEquipeText(List<MembreDisponible>? equipe)
    {
        if (equipe == null || equipe.Count == 0)
            return "Aucun membre - responsableId = null";

        return string.Join("\n", equipe.Select(m =>
        {
            var comps = m.Competences?.Any() == true ? string.Join(", ", m.Competences) : "Aucune";
            return $"- ID:{m.Id} | {m.Nom} | {m.Role} | {comps}";
        }));
    }
}