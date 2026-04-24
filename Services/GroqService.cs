using System.Text;
using System.Text.Json;

namespace GestionProjet.Services;

public class GroqService
{
    private readonly HttpClient _http;
    private readonly string? _apiKey;
    private readonly ILogger<GroqService> _logger;

    public GroqService(HttpClient http, IConfiguration config, ILogger<GroqService> logger)
    {
        _http = http;
        _apiKey = config["Groq:ApiKey"];
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(90);
    }

    public async Task<string> GeneratePlanning(string prompt)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Clé API Groq non configurée.");

        var request = new
        {
            model = "llama-3.1-8b-instant",
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

                _logger.LogInformation("Tentative {Attempt}/{Max} - Prompt: {Length} chars", attempt, maxRetries, prompt.Length);

                var response = await _http.SendAsync(httpRequest);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Groq succès - {Length} chars", content.Length);
                    return content;
                }

                // ── 429 : Rate limit (TPM ou RPM) → retry avec délai ──
                if ((int)response.StatusCode == 429)
                {
                    var delay = baseDelay * attempt; // 5s, 10s, 15s

                    _logger.LogWarning(
                        "Rate limit (tentative {Attempt}/{Max}). Attente {Delay}s...",
                        attempt, maxRetries, delay.TotalSeconds
                    );

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delay);
                        continue;
                    }

                    throw new Exception(
                        "Limite de requêtes atteinte (tier gratuit Groq). " +
                        "Attendez 60 secondes et réessayez, ou passez au plan Dev : https://console.groq.com/settings/billing"
                    );
                }

                // ── Autres erreurs HTTP ──
                _logger.LogError("Groq erreur {Status}: {Body}", response.StatusCode,
                    content.Length > 300 ? content.Substring(0, 300) : content);

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