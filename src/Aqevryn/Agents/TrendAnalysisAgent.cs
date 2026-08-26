using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

public class TrendAnalysisAgent
{
    private readonly ILogger<TrendAnalysisAgent> _logger;
    private static readonly double[] SignalWeights = { 0.20, 0.25, 0.20, 0.20, 0.15 };

    public TrendAnalysisAgent(ILogger<TrendAnalysisAgent>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TrendAnalysisAgent>.Instance;
    }

    public Task<TrendScoreResult> AnalyzeAsync(string topic, List<Dictionary<string, object?>> articles)
    {
        _logger.LogInformation("Analyzing trend for {Topic} based on {Count} articles", topic, articles.Count);
        if (articles.Count == 0)
        {
            return Task.FromResult(new TrendScoreResult { Topic = topic, Score = 0, Evidence = new() { "No articles found for this topic" } });
        }

        var mentionCount = articles.Count;
        var independentSources = articles.Select(a => a.GetValueOrDefault("source_name")?.ToString() ?? "").Where(s => !string.IsNullOrEmpty(s)).Distinct().Count();
        var recency = ComputeRecency(articles);
        var diversity = ComputeSourceDiversity(articles);

        var signals = new Dictionary<string, double>
        {
            ["mention_count"] = NormalizeMentionCount(mentionCount),
            ["mention_growth"] = 0,
            ["independent_sources"] = NormalizeSourceCount(independentSources),
            ["recency"] = recency * 100,
            ["source_diversity"] = diversity * 100,
        };

        var composite = signals["mention_count"] * SignalWeights[0]
            + signals["independent_sources"] * SignalWeights[2]
            + signals["recency"] * SignalWeights[3]
            + signals["source_diversity"] * SignalWeights[4];

        composite = Math.Clamp(composite, 0, 100);

        var result = new TrendScoreResult
        {
            Topic = topic,
            Score = Math.Round(composite, 1),
            MentionCount = mentionCount,
            IndependentSources = independentSources,
            RecencyScore = Math.Round(recency, 4),
            Signals = signals,
            Evidence = new()
            {
                $"{mentionCount} mentions across {independentSources} independent sources",
                $"Recency score: {recency:.P0}",
                $"Source diversity: {diversity:.F2}",
            },
        };

        return Task.FromResult(result);
    }

    private static double ComputeRecency(List<Dictionary<string, object?>> articles)
    {
        var now = DateTime.UtcNow.Ticks;
        var window = TimeSpan.FromDays(30).Ticks;
        double sum = 0;
        foreach (var a in articles)
        {
            if (a.GetValueOrDefault("published_at") is DateTime dt)
            {
                var age = Math.Max(0, now - dt.Ticks);
                sum += Math.Max(0, 1.0 - (age / (double)window));
            }
        }
        return articles.Count > 0 ? sum / articles.Count : 0;
    }

    private static double ComputeSourceDiversity(List<Dictionary<string, object?>> articles)
    {
        var sources = articles
            .Select(a => a.GetValueOrDefault("source_name")?.ToString() ?? "unknown")
            .GroupBy(s => s)
            .ToDictionary(g => g.Key, g => g.Count());

        var total = articles.Count;
        double entropy = 0;
        foreach (var count in sources.Values)
        {
            var p = count / (double)total;
            entropy -= p * Math.Log(p);
        }

        var maxEntropy = sources.Count > 1 ? Math.Log(sources.Count) : 1.0;
        return maxEntropy > 0 ? Math.Min(1.0, entropy / maxEntropy) : 0;
    }

    private static double NormalizeMentionCount(int count) => count <= 0 ? 0 : Math.Min(100, Math.Sqrt(count) * 10);
    private static double NormalizeSourceCount(int count) => count <= 0 ? 0 : count >= 20 ? 100 : count * 5.0;
    private static double NormalizeRecency(double recency) => recency * 100;
}