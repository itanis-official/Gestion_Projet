using System.Text;
using System.Text.Json;

namespace GestionProjet.Services;

public class GeminiService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly ILogger<GeminiService> _logger;

    private readonly Dictionary<string, string> _availableModels = new()
    {
        { "gemini-2.0-flash", "Gemini 2.0 Flash (Recommandé)" },
    };

    // ✅ Plus que les modèles qui existent VRAIMENT sur v1beta
    private static readonly string[] FallbackList = 
    {
        "gemini-2.0-flash"
    };

    public GeminiService(HttpClient http, IConfiguration config, ILogger<GeminiService> logger)
    {
        _http = http;
        _apiKey = config["Gemini:ApiKey"];
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(180);
    }

    public Dictionary<string, string> GetAvailableModels() => _availableModels;

    public async Task<string> GeneratePlanning(string prompt, string model = "gemini-2.0-flash")
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Clé API Gemini non configurée.");

        var modelsToTry = new List<string> { model };
        foreach (var fb in FallbackList)
            if (!modelsToTry.Contains(fb))
                modelsToTry.Add(fb);

        string lastError = "";
        var rateLimitedModels = new HashSet<string>();

        foreach (var currentModel in modelsToTry)
        {
            // ✅ Ne pas retenter un modèle qui vient de faire un rate limit
            if (rateLimitedModels.Contains(currentModel))
                continue;

            try
            {
                _logger.LogInformation("🔄 Gemini tentative : {Model}", currentModel);
                var result = await TryGenerate(prompt, currentModel);
                _logger.LogInformation("✅ Gemini succès : {Model}", currentModel);
                return result;
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning("⛔ Échec modèle {Model}: {Error}", currentModel, ex.Message);
                
                if (ex.Message.Contains("not found") || ex.Message.Contains("NOT_FOUND"))
                    continue;

                if (ex.Message.Contains("Limite") || ex.Message.Contains("429"))
                {
                    rateLimitedModels.Add(currentModel);
                    continue;
                }
                    
                throw;
            }
        }

        // ✅ Message clair pour l'utilisateur
        throw new Exception(
            "Trop de requêtes envoyées à l'IA. " +
            "Veuillez patienter 60 secondes avant de réessayer. " +
            "(Quota gratuit : ~15 requêtes/minute)"
        );
    }

    private async Task<string> TryGenerate(string prompt, string model)
    {
        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                maxOutputTokens = 8192,
                responseMimeType = "application/json"
            },
            safetySettings = new[]
            {
                new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
            }
        };

        var jsonRequest = JsonSerializer.Serialize(requestBody);
        
        // ✅ Délai augmenté à 30 secondes entre les tentatives
        var maxRetries = 3;
        var baseDelay = TimeSpan.FromSeconds(30);

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_apiKey}";
                
                var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
                httpRequest.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return ConvertGeminiToOpenAiFormat(content, model);
                }

                // ✅ Rate limit → attendre plus longtemps
                if ((int)response.StatusCode == 429)
                {
                    _logger.LogWarning("Rate limit Gemini, tentative {Attempt}/{Max} - attente {Delay}s", 
                        attempt, maxRetries, baseDelay.TotalSeconds * attempt);
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(baseDelay * attempt);
                        continue;
                    }
                    throw new Exception("Limite de requêtes Gemini atteinte. Patientez.");
                }

                // ✅ Modèle introuvable → ne pas réessayer
                if ((int)response.StatusCode == 404)
                {
                    throw new Exception($"Le modèle '{model}' n'existe pas chez Gemini.");
                }

                _logger.LogError("Gemini erreur {Status}: {Error}", response.StatusCode, content);
                throw new Exception($"Gemini Error ({response.StatusCode}): {content}");
            }
            catch (HttpRequestException ex)
            {
                if (attempt < maxRetries) { await Task.Delay(baseDelay * attempt); continue; }
                throw new Exception($"Erreur réseau Gemini: {ex.Message}", ex);
            }
            catch (TaskCanceledException)
            {
                if (attempt < maxRetries) { await Task.Delay(baseDelay * attempt); continue; }
                throw new Exception("L'appel Gemini a expiré.");
            }
        }

        throw new Exception("Échec après toutes les tentatives.");
    }

    private string ConvertGeminiToOpenAiFormat(string geminiResponse, string model)
    {
        using var doc = JsonDocument.Parse(geminiResponse);
        var root = doc.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            throw new Exception("Gemini n'a retourné aucun candidat.");

        var text = candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text))
            throw new Exception("Gemini a retourné une réponse vide.");

        var openAiResponse = new Dictionary<string, object>
        {
            ["id"] = $"gemini-{Guid.NewGuid():N}",
            ["object"] = "chat.completion",
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["model"] = model,
            ["choices"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["index"] = 0,
                    ["message"] = new Dictionary<string, object>
                    {
                        ["role"] = "assistant",
                        ["content"] = text
                    },
                    ["finish_reason"] = "stop"
                }
            },
            ["usage"] = new Dictionary<string, object>
            {
                ["prompt_tokens"] = 0,
                ["completion_tokens"] = 0,
                ["total_tokens"] = 0
            }
        };

        return JsonSerializer.Serialize(openAiResponse);
    }
}