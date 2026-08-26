using System.Text.Json;
using Aqevryn.Config;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

public class LLMClient
{
    private readonly AqevrynSettings _settings;
    private readonly ILogger<LLMClient> _logger;
    private readonly HttpClient _http;
    public int TotalTokens { get; private set; }
    public double TotalCost { get; private set; }

    public LLMClient(AqevrynSettings settings, ILogger<LLMClient>? logger = null)
    {
        _settings = settings;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LLMClient>.Instance;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, bool expectJson = true)
    {
        if (string.IsNullOrEmpty(_settings.LlmApiKey) && _settings.LlmProvider != "mock")
            throw new InvalidOperationException("LLM API key not configured");

        if (_settings.LlmProvider == "mock")
            return MockResponse(userPrompt);

        var payload = new
        {
            model = _settings.LlmModel,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            max_tokens = _settings.LlmMaxTokens,
            temperature = _settings.LlmTemperature,
            response_format = expectJson ? new { type = "json_object" } : null
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        _http.DefaultRequestHeaders.Remove("Authorization");
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.LlmApiKey}");

        var baseUrl = _settings.LlmProvider switch
        {
            "openai" => "https://api.openai.com/v1",
            "anthropic" => "https://api.anthropic.com/v1",
            _ => "https://api.openai.com/v1"
        };

        var response = await _http.PostAsync($"{baseUrl}/chat/completions", content);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(responseJson);

        var usage = doc.RootElement.TryGetProperty("usage", out var u) ? u : default;
        if (usage.ValueKind != JsonValueKind.Undefined)
        {
            TotalTokens += usage.TryGetProperty("total_tokens", out var tt) ? tt.GetInt32() : 0;
        }

        var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        return text;
    }

    private string MockResponse(string prompt)
    {
        if (prompt.Contains("topic", StringComparison.OrdinalIgnoreCase) || prompt.Contains("researchability", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                topic = "AI Coding Agents",
                summary = "Autonomous coding agents are rising in adoption.",
                why_trending = "Rapid adoption among developers",
                evidence = new[] { "Developer survey data" },
                related_topics = new[] { "LLM Agents", "Tool use" },
                source_ids = Array.Empty<string>()
            });
        }
        return "{}";
    }
}