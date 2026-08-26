using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

public class ResearchabilityAgent
{
    private readonly ILogger<ResearchabilityAgent> _logger;

    public ResearchabilityAgent(ILogger<ResearchabilityAgent>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ResearchabilityAgent>.Instance;
    }

    public Task<ResearchabilityResult> EvaluateAsync(string topic, List<Dictionary<string, object?>> articles)
    {
        _logger.LogInformation("Evaluating researchability for {Topic} with {Count} articles", topic, articles.Count);
        if (articles.Count == 0)
        {
            return Task.FromResult(new ResearchabilityResult
            {
                Topic = topic, Score = 0, Evidence = new() { "No articles available" },
                Weaknesses = new() { "No source material found" }
            });
        }

        var sourceTypes = articles.Select(a => a.GetValueOrDefault("source_type")?.ToString() ?? "unknown").GroupBy(t => t).ToDictionary(g => g.Key, g => g.Count());
        var hasPrimary = sourceTypes.Keys.Any(t => t is "arxiv" or "research_paper" or "academic");
        var hasAcademic = hasPrimary;
        var hasTechnical = sourceTypes.Keys.Any(t => t is "github" or "documentation" or "rss" or "blog");
        var hasImplementations = CheckImplementations(articles);
        var hasCompeting = CheckCompetingViewpoints(articles);

        var sourceQuality = ScoreSourceQuality(sourceTypes, articles);
        var depthScore = ScoreDepth(articles);
        var specificity = ScoreSpecificity(topic);

        var score = sourceQuality * 0.35 + depthScore * 0.35 + specificity * 0.30;
        score = Math.Clamp(score, 0, 100);

        var evidence = new List<string>
        {
            $"Analyzed {articles.Count} articles",
            hasPrimary ? "Primary sources available" : "No primary sources identified",
            hasAcademic ? "Academic papers available" : "No academic papers found",
            hasTechnical ? "Technical documentation found" : "",
        };
        evidence.RemoveAll(string.IsNullOrEmpty);

        var weaknesses = new List<string>();
        if (!hasPrimary) weaknesses.Add("No primary sources identified");
        if (!hasAcademic) weaknesses.Add("No academic papers found");
        if (!hasImplementations) weaknesses.Add("No implementations or benchmarks found");

        var researchQuestion = score >= 50 ? GenerateResearchQuestion(topic, articles) : null;

        return Task.FromResult(new ResearchabilityResult
        {
            Topic = topic,
            Score = Math.Round(score, 1),
            HasPrimarySources = hasPrimary,
            HasAcademicPapers = hasAcademic,
            HasTechnicalDocs = hasTechnical,
            HasImplementations = hasImplementations,
            HasCompetingViewpoints = hasCompeting,
            ResearchQuestion = researchQuestion,
            Evidence = evidence,
            Weaknesses = weaknesses,
        });
    }

    private static bool CheckImplementations(List<Dictionary<string, object?>> articles)
    {
        var keywords = new[] { "implementation", "benchmark", "open source", "github", "repository", "code", "dataset", "api", "framework", "tool", "library", "package" };
        foreach (var a in articles)
        {
            var text = string.Join(" ", a.GetValueOrDefault("title")?.ToString() ?? "", a.GetValueOrDefault("summary")?.ToString() ?? "", a.GetValueOrDefault("content")?.ToString() ?? "").ToLower();
            if (keywords.Any(k => text.Contains(k))) return true;
        }
        return false;
    }

    private static bool CheckCompetingViewpoints(List<Dictionary<string, object?>> articles)
    {
        var keywords = new[] { "limitation", "challenge", "problem", "issue", "concern", "debate", "controversy", "comparison", "alternative", "vs", "versus", "trade-off", "drawback", "risk" };
        foreach (var a in articles)
        {
            var text = string.Join(" ", a.GetValueOrDefault("title")?.ToString() ?? "", a.GetValueOrDefault("summary")?.ToString() ?? "", a.GetValueOrDefault("content")?.ToString() ?? "").ToLower();
            if (keywords.Any(k => text.Contains(k))) return true;
        }
        return false;
    }

    private static double ScoreSourceQuality(Dictionary<string, int> sourceTypes, List<Dictionary<string, object?>> articles)
    {
        if (articles.Count == 0) return 0;
        double score = 0;
        if (sourceTypes.Keys.Any(t => t is "arxiv" or "research_paper")) score += 30;
        if (sourceTypes.Keys.Any(t => t is "arxiv" or "academic")) score += 20;
        if (sourceTypes.Count >= 3) score += 20; else if (sourceTypes.Count >= 2) score += 10;
        if (articles.Count >= 20) score += 30; else if (articles.Count >= 10) score += 20; else if (articles.Count >= 5) score += 10;
        return Math.Min(100, score);
    }

    private static double ScoreDepth(List<Dictionary<string, object?>> articles)
    {
        if (articles.Count == 0) return 0;
        var hasContent = articles.Count(a => (a.GetValueOrDefault("content")?.ToString()?.Length ?? 0) > 500);
        var contentRatio = hasContent / (double)articles.Count;
        var techKeywords = new[] { "architecture", "algorithm", "implementation", "performance", "evaluation", "methodology", "experiment", "result", "analysis", "comparison", "benchmark" };
        var hasDepth = articles.Count(a => techKeywords.Any(k => (a.GetValueOrDefault("content")?.ToString() ?? "").ToLower().Contains(k)));
        var depthRatio = hasDepth / (double)articles.Count;
        return contentRatio * 50 + depthRatio * 50;
    }

    private static double ScoreSpecificity(string topic)
    {
        var broad = new[] { "technology", "artificial intelligence", "machine learning", "computing", "software", "hardware", "internet", "data" };
        var lower = topic.ToLower();
        if (broad.Any(b => lower.Contains(b))) return 40;
        var specific = new[] { "reliability", "performance", "scalability", "security", "efficiency", "optimization", "comparison", "evaluation", "architecture", "design", "implementation", "deployment", "safety", "alignment", "reasoning", "planning", "memory" };
        if (specific.Any(s => lower.Contains(s))) return 80;
        return 60;
    }

    private static string? GenerateResearchQuestion(string topic, List<Dictionary<string, object?>> articles)
    {
        var lower = topic.ToLower();
        if (lower.Contains("reliability") || lower.Contains("safety") || lower.Contains("security"))
            return $"What failure modes most commonly affect {topic}, and how can they be mitigated?";
        if (lower.Contains("performance") || lower.Contains("scalability") || lower.Contains("efficiency"))
            return $"What factors most significantly impact {topic} performance in production environments?";
        return $"What are the key factors driving {topic} adoption and what limitations remain?";
    }
}