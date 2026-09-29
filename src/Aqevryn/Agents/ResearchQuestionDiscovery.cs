using System.Text.Json;
using Aqevryn.Config;

namespace Aqevryn.Agents;

/// <summary>
/// Generates specific research questions from collected articles using LLM.
/// Implements soul.md §21 (Researchability) and §38 (Research Opportunity Engine).
/// Transforms broad topics into specific, researchable questions.
/// </summary>
public class ResearchQuestionDiscovery
{
    private readonly AqevrynSettings _settings;

    public ResearchQuestionDiscovery(AqevrynSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Given a topic and articles, generate the most valuable research question.
    /// </summary>
    public async Task<string> GenerateQuestionAsync(string topic, List<Dictionary<string, object?>> articles)
    {
        // Default question based on topic keywords
        var defaultQuestion = GenerateDefaultQuestion(topic);

        if (_settings.LlmProvider == "mock" || string.IsNullOrEmpty(_settings.LlmApiKey))
            return defaultQuestion;

        try
        {
            var titles = string.Join("\n", articles.Take(20).Select(a =>
                $"- {a.GetValueOrDefault("title")} ({a.GetValueOrDefault("source_type")})"));

            var prompt = $"I am researching '{topic}'. Here are the related articles collected:\n\n{titles}\n\n"
                + "Based on these articles, what is the MOST valuable, specific research question I should investigate?\n\n"
                + "A good research question should:\n"
                + "- Reveal something poorly understood\n"
                + "- Challenge an existing assumption\n"
                + "- Expose a meaningful trend\n"
                + "- Be specific enough to investigate with available evidence\n"
                + "- NOT be answerable with a simple yes/no\n\n"
                + "Return ONLY a JSON object: {\"research_question\": \"...\", \"rationale\": \"...\", \"subquestions\": [\"...\", \"...\"]}";

            var baseUrl = _settings.LlmProvider switch
            {
                "openai" => "https://api.openai.com/v1",
                "openrouter" => "https://openrouter.ai/api/v1",
                _ => "https://api.openai.com/v1"
            };
            var model = _settings.LlmModel == "gpt-5-nano" || string.IsNullOrEmpty(_settings.LlmModel) ? "gpt-4o-mini" : _settings.LlmModel;

            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.LlmApiKey}");
            http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");
            if (_settings.LlmProvider == "openrouter")
            {
                http.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/aqevryn-cloud/aqevryn");
                http.DefaultRequestHeaders.Add("X-Title", "Aqevryn Research");
            }

            var payloadObj = new
            {
                model = model,
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = 500,
                temperature = 0.3,
                response_format = new { type = "json_object" }
            };

            var httpContent = new StringContent(JsonSerializer.Serialize(payloadObj), System.Text.Encoding.UTF8, "application/json");
            var response = await http.PostAsync($"{baseUrl}/chat/completions", httpContent);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(text);

            if (data != null && data.TryGetValue("research_question", out var rq) && rq.ValueKind == JsonValueKind.String)
            {
                var question = rq.GetString() ?? "";
                if (!string.IsNullOrEmpty(question) && question.Length > 15)
                    return question;
            }
        }
        catch { }

        return defaultQuestion;
    }

    private static string GenerateDefaultQuestion(string topic)
    {
        var lower = topic.ToLowerInvariant();
        return lower switch
        {
            _ when lower.Contains("agent") => $"What are the primary failure modes affecting {topic} reliability, and what mitigation strategies exist?",
            _ when lower.Contains("security") => $"How do {topic} approaches compare across different industries, and what cross-cutting patterns emerge?",
            _ when lower.Contains("quantum") => $"What are the practical barriers to {topic} commercialization, and which approaches show most promise?",
            _ when lower.Contains("climate") || lower.Contains("energy") => $"What is the gap between {topic} targets and current trajectories, and what interventions show measurable impact?",
            _ when lower.Contains("economic") || lower.Contains("finance") => $"What factors most significantly influence {topic}, and how resilient are current models to unexpected shocks?",
            _ when lower.Contains("health") || lower.Contains("medic") => $"What evidence gaps exist in {topic} research, and where is the strongest case for policy change?",
            _ when lower.Contains("history") => $"How does {topic} inform current decision-making, and what lessons are being repeated or ignored?",
            _ when lower.Contains("education") => $"Which {topic} approaches show the strongest evidence of impact, and why have they not scaled?",
            _ => $"What are the key factors driving {topic} adoption and what critical limitations remain unresolved?"
        };
    }
}