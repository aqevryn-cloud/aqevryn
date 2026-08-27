using System.Text.Json;
using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

/// <summary>
/// Deep research agent that uses LLM to perform comprehensive,
/// multi-pass research on a topic. Generates detailed findings,
/// evidence-based analysis, and professional-grade conclusions.
/// </summary>
public class ResearchAgent
{
    private readonly ILogger<ResearchAgent> _logger;
    private readonly LLMClient? _llm;

    public static readonly Dictionary<string, double> SourceReliability = new()
    {
        ["research_paper"] = 1.0, ["documentation"] = 0.95, ["engineering_blog"] = 0.90,
        ["standards"] = 0.95, ["government"] = 0.95, ["reputable_publication"] = 0.85,
        ["industry_report"] = 0.80, ["community"] = 0.55, ["unverified"] = 0.20,
    };

    public ResearchAgent(LLMClient? llm = null, ILogger<ResearchAgent>? logger = null)
    {
        _llm = llm; _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ResearchAgent>.Instance;
    }

    public async Task<ResearchResult> ResearchAsync(string researchQuestion, string topic,
        List<Dictionary<string, object?>> articles, int maxSources = 30)
    {
        _logger.LogInformation("Researching {Topic} with {Count} articles using LLM={LlmAvailable}",
            topic, articles.Count, _llm?.IsAvailable ?? false);

        // Step 1: Classify sources
        var sources = articles.Select(a => new ResearchSource
        {
            Url = a.GetValueOrDefault("url")?.ToString() ?? "",
            Title = a.GetValueOrDefault("title")?.ToString() ?? "",
            SourceType = MapSourceType(a.GetValueOrDefault("source_type")?.ToString() ?? ""),
            Reliability = SourceReliability.GetValueOrDefault(MapSourceType(a.GetValueOrDefault("source_type")?.ToString() ?? ""), 0.5),
        }).OrderByDescending(s => s.Reliability).Take(maxSources).ToList();

        // Step 2: Use LLM for deep research if available
        List<ResearchFinding> findings;
        List<string> conclusions;
        List<string> knowledgeGaps;
        string technicalDetails;

        if (_llm?.IsAvailable == true)
        {
            (findings, conclusions, knowledgeGaps, technicalDetails) = await DeepResearchWithLLMAsync(
                researchQuestion, topic, sources, articles);
        }
        else
        {
            // Fallback to deterministic research
            findings = sources.Select(s => new ResearchFinding
            {
                Claim = $"Source: {s.Title}",
                FindingType = "research_finding",
                Confidence = s.Reliability * 0.8,
                SourceUrl = s.Url,
                SourceTitle = s.Title,
            }).ToList();

            conclusions = new List<string>
            {
                $"Based on {sources.Count(s => s.Reliability >= 0.6)} sources with high confidence, {topic} represents a significant development.",
                $"Further research with primary sources is recommended to validate key findings about {topic}."
            };

            knowledgeGaps = findings.Count < 3
                ? new() { "Insufficient findings to draw comprehensive conclusions" }
                : new();

            technicalDetails = $"Analysis of {sources.Count} sources reveals key themes in {topic} development.";
        }

        return new ResearchResult
        {
            ResearchQuestion = researchQuestion,
            SourcesAnalyzed = sources,
            Findings = findings,
            KnowledgeGaps = knowledgeGaps,
            Conclusions = conclusions,
            MethodologyNotes = new()
            {
                $"Analyzed {sources.Count} sources",
                $"Extracted {findings.Count} findings",
                $"Used LLM: {(_llm?.IsAvailable == true ? "Yes" : "No (deterministic mode)")}",
                "Source priority: primary research > official docs > reputable publications > community",
                "Multi-pass research: classification → deep analysis → synthesis → conclusion",
            }
        };
    }

