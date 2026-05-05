using System.Text;
using System.Text.Json;

namespace GestionProjet.Services;

public class GroqService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly ILogger<GroqService> _logger;

    
    private static readonly string[] ModelFallbackList = 
    {
        "llama-3.3-70b-versatile",
        "llama-3.1-8b-instant",
        "llama3-70b-8192",
        "llama3-8b-8192"
    };

    private readonly Dictionary<string, string> _availableModels = new()
    {
        { "llama-3.3-70b-versatile", "Llama 3.3 70B (Haute performance)" },
        { "llama-3.1-8b-instant", "Llama 3.1 8B (Rapide)" },
    };

    public GroqService(HttpClient http, IConfiguration config, ILogger<GroqService> logger)
    {
        _http = http;
        _apiKey = config["Groq:ApiKey"];
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(120);
    }

    public Dictionary<string, string> GetAvailableModels() => _availableModels;

    public async Task<string> GeneratePlanning(string prompt, string model = "llama-3.1-8b-instant")
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Clé API Groq non configurée.");

       
        var modelsToTry = new List<string> { model };
        foreach (var fallback in ModelFallbackList)
        {
            if (!modelsToTry.Contains(fallback))
                modelsToTry.Add(fallback);
        }

        var lastError = "";

        foreach (var currentModel in modelsToTry)
        {
            try
            {
                _logger.LogInformation("🔄 Tentative avec le modèle : {Model}", currentModel);
                var result = await TryGenerate(prompt, currentModel);
                _logger.LogInformation("✅ Succès avec le modèle : {Model}", currentModel);
                return result;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                
                if (ex.Message.Contains("decommissioned") || 
                    ex.Message.Contains("no longer supported"))
                {
                    _logger.LogWarning("⛔ Modèle déprécié : {Model}, passage au suivant...", currentModel);
                    continue;
                }
                throw;
            }
        }

        throw new Exception($"Aucun modèle disponible. Dernière erreur : {lastError}");
    }

    private async Task<string> TryGenerate(string prompt, string model)
    {
        var request = new
        {
            model = model,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = "Tu es un expert planning IT. Répond UNIQUEMENT en JSON valide. Utilise les IDs membres fournis." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = 4096
        };

        var jsonRequest = JsonSerializer.Serialize(request);
        var maxRetries = 3;
        var baseDelay = TimeSpan.FromSeconds(5);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var httpRequest = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.groq.com/openai/v1/chat/completions"
                );

                httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
                httpRequest.Headers.Add("Accept", "application/json");
                httpRequest.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return content;
                }

                if (content.Contains("decommissioned") || content.Contains("no longer supported"))
                {
                    throw new Exception($"Le modèle '{model}' a été décommissionné par Groq.");
                }

                if ((int)response.StatusCode == 429)
                {
                    var delay = baseDelay * attempt;
                    _logger.LogWarning("Rate limit, tentative {Attempt}/{Max}", attempt, maxRetries);
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delay);
                        continue;
                    }
                    throw new Exception("Limite de requêtes atteinte. Patientez et réessayez.");
                }

                _logger.LogError("Groq erreur {Status} avec modèle {Model}", response.StatusCode, model);
                throw new Exception($"Groq API Error ({response.StatusCode}): {content}");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Erreur réseau tentative {Attempt}", attempt);
                if (attempt < maxRetries)
                {
                    await Task.Delay(baseDelay * attempt);
                    continue;
                }
                throw new Exception($"Erreur réseau Groq: {ex.Message}", ex);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("Timeout tentative {Attempt}", attempt);
                if (attempt < maxRetries)
                {
                    await Task.Delay(baseDelay * attempt);
                    continue;
                }
                throw new Exception("L'appel à l'IA a expiré. Réessayez.");
            }
        }

        throw new Exception("Échec après toutes les tentatives.");
    }
}