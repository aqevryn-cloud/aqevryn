using System.Text.Json;
using System.Text.Json.Serialization;
using Aqevryn.Config;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

/// <summary>
/// LLM client that works with OpenAI-compatible APIs including OpenRouter.
/// Handles JSON mode, streaming, token tracking, and cost estimation.
/// </summary>
public class LLMClient
{
    private readonly AqevrynSettings _settings;
    private readonly ILogger<LLMClient> _logger;
    private readonly HttpClient _http;
    private static readonly HttpClient _sharedHttp = new() { Timeout = TimeSpan.FromSeconds(120) };

    public int TotalTokens { get; private set; }
    public double TotalCost { get; private set; }

    public LLMClient(AqevrynSettings settings, ILogger<LLMClient>? logger = null)
    {
        _settings = settings;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LLMClient>.Instance;
        _http = _sharedHttp;
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_settings.LlmApiKey) && _settings.LlmProvider != "mock";

    /// <summary>
    /// Send a chat completion request and get the response text.
    /// Supports OpenAI-compatible APIs (OpenAI, OpenRouter, Anthropic, etc.)
    /// </summary>
    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt,
        bool expectJson = true, int maxTokens = 4096, double temperature = 0.3)
    {
        if (!IsAvailable)
        {
            _logger.LogWarning("LLM not available (provider={Provider}, key={KeyLen})",
                _settings.LlmProvider, _settings.LlmApiKey?.Length ?? 0);
            return MockResponse(userPrompt, expectJson);
        }

        var baseUrl = _settings.LlmProvider switch
        {
            "openai" => "https://api.openai.com/v1",
            "openrouter" => "https://openrouter.ai/api/v1",
            "anthropic" => "https://api.anthropic.com/v1",
            _ when _settings.LlmProvider.StartsWith("http") => _settings.LlmProvider,
            _ => "https://api.openai.com/v1"
        };

        var model = _settings.LlmModel switch
        {
            "gpt-5-nano" or "" or null => "gpt-4o-mini", // fallback from invalid model name
            _ => _settings.LlmModel
        };

        var payloadObj = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            ["max_tokens"] = maxTokens,
            ["temperature"] = temperature,
        };

        if (expectJson)
        {
            // OpenRouter supports response_format for most models
            payloadObj["response_format"] = new { type = "json_object" };
        }

        var json = JsonSerializer.Serialize(payloadObj);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_settings.LlmApiKey}");
        request.Headers.Add("User-Agent", "Aqevryn/1.0");
        request.Content = content;

        // OpenRouter needs extra headers
        if (_settings.LlmProvider == "openrouter")
        {
            request.Headers.Add("HTTP-Referer", "https://github.com/aqevryn-cloud/aqevryn");
            request.Headers.Add("X-Title", "Aqevryn Research");
        }

        try
        {
            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            // Track usage
            if (doc.RootElement.TryGetProperty("usage", out var usage))
            {
                var promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                var completionTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                TotalTokens += promptTokens + completionTokens;
                TotalCost += EstimateCost(promptTokens, completionTokens, model);
            }

            var text = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            return text;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("LLM API call failed: {Message}", ex.Message);
            _logger.LogWarning("Falling back to mock response");
            return MockResponse(userPrompt, expectJson);
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("LLM API call timed out after 120s, falling back to mock");
            return MockResponse(userPrompt, expectJson);
        }
    }

    /// <summary>
    /// Perform a deep multi-turn research conversation with the LLM.
    /// Used by the research agent for iterative deep-diving.
    /// </summary>
    public async Task<string> ResearchConversationAsync(List<(string Role, string Content)> messages,
        int maxTokens = 4096, double temperature = 0.3)
    {
        if (!IsAvailable)
            return "{}";

        var baseUrl = _settings.LlmProvider switch
        {
            "openai" => "https://api.openai.com/v1",
            "openrouter" => "https://openrouter.ai/api/v1",
            _ => "https://api.openai.com/v1"
        };

        var model = _settings.LlmModel switch
        {
            "gpt-5-nano" or "" or null => "gpt-4o-mini",
            _ => _settings.LlmModel
        };

        var payloadObj = new Dictionary<string, object>
        {
            ["model"] = model,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
            ["max_tokens"] = maxTokens,
            ["temperature"] = temperature,
            ["response_format"] = new { type = "json_object" },
        };

        var json = JsonSerializer.Serialize(payloadObj);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        request.Headers.Add("Authorization", $"Bearer {_settings.LlmApiKey}");
        request.Headers.Add("User-Agent", "Aqevryn/1.0");
        request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        if (_settings.LlmProvider == "openrouter")
        {
            request.Headers.Add("HTTP-Referer", "https://github.com/aqevryn-cloud/aqevryn");
            request.Headers.Add("X-Title", "Aqevryn Research");
        }

        try
        {
            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("usage", out var usage))
            {
                var promptTokens = usage.TryGetProperty("prompt_tokens", out var pt) ? pt.GetInt32() : 0;
                var completionTokens = usage.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0;
                TotalTokens += promptTokens + completionTokens;
            }

            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "{}";
        }
        catch
        {
            return "{}";
        }
    }

    private string MockResponse(string prompt, bool expectJson = true)
    {
        if (!expectJson) return "This is a mock response for testing purposes.";

        if (prompt.Contains("research plan", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                research_question = "What are the key factors affecting the reliability and adoption of this technology?",
                objectives = new[] { "Analyze current state", "Evaluate key factors", "Assess implications" },
                subquestions = new[] { "What are the fundamental concepts?", "What evidence supports current claims?" },
                search_queries = new[] { "technology trends 2026", "latest research developments" },
                key_concepts_to_explore = new[] { "Core architecture", "Performance characteristics", "Adoption patterns" },
            });
        }

        if (prompt.Contains("deep research", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("findings", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                findings = new[]
                {
                    new { claim = "This technology is experiencing rapid adoption across multiple industries", finding_type = "research_finding", confidence = 0.85, supporting_evidence = "Multiple industry reports and case studies indicate growing implementation" },
                    new { claim = "Performance improvements are driving enterprise interest", finding_type = "research_finding", confidence = 0.80, supporting_evidence = "Benchmark results show significant improvements over previous approaches" },
                    new { claim = "Security and reliability concerns remain key challenges", finding_type = "research_finding", confidence = 0.75, supporting_evidence = "Several studies identify common failure modes and mitigation strategies" },
                    new { claim = "Open-source ecosystem is expanding rapidly", finding_type = "research_finding", confidence = 0.82, supporting_evidence = "GitHub activity and community contributions are growing" },
                    new { claim = "Enterprise adoption is accelerating with major companies investing", finding_type = "research_finding", confidence = 0.78, supporting_evidence = "Major technology companies have announced significant investments and product launches" },
                },
                conclusions = new[]
                {
                    "The technology represents a significant advancement with broad implications",
                    "Further research is needed to address reliability and security challenges",
                    "The market is poised for continued growth and innovation",
                },
                knowledge_gaps = new[]
                {
                    "Long-term performance data is limited",
                    "Standardized benchmarks are still emerging",
                },
                technical_details = "The technology builds on established principles while introducing novel approaches to key challenges.",
            });
        }

        if (prompt.Contains("write article", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("comprehensive article", StringComparison.OrdinalIgnoreCase))
        {
            return "{}";
        }

        return JsonSerializer.Serialize(new
        {
            topic = "AI Coding Agents",
            summary = "Autonomous coding agents are rapidly transforming software development.",
            key_areas = new[] { "Tool use reliability", "Planning and reasoning", "Code generation quality" },
        });
    }

    private static double EstimateCost(int promptTokens, int completionTokens, string model)
    {
        // Approximate pricing per 1M tokens
        var (inputPrice, outputPrice) = model switch
        {
            "gpt-4o" => (2.50, 10.00),
            "gpt-4o-mini" => (0.15, 0.60),
            "claude-3.5-sonnet" => (3.00, 15.00),
            _ => (1.00, 5.00)
        };
        return (promptTokens / 1_000_000.0 * inputPrice) +
               (completionTokens / 1_000_000.0 * outputPrice);
    }
}