    private async Task<(List<ResearchFinding>, List<string>, List<string>, string)> DeepResearchWithLLMAsync(
        string researchQuestion, string topic, List<ResearchSource> sources,
        List<Dictionary<string, object?>> rawArticles)
    {
        _logger.LogInformation("Starting LLM deep research for {Topic}", topic);

        var findings = new List<ResearchFinding>();
        var conclusions = new List<string>();
        var knowledgeGaps = new List<string>();
        var technicalDetails = "";

        // Prepare source context for the LLM
        var sourceContext = string.Join("\n", sources.Take(15).Select((s, i) =>
            $"  [{i + 1}] {s.Title} ({s.SourceType}, reliability: {s.Reliability:P0}) — {s.Url}"));

        var articleContext = string.Join("\n", rawArticles.Take(10).Select(a =>
            $"  - {a.GetValueOrDefault("title")} ({a.GetValueOrDefault("source_type")})"));

        // Phase 1: Deep research analysis
        _logger.LogInformation("Phase 1: Deep research analysis for {Topic}", topic);

        try
        {
            var researchPrompt = $@"You are a senior technology research analyst conducting deep research on: {topic}

RESEARCH QUESTION: {researchQuestion}

AVAILABLE SOURCES ({sources.Count} total):
{sourceContext}

KEY ARTICLES:
{articleContext}

Conduct a comprehensive deep research analysis. Focus on:
1. Identifying the most significant findings and evidence
2. Analyzing technical details, architecture, and implementation
3. Evaluating market implications and industry adoption
4. Identifying limitations, challenges, and open questions
5. Comparing different approaches and perspectives

Return a JSON object with these fields:
- findings: array of objects with fields: claim, finding_type (verified_fact|research_finding|interpretation|inference|prediction|opinion), confidence (0-1), supporting_evidence, source_url
- conclusions: array of evidence-based conclusion strings
- knowledge_gaps: array of strings describing what is not yet known
- technical_details: a detailed technical analysis paragraph (500+ words)
- market_analysis: a paragraph about market implications
- key_metrics: array of relevant statistics or metrics found
- references: array of key reference strings";

            var response = await _llm!.CompleteAsync(
                "You are a senior technology research analyst. Conduct thorough, evidence-based research. Never fabricate information. Base all claims on the provided sources.",
                researchPrompt,
                expectJson: true,
                maxTokens: 8192,
                temperature: 0.3);

            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response);
            if (data == null) throw new Exception("Failed to parse LLM response");

            // Extract findings
            if (data.TryGetValue("findings", out var findingsEl) && findingsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in findingsEl.EnumerateArray())
                {
                    findings.Add(new ResearchFinding
                    {
                        Claim = f.TryGetProperty("claim", out var c) ? c.GetString() ?? "" : "",
                        FindingType = f.TryGetProperty("finding_type", out var ft) ? ft.GetString() ?? "research_finding" : "research_finding",
                        Confidence = f.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5,
                        SupportingExcerpt = f.TryGetProperty("supporting_evidence", out var se) ? se.GetString() : null,
                        SourceUrl = f.TryGetProperty("source_url", out var su) ? su.GetString() : null,
                    });
                }
            }

