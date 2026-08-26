using Aqevryn.Common;

namespace Aqevryn.Agents;

public class TopicRanker
{
    private readonly double _trendWeight;
    private readonly double _researchabilityWeight;
    private readonly double _marketWeight;
    private readonly double _noveltyWeight;
    private readonly double _technicalWeight;
    private readonly double _minResearchability;
    private readonly int _minEvidence;

    public TopicRanker(double trendWeight = 0.25, double researchabilityWeight = 0.25,
        double marketWeight = 0.20, double noveltyWeight = 0.15, double technicalWeight = 0.15,
        double minResearchability = 30, int minEvidence = 3)
    {
        _trendWeight = trendWeight; _researchabilityWeight = researchabilityWeight;
        _marketWeight = marketWeight; _noveltyWeight = noveltyWeight; _technicalWeight = technicalWeight;
        _minResearchability = minResearchability; _minEvidence = minEvidence;
    }

    public List<RankedTopic> Rank(List<Dictionary<string, object?>> topics,
        Dictionary<string, double>? trendScores = null,
        Dictionary<string, double>? researchabilityScores = null,
        Dictionary<string, double>? marketScores = null,
        Dictionary<string, int>? articleCounts = null)
    {
        if (topics.Count == 0) return new();

        trendScores ??= new(); researchabilityScores ??= new(); marketScores ??= new(); articleCounts ??= new();
        var ranked = new List<RankedTopic>();

        foreach (var topic in topics)
        {
            var topicName = topic.GetValueOrDefault("topic")?.ToString() ?? "";
            if (string.IsNullOrEmpty(topicName)) continue;

            var ts = trendScores.GetValueOrDefault(topicName, 0);
            var rs = researchabilityScores.GetValueOrDefault(topicName, 0);
            var ms = marketScores.GetValueOrDefault(topicName, 0);
            var ac = articleCounts.GetValueOrDefault(topicName, 0);

            var ns = Math.Max(0, 100 - ts * 0.5);
            var techSig = rs * 0.6 + ms * 0.4;
            var finalScore = ts * _trendWeight + rs * _researchabilityWeight + ms * _marketWeight
                + ns * _noveltyWeight + techSig * _technicalWeight;
            finalScore = Math.Clamp(finalScore, 0, 100);

            var decision = "SELECTED";
            string? rejectionReason = null;

            if (rs < _minResearchability)
            {
                decision = "REJECTED";
                rejectionReason = $"Researchability score ({rs:F1}) below minimum threshold ({_minResearchability}).";
            }
            else if (ac < _minEvidence)
            {
                decision = "REJECTED";
                rejectionReason = $"Insufficient evidence ({ac} articles, minimum {_minEvidence}).";
            }

            ranked.Add(new RankedTopic
            {
                Topic = topicName, TrendScore = Math.Round(ts, 1), ResearchabilityScore = Math.Round(rs, 1),
                MarketViabilityScore = Math.Round(ms, 1), NoveltyScore = Math.Round(ns, 1),
                TechnicalSignificance = Math.Round(techSig, 1), FinalScore = Math.Round(finalScore, 1),
                Decision = decision, RejectionReason = rejectionReason, ArticleCount = ac,
                Category = topic.GetValueOrDefault("category")?.ToString(),
            });
        }

        return ranked.OrderByDescending(r => r.FinalScore).ToList();
    }

    public List<RankedTopic> SelectTopN(List<RankedTopic> ranked, int n = 1)
    {
        return ranked.Where(r => r.Decision == "SELECTED").Take(n).ToList();
    }

    public string ExplainRejection(RankedTopic topic)
    {
        if (topic.Decision != "REJECTED")
            return $"Topic '{topic.Topic}' was selected (score: {topic.FinalScore:F1}).";
        return $"Topic '{topic.Topic}' was REJECTED. {topic.RejectionReason}";
    }
}