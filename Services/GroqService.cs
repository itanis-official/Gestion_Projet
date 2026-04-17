using Microsoft.Extensions.Configuration;
using System.Text;
using System.Text.Json;

public class GroqService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    public GroqService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = config["Groq:ApiKey"];
        _http.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<string> GeneratePlanning(string prompt)
    {
        var request = new
        {
            model = "llama-3.1-8b-instant",
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { 
                    role = "system", 
                    content = @"Tu es un expert en gestion de projets IT. 
                    Tu dois répondre UNIQUEMENT avec un objet JSON valide.
                    Tu dois utiliser STRICTEMENT les IDs des membres fournis dans le prompt.
                    Ne crée JAMAIS de nouveaux IDs.
                    Si aucun membre n'a les compétences requises, mets responsableId = null." 
                },
                new { role = "user", content = prompt }
            },
            temperature = 0.3,
            max_tokens = 4096
        };

        var jsonRequest = JsonSerializer.Serialize(request);
        
        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.groq.com/openai/v1/chat/completions"
        );

        httpRequest.Headers.Add("Authorization", $"Bearer {_apiKey}");
        httpRequest.Headers.Add("Accept", "application/json");
        httpRequest.Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(httpRequest);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Groq API Error ({response.StatusCode}): {content}");
        }

        return content;
    }
}