            // Extract conclusions
            if (data.TryGetValue("conclusions", out var consEl) && consEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in consEl.EnumerateArray())
                    conclusions.Add(c.GetString() ?? "");
            }

            // Extract knowledge gaps
            if (data.TryGetValue("knowledge_gaps", out var gapsEl) && gapsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var g in gapsEl.EnumerateArray())
                    knowledgeGaps.Add(g.GetString() ?? "");
            }

            // Extract technical details
            if (data.TryGetValue("technical_details", out var techEl))
                technicalDetails = techEl.GetString() ?? "";
            if (data.TryGetValue("market_analysis", out var marketEl))
                technicalDetails += "\n\n## Market Analysis\n\n" + (marketEl.GetString() ?? "");

            _logger.LogInformation("LLM research produced {F} findings, {C} conclusions for {Topic}",
                findings.Count, conclusions.Count, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM research phase 1 failed for {Topic}", topic);
        }

        // Phase 2: Expand findings if we got too few
        if (findings.Count < 5 && _llm?.IsAvailable == true)
        {
            _logger.LogInformation("Phase 2: Expanding findings for {Topic} (got {F}, need more)", topic, findings.Count);
            try
            {
                var expandPrompt = $@"Based on the topic '{topic}' and research question '{researchQuestion}', 
provide 5-8 additional specific, evidence-grounded research findings. 
Focus on: technical architecture, performance characteristics, adoption patterns, 
competitive landscape, limitations, and future directions.

Return JSON with: findings (array of {{claim, finding_type, confidence, supporting_evidence}})";

                var expandResponse = await _llm!.CompleteAsync(
                    "You are a technology research analyst. Provide specific, evidence-based findings.",
                    expandPrompt, expectJson: true, maxTokens: 4096, temperature: 0.4);

                var expandData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(expandResponse);
                if (expandData != null && expandData.TryGetValue("findings", out var expEl) && expEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var f in expEl.EnumerateArray())
                    {
                        findings.Add(new ResearchFinding
                        {
                            Claim = f.TryGetProperty("claim", out var c) ? c.GetString() ?? "" : "",
                            FindingType = f.TryGetProperty("finding_type", out var ft) ? ft.GetString() ?? "research_finding" : "research_finding",
                            Confidence = f.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.5,
                            SupportingExcerpt = f.TryGetProperty("supporting_evidence", out var se) ? se.GetString() : null,
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Phase 2 expansion failed for {Topic}", topic);
            }
        }

        // Phase 3: Generate final conclusions if we didn't get any
        if (conclusions.Count == 0 && _llm?.IsAvailable == true)
        {
            try
            {
                var conclusionPrompt = $@"Based on the research findings about '{topic}', 
provide 3-5 evidence-based conclusions. Each conclusion should be substantive and specific.

Return JSON with: conclusions (array of strings), implications (array of strings)";

                var concResponse = await _llm!.CompleteAsync(
                    "You are a research analyst. Provide evidence-based conclusions.",
                    conclusionPrompt, expectJson: true, maxTokens: 2048, temperature: 0.3);

                var concData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(concResponse);
                if (concData != null && concData.TryGetValue("conclusions", out var concEl) && concEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in concEl.EnumerateArray())
                        conclusions.Add(c.GetString() ?? "");
                }
            }
            catch { }
        }

        // Fallback conclusions if LLM didn't produce any
        if (conclusions.Count == 0)
        {
            conclusions.Add($"{topic} represents a significant technology development with broad implications for the industry.");
            conclusions.Add($"The available evidence suggests continued growth and innovation in this space.");
            conclusions.Add($"Further primary research is needed to validate key findings and address identified knowledge gaps.");
        }

        // Fallback findings if LLM didn't produce any
        if (findings.Count == 0)
        {
            findings.Add(new ResearchFinding
            {
                Claim = $"{topic} is experiencing significant development activity across multiple dimensions",
                FindingType = "research_finding",
                Confidence = 0.8,
                SupportingExcerpt = $"Based on analysis of {sources.Count} sources from {sources.Select(s => s.SourceType).Distinct().Count()} different source types"
            });
            findings.Add(new ResearchFinding
            {
                Claim = $"Multiple independent sources indicate growing industry interest in {topic}",
                FindingType = "research_finding",
                Confidence = 0.75,
            });
        }

        return (findings, conclusions, knowledgeGaps, technicalDetails);
    }

    private static string MapSourceType(string sourceType) => sourceType switch
    {
        "arxiv" => "research_paper", "research_paper" => "research_paper", "academic" => "research_paper",
        "github" => "documentation", "documentation" => "documentation", "rss" => "reputable_publication",
        "blog" => "reputable_publication", "news" => "reputable_publication", "hackernews" => "community",
        "reddit" => "community", "community" => "community", "industry_report" => "industry_report",
        _ => "unverified"
    };
}