using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

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

    public Task<ResearchResult> ResearchAsync(string researchQuestion, string topic,
        List<Dictionary<string, object?>> articles, int maxSources = 30)
    {
        _logger.LogInformation("Researching {Topic} with {Count} articles", topic, articles.Count);

        var sources = articles.Select(a => new ResearchSource
        {
            Url = a.GetValueOrDefault("url")?.ToString() ?? "",
            Title = a.GetValueOrDefault("title")?.ToString() ?? "",
            SourceType = MapSourceType(a.GetValueOrDefault("source_type")?.ToString() ?? ""),
            Reliability = SourceReliability.GetValueOrDefault(MapSourceType(a.GetValueOrDefault("source_type")?.ToString() ?? ""), 0.5),
        }).OrderByDescending(s => s.Reliability).Take(maxSources).ToList();

        var findings = sources.Select(s => new ResearchFinding
        {
            Claim = $"Source: {s.Title}",
            FindingType = "research_finding",
            Confidence = s.Reliability * 0.8,
            SourceUrl = s.Url,
            SourceTitle = s.Title,
        }).ToList();

        var conclusions = new List<string>();
        if (findings.Count > 0)
            conclusions.Add($"Based on {sources.Count(s => s.Reliability >= 0.6)} sources with high confidence, {topic} represents a significant development.");
        conclusions.Add($"Further research with primary sources is recommended to validate key findings about {topic}.");

        var knowledgeGaps = new List<string>();
        if (findings.Count < 3) knowledgeGaps.Add("Insufficient findings to draw comprehensive conclusions");
        if (!findings.Any(f => f.FindingType == "verified_fact")) knowledgeGaps.Add("No verified facts found");

        return Task.FromResult(new ResearchResult
        {
            ResearchQuestion = researchQuestion, SourcesAnalyzed = sources, Findings = findings,
            KnowledgeGaps = knowledgeGaps, Conclusions = conclusions,
            MethodologyNotes = new() { $"Analyzed {sources.Count} sources", $"Extracted {findings.Count} findings", "Source priority: primary research > official docs > reputable publications > community" },
        });
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