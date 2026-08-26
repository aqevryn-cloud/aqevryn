using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

public class EditorialReviewAgent
{
    private readonly ILogger<EditorialReviewAgent> _logger;
    private static readonly string[] FillerPhrases = { "in today's rapidly evolving technological landscape", "in today's fast-paced world", "as technology continues to evolve", "it's important to note", "delve into", "navigate the complexities", "unlock", "game-changer", "revolutionize the way", "the future of", "stay ahead of the curve" };

    public EditorialReviewAgent(ILogger<EditorialReviewAgent>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EditorialReviewAgent>.Instance;
    }

    public Task<EditorialReviewResult> ReviewAsync(GeneratedArticle article, double minScore = 70)
    {
        _logger.LogInformation("Reviewing article: {Title}", article.Title);

        var factual = EvaluateFactualAccuracy(article);
        var research = EvaluateResearchQuality(article);
        var originality = EvaluateOriginality(article);
        var technical = EvaluateTechnicalQuality(article);
        var writing = EvaluateWritingQuality(article);
        var seo = EvaluateSeo(article);

        var criticalIssues = new List<string>();
        if (article.References.Count == 0) criticalIssues.Add("Missing references — citations are required");
        if (string.IsNullOrEmpty(article.Slug)) criticalIssues.Add("Missing slug — required metadata");
        if (string.IsNullOrEmpty(article.Description)) criticalIssues.Add("Missing description — required metadata");
        if (factual < 40) criticalIssues.Add($"Factual accuracy is critically low ({factual:F0}/100)");
        if (originality < 50) criticalIssues.Add($"Originality is too low ({originality:F0}/100)");

        var warnings = new List<string>();
        if (factual < 60) warnings.Add($"Factual accuracy could be improved ({factual:F0}/100)");
        if (research < 60) warnings.Add($"Research quality could be improved ({research:F0}/100)");
        if (technical < 50) warnings.Add($"Technical depth could be improved ({technical:F0}/100)");
        if (writing < 50) warnings.Add($"Writing could be clearer ({writing:F0}/100)");

        var overall = factual * 0.25 + research * 0.25 + originality * 0.20 + technical * 0.10 + writing * 0.10 + seo * 0.10;
        var recommendation = criticalIssues.Count > 0 ? "REJECT" : overall < minScore ? "REVISE" : "APPROVE";

        return Task.FromResult(new EditorialReviewResult
        {
            OverallScore = Math.Round(overall, 1), FactualAccuracy = Math.Round(factual, 1), ResearchQuality = Math.Round(research, 1),
            Originality = Math.Round(originality, 1), TechnicalQuality = Math.Round(technical, 1), WritingQuality = Math.Round(writing, 1),
            SeoQuality = Math.Round(seo, 1), CriticalIssues = criticalIssues, Warnings = warnings, PublishRecommendation = recommendation,
        });
    }

    private static double EvaluateFactualAccuracy(GeneratedArticle a)
    {
        double score = 70;
        if (a.References.Count > 0) score += 15; else score -= 30;
        return Math.Clamp(score, 0, 100);
    }

    private static double EvaluateResearchQuality(GeneratedArticle a)
    {
        double score = 65;
        if (!string.IsNullOrEmpty(a.ResearchQuestion)) score += 10;
        if (a.Findings.Length > 100) score += 10;
        if (a.Limitations.Length > 50) score += 10;
        if (a.References.Count >= 5) score += 15; else if (a.References.Count >= 3) score += 10; else if (a.References.Count < 3) score -= 10;
        return Math.Clamp(score, 0, 100);
    }

    private static double EvaluateOriginality(GeneratedArticle a)
    {
        var content = GetFullText(a).ToLower();
        double score = 80;
        var fillerCount = FillerPhrases.Count(f => content.Contains(f));
        if (fillerCount > 0) score -= fillerCount * 5;
        return Math.Clamp(score, 0, 100);
    }

    private static double EvaluateTechnicalQuality(GeneratedArticle a)
    {
        double score = 75;
        var terms = new[] { "architecture", "algorithm", "framework", "implementation", "performance", "evaluation", "api", "protocol", "system", "model", "benchmark", "scalability" };
        var count = terms.Count(t => a.TechnicalAnalysis.ToLower().Contains(t));
        if (count >= 3) score += 10; else if (count == 0) score -= 15;
        return Math.Clamp(score, 0, 100);
    }

    private static double EvaluateWritingQuality(GeneratedArticle a)
    {
        var content = GetFullText(a);
        double score = 75;
        if (content.Length < 500) score -= 20; else if (content.Length < 1000) score -= 5;
        return Math.Clamp(score, 0, 100);
    }

    private static double EvaluateSeo(GeneratedArticle a)
    {
        double score = 70;
        if (a.Title.Length >= 30 && a.Title.Length <= 70) score += 10; else if (a.Title.Length < 20) score -= 5;
        if (a.Description.Length >= 50 && a.Description.Length <= 160) score += 10; else if (string.IsNullOrEmpty(a.Description)) score -= 20;
        if (!string.IsNullOrEmpty(a.Slug)) score += 5;
        return Math.Clamp(score, 0, 100);
    }

    private static string GetFullText(GeneratedArticle a) => string.Join(" ", a.Title, a.Introduction, a.WhyThisMatters, a.Background, a.ResearchQuestion, a.TechnicalAnalysis, a.Findings, a.MarketImplications, a.Limitations, a.FutureOutlook, a.Conclusion, a.Methodology);
}