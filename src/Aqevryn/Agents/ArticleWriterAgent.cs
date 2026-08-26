using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

public class ArticleWriterAgent
{
    private readonly ILogger<ArticleWriterAgent> _logger;
    private readonly LLMClient? _llm;

    public ArticleWriterAgent(LLMClient? llm = null, ILogger<ArticleWriterAgent>? logger = null)
    {
        _llm = llm; _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ArticleWriterAgent>.Instance;
    }

    public Task<GeneratedArticle> WriteAsync(string topic, ResearchResult researchResult)
    {
        _logger.LogInformation("Writing article for {Topic}", topic);

        var slug = topic.ToLower().Replace(" ", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9-]", "").Trim('-');
        if (slug.Length > 80) slug = slug[..80];

        var findings = researchResult.Findings;
        var sources = researchResult.SourcesAnalyzed;
        var conclusions = researchResult.Conclusions;

        var findingLines = string.Join("\n", findings.Take(8).Select(f => $"- **{f.Claim}** (*{f.FindingType}*, confidence: {f.Confidence:P0})"));
        var conclusionLines = string.Join("\n", conclusions.Take(5));

        var references = sources.Select((s, i) => $"[{i + 1}] {s.Title} — {s.Url} ({s.SourceType})").ToList();

        var article = new GeneratedArticle
        {
            Title = $"Research: {topic} — An Evidence-Based Analysis",
            Slug = slug,
            Description = $"An evidence-based investigation into {topic}, analyzing {sources.Count} sources.",
            Introduction = $"This article presents an evidence-based analysis of {topic}. Drawing on {sources.Count} sources, we examine the current state, key findings, and implications of this technology.",
            WhyThisMatters = $"{topic} represents a significant area of technology development with implications for industry, research, and practice.",
            Background = $"{topic} has emerged as a notable technology topic. This research synthesizes evidence from multiple sources.",
            ResearchQuestion = researchResult.ResearchQuestion,
            TechnicalAnalysis = $"Our analysis reveals several key themes. The technology landscape shows active development with {sources.Count} sources analyzed and {findings.Count} findings extracted.",
            Findings = findingLines,
            MarketImplications = "Market analysis indicates active interest from industry participants.",
            Limitations = "This analysis is based on publicly available sources. The technology landscape evolves rapidly.",
            FutureOutlook = $"Continued development in {topic} is expected.",
            Conclusion = $"{topic} represents a significant technology development. Evidence from {sources.Count} sources suggests meaningful progress.",
            References = references,
            Methodology = $"This research was conducted by analyzing {sources.Count} sources across research papers, engineering blogs, and industry publications.",
        };

        return Task.FromResult(article);
    }

    private static readonly System.Text.RegularExpressions.Regex SlugRegex = new(@"[^a-z0-9-]", System.Text.RegularExpressions.RegexOptions.Compiled);
}