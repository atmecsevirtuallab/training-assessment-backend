using System.Net.Http.Json;
using System.Text.Json;

namespace TrainingAndAssessmentWebAPI.Services;

public sealed class OllamaAnalysisService(HttpClient httpClient, IConfiguration configuration)
{
    public async Task<string> AnalyzeFeedbackAsync(string sessionName, int batchStrength, int submitted, double averageRating)
    {
        var endpoint = configuration["Ollama:Endpoint"] ?? "http://127.0.0.1:11434";
        var model = configuration["Ollama:Model"] ?? "qwen2:1.5b";
        var prompt = $"""
            You are an academic training feedback analyst. Analyze this feedback data for "{sessionName}":
            batch strength={batchStrength}, submitted feedback={submitted}, submission percentage={(batchStrength == 0 ? 0 : Math.Round(submitted * 100d / batchStrength, 1))}%, average rating={averageRating:F2}/5.
            Write a concise, objective analysis in 2-3 sentences covering participation, satisfaction, and one improvement action. Return plain text only.
            """;

        using var response = await httpClient.PostAsJsonAsync($"{endpoint.TrimEnd('/')}/api/generate", new { model, prompt, stream = false });
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OllamaResponse>();
        if (string.IsNullOrWhiteSpace(result?.Response)) throw new InvalidOperationException("Ollama returned an empty analysis.");
        return result.Response.Trim();
    }

    private sealed record OllamaResponse([property: System.Text.Json.Serialization.JsonPropertyName("response")] string Response);
}
