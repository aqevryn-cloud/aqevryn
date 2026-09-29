using System.Text.Json;
using Aqevryn.Config;

namespace Aqevryn.Agents;

/// <summary>
/// Implements soul.md §14 — actively searches for evidence that could disprove
/// emerging conclusions before finalizing them.
/// </summary>
public class FalsifiabilityChecker
{
    private readonly AqevrynSettings _settings;

    public FalsifiabilityChecker(AqevrynSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Given a set of findings and a conclusion, search for evidence that could disprove it.
    /// Returns counterarguments and their source strength.
    /// </summary>
    public async Task<FalsifiabilityReport> CheckAsync(string topic, string conclusion,
        List<Common.ResearchFinding> findings)
    {
        var report = new FalsifiabilityReport
        {
            Topic = topic,
            Conclusion = conclusion,
            DisconfirmingEvidence = new List<DisconfirmingEvidence>(),
        };

        // 1. Check the knowledge graph for existing contradictions
        var contradictions = Common.KnowledgeGraph.SearchDisconfirmingEvidence(conclusion);
        foreach (var c in contradictions)
        {
            report.DisconfirmingEvidence.Add(new DisconfirmingEvidence
            {
                Claim = c.Claim,
                Source = c.SourceUrl ?? "knowledge graph",
                Strength = c.Confidence >= 0.7 ? "strong" : c.Confidence >= 0.4 ? "moderate" : "weak",
                FoundIn = "knowledge graph",
            });
        }

        // 2. Check findings for internal contradictions
        for (int i = 0; i < findings.Count; i++)
        {
            for (int j = i + 1; j < findings.Count; j++)
            {
                if (ClaimsContradict(findings[i].Claim, findings[j].Claim))
                {
                    report.DisconfirmingEvidence.Add(new DisconfirmingEvidence
                    {
                        Claim = $"Finding #{i + 1} and #{j + 1} may contradict each other",
                        Source = $"{findings[i].SourceUrl ?? "unknown"} vs {findings[j].SourceUrl ?? "unknown"}",
                        Strength = "needs_review",
                        FoundIn = "internal_analysis",
                    });
                }
            }
        }

        // 3. Use LLM to suggest what evidence would disprove the conclusion
        if (_settings.LlmProvider != "mock" && !string.IsNullOrEmpty(_settings.LlmApiKey))
        {
            await ExpandWithLLMAsync(topic, conclusion, report);
        }

        report.OverallFalsifiabilityScore = report.DisconfirmingEvidence.Count > 0
            ? Math.Max(0, 100 - report.DisconfirmingEvidence.Count * 15)
            : 85; // High score = conclusion is hard to disprove (not necessarily good)

        return report;
    }

    private async Task ExpandWithLLMAsync(string topic, string conclusion, FalsifiabilityReport report)
    {
        try
        {
            var prompt = $"I'm researching '{topic}' and have reached this conclusion: \"{conclusion}\"\n\nWhat evidence could potentially disprove or challenge this conclusion? List 2-4 specific counterarguments or scenarios that would invalidate it. Return JSON only: {{\"counterarguments\": [{{\"argument\": \"...\", \"evidence_needed\": \"...\"}}]}}";

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

            if (data != null && data.TryGetValue("counterarguments", out var args) && args.ValueKind == JsonValueKind.Array)
            {
                foreach (var arg in args.EnumerateArray())
                {
                    report.DisconfirmingEvidence.Add(new DisconfirmingEvidence
                    {
                        Claim = arg.TryGetProperty("argument", out var a) ? a.GetString() ?? "" : "",
                        Source = arg.TryGetProperty("evidence_needed", out var e) ? e.GetString() ?? "LLM suggestion" : "LLM suggestion",
                        Strength = "hypothetical",
                        FoundIn = "llm_analysis",
                    });
                }
            }
        }
        catch { }
    }

    private static bool ClaimsContradict(string c1, string c2)
    {
        var opposing = new[] { ("increase", "decrease"), ("improve", "degrade"), ("better", "worse"),
                               ("advantage", "disadvantage"), ("supports", "contradicts"), ("proven", "disproven"),
                               ("growing", "shrinking"), ("accelerating", "slowing"), ("successful", "unsuccessful") };
        var l1 = c1.ToLowerInvariant();
        var l2 = c2.ToLowerInvariant();
        return opposing.Any(p => (l1.Contains(p.Item1) && l2.Contains(p.Item2)) ||
                                  (l1.Contains(p.Item2) && l2.Contains(p.Item1)));
    }
}

public class FalsifiabilityReport
{
    public string Topic { get; set; } = "";
    public string Conclusion { get; set; } = "";
    public List<DisconfirmingEvidence> DisconfirmingEvidence { get; set; } = new();
    public double OverallFalsifiabilityScore { get; set; } = 0;
}

public class DisconfirmingEvidence
{
    public string Claim { get; set; } = "";
    public string Source { get; set; } = "";
    public string Strength { get; set; } = ""; // strong, moderate, weak, hypothetical, needs_review
    public string FoundIn { get; set; } = "";
}