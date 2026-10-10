using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TrainingAndAssessmentWebAPI.Services;

public sealed class OllamaAnalysisService(HttpClient httpClient, IConfiguration configuration)
{
    private static readonly SemaphoreSlim CloudConcurrency = new(5, 5);

    public async Task<AiResult> GenerateAsync(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("AI prompt is required.", nameof(prompt));
        if (prompt.Length > 30000) throw new ArgumentException("AI prompt is too large.", nameof(prompt));

        var apiKey = configuration["Ollama:ApiKey"] ?? configuration["OLLAMA_API_KEY"];
        var cloudEnabled = !string.IsNullOrWhiteSpace(apiKey);
        var endpoint = configuration["Ollama:Endpoint"]
            ?? (cloudEnabled ? "https://ollama.com" : "http://127.0.0.1:11434");
        var model = configuration["Ollama:Model"]
            ?? (cloudEnabled ? "gemma4:cloud" : "qwen2:1.5b");

        await CloudConcurrency.WaitAsync();
        try
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint.TrimEnd('/')}/api/generate")
                {
                    Content = JsonContent.Create(new { model, prompt, stream = false })
                };
                if (cloudEnabled)
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var response = await httpClient.SendAsync(request);
                if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                {
                    if (attempt == 3) response.EnsureSuccessStatusCode();
                    var retryDelay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(attempt * 2);
                    await Task.Delay(retryDelay > TimeSpan.FromSeconds(15) ? TimeSpan.FromSeconds(15) : retryDelay);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
                if (string.IsNullOrWhiteSpace(result?.Response)) throw new InvalidOperationException("Ollama returned an empty analysis.");
                return new AiResult(result.Response.Trim(), model);
            }
            throw new InvalidOperationException("Ollama did not return a response after three attempts.");
        }
        finally
        {
            CloudConcurrency.Release();
        }
    }

    public async Task<string> AnalyzeFeedbackAsync(string sessionName, int batchStrength, int submitted, double averageRating)
    {
        var prompt = $"""
            You are an academic training feedback analyst. Analyze this feedback data for "{sessionName}":
            batch strength={batchStrength}, submitted feedback={submitted}, submission percentage={(batchStrength == 0 ? 0 : Math.Round(submitted * 100d / batchStrength, 1))}%, average rating={averageRating:F2}/5.
            Write a concise, objective analysis in 2-3 sentences covering participation, satisfaction, and one improvement action. Return plain text only.
            """;

        return (await GenerateAsync(prompt)).Response;
    }

    public sealed record AiResult(string Response, string Model);
    private sealed record OllamaResponse([property: System.Text.Json.Serialization.JsonPropertyName("response")] string Response);
}
