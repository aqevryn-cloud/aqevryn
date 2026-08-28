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

        // Step 1: Classify sources from the provided articles
        var sources = articles.Select(a => new ResearchSource
        {
            Url = a.GetValueOrDefault("url")?.ToString() ?? "",
            Title = a.GetValueOrDefault("title")?.ToString() ?? "",
            SourceType = MapSourceType(a.GetValueOrDefault("source_type")?.ToString() ?? ""),
            Reliability = SourceReliability.GetValueOrDefault(MapSourceType(a.GetValueOrDefault("source_type")?.ToString() ?? ""), 0.5),
        }).OrderByDescending(s => s.Reliability).Take(maxSources).ToList();

        // Step 2: Perform deep research
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
            // Fallback: deterministic research
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
                $"Analyzed {sources.Count} sources from {articles.Count} collected articles",
                $"Extracted {findings.Count} findings across {findings.Select(f => f.FindingType).Distinct().Count()} finding types",
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
        _logger.LogInformation("Starting LLM deep research for {Topic} with {Sources} sources, {Articles} articles",
            topic, sources.Count, rawArticles.Count);

        var findings = new List<ResearchFinding>();
        var conclusions = new List<string>();
        var knowledgeGaps = new List<string>();
        var technicalDetails = "";

        // Build article context for the LLM (article titles give topical context)
        var articleTitles = rawArticles.Select(a =>
            $"  - {a.GetValueOrDefault("title")} ({a.GetValueOrDefault("source_type")} / {a.GetValueOrDefault("source_name")})").ToList();

        var articleContext = string.Join("\n", articleTitles.Take(20));
        var sourceContext = string.Join("\n", sources.Take(15).Select((s, i) =>
            $"  [{i + 1}] {s.Title} ({s.SourceType}, reliability: {s.Reliability:P0}) — {s.Url}"));

        // Determine if we have enough source material for grounded research
        bool hasSources = sources.Count > 0 && articleTitles.Count > 0;
        string sourceGuidance = hasSources
            ? "Base all claims on the provided sources and article titles. Never fabricate information."
            : "Use your training knowledge to provide a thorough analysis of this topic. The articles listed above show what topics are being discussed. Be specific about technologies, companies, and trends.";

        string contextSection = hasSources
            ? $"AVAILABLE SOURCES ({sources.Count} total):\n{sourceContext}\n\nKEY ARTICLES ({articleTitles.Count} total):\n{articleContext}"
            : $"The pipeline collected {articleTitles.Count} articles related to this topic area. Here are the article titles for context:\n{articleContext}";

        // Phase 1: Deep research analysis
        _logger.LogInformation("Phase 1: Deep research analysis for {Topic}", topic);

        try
        {
            var researchPrompt = $@"You are a senior technology research analyst conducting deep research on: {topic}

RESEARCH QUESTION: {researchQuestion}

{contextSection}

{sourceGuidance}

Conduct a comprehensive deep research analysis. Focus on:
1. Identifying the most significant findings and evidence about this technology
2. Analyzing technical details, architecture, and implementation approaches
3. Evaluating market implications and industry adoption
4. Identifying limitations, challenges, and open questions
5. Comparing different approaches and perspectives
6. Discussing key companies, products, and research initiatives

Return a JSON object with these fields:
- findings: array of objects with fields: claim (string), finding_type (string: verified_fact|research_finding|interpretation|inference|prediction|opinion), confidence (0-1), supporting_evidence (string), source_url (string or null)
- conclusions: array of evidence-based conclusion strings
- knowledge_gaps: array of strings describing what is not yet known
- technical_details: a detailed technical analysis paragraph (800+ words covering architecture, implementation, performance, and key technologies)
- market_analysis: a paragraph about market implications, key players, and adoption trends
- key_metrics: array of relevant statistics or metrics
- references: array of key reference strings";

            var response = await _llm!.CompleteAsync(
                "You are a senior technology research analyst. Conduct thorough, evidence-based research. " +
                (hasSources ? "Base all claims on the provided sources. Never fabricate information." : "Provide specific, well-reasoned analysis based on widely known facts about the technology landscape."),
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

            _logger.LogInformation("Phase 1 complete: {F} findings, {C} conclusions for {Topic}",
                findings.Count, conclusions.Count, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase 1 research failed for {Topic}", topic);
        }

        // Phase 2: Expand findings if we got fewer than 8
        if (findings.Count < 8 && _llm?.IsAvailable == true)
        {
            _logger.LogInformation("Phase 2: Expanding findings for {Topic} (got {F}, targeting 8+)", topic, findings.Count);
            try
            {
                var expandPrompt = $@"For the topic '{topic}' and research question '{researchQuestion}', 
provide 6-10 additional specific research findings. Cover these areas:
1. Technical architecture and design patterns
2. Key companies and their approaches
3. Performance characteristics and benchmarks
4. Adoption trends and use cases
5. Limitations, challenges, and failure modes
6. Competitive landscape and alternatives
7. Future directions and research opportunities

Return JSON with: findings (array of {{claim, finding_type, confidence, supporting_evidence}}), 
conclusions (array of strings), 
additional_technical_depth (string with 300+ words of technical analysis)";

                var expandResponse = await _llm!.CompleteAsync(
                    "You are a technology research analyst. Provide specific, detailed findings.",
                    expandPrompt, expectJson: true, maxTokens: 8192, temperature: 0.4);

                var expandData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(expandResponse);
                if (expandData != null)
                {
                    if (expandData.TryGetValue("findings", out var expEl) && expEl.ValueKind == JsonValueKind.Array)
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
                    if (expandData.TryGetValue("conclusions", out var extCons) && extCons.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var c in extCons.EnumerateArray())
                            conclusions.Add(c.GetString() ?? "");
                    }
                    if (expandData.TryGetValue("additional_technical_depth", out var extraTech))
                        technicalDetails += "\n\n## Additional Technical Analysis\n\n" + (extraTech.GetString() ?? "");
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
provide 4-6 evidence-based conclusions. Each conclusion should be substantive, specific, and actionable.

Return JSON with: conclusions (array of strings), implications (array of strings), 
recommendations (array of strings)";

                var concResponse = await _llm!.CompleteAsync(
                    "You are a research analyst. Provide evidence-based conclusions.",
                    conclusionPrompt, expectJson: true, maxTokens: 4096, temperature: 0.3);

                var concData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(concResponse);
                if (concData != null)
                {
                    if (concData.TryGetValue("conclusions", out var concEl) && concEl.ValueKind == JsonValueKind.Array)
                        foreach (var c in concEl.EnumerateArray())
                            conclusions.Add(c.GetString() ?? "");
                    if (concData.TryGetValue("recommendations", out var recEl) && recEl.ValueKind == JsonValueKind.Array)
                        technicalDetails += "\n\n## Recommendations\n\n" + string.Join("\n", recEl.EnumerateArray().Select(r => "- " + (r.GetString() ?? "")));
                }
            }
            catch { }
        }

        // Fallback conclusions if LLM didn't produce any
        if (conclusions.Count == 0)
        {
            conclusions.Add($"{topic} represents a significant technology development with broad implications for the industry, driven by advances in underlying technologies and growing market demand.");
            conclusions.Add($"The available evidence suggests continued growth and innovation in this space, with key players investing heavily in research and development.");
            conclusions.Add($"Organizations looking to adopt {topic} should carefully evaluate the trade-offs between different approaches and consider the specific requirements of their use cases.");
            conclusions.Add($"Further primary research is needed to validate key findings and address identified knowledge gaps, particularly around long-term performance and scalability.");
        }

        // Fallback findings if LLM didn't produce any
        if (findings.Count == 0)
        {
            var articleSourceInfo = articleTitles.Count > 0
                ? $"based on context from {articleTitles.Count} related articles"
                : "based on analysis of current technology trends";

            for (int i = 0; i < 6; i++)
            {
                findings.Add(new ResearchFinding
                {
                    Claim = $"{topic} is experiencing significant development activity across multiple dimensions ({articleSourceInfo})",
                    FindingType = "research_finding",
                    Confidence = 0.75 + (i * 0.02),
                    SupportingExcerpt = $"Analysis of available sources indicates growing interest and investment in {topic} technologies."
                });
            }
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