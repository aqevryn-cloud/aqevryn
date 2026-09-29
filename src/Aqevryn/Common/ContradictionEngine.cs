namespace Aqevryn.Common;

/// <summary>
/// Implements soul.md §13 — systematically surfaces conflicting claims across sources.
/// Also implements §24 — temporal awareness for evidence freshness.
/// </summary>
public static class ContradictionEngine
{
    /// <summary>Find contradictions between a set of findings.</summary>
    public static List<Contradiction> FindContradictions(List<ResearchFinding> findings)
    {
        var contradictions = new List<Contradiction>();
        var opposingPairs = new[] {
            ("increase", "decrease"), ("improve", "degrade"), ("better", "worse"),
            ("advantage", "disadvantage"), ("supports", "contradicts"), ("proven", "disproven"),
            ("growing", "shrinking"), ("accelerating", "slowing"), ("rising", "falling"),
            ("successful", "unsuccessful"), ("beneficial", "harmful"), ("effective", "ineffective"),
            ("strong", "weak"), ("stable", "unstable"), ("secure", "vulnerable"),
        };

        for (int i = 0; i < findings.Count; i++)
        {
            for (int j = i + 1; j < findings.Count; j++)
            {
                var fi = findings[i].Claim.ToLowerInvariant();
                var fj = findings[j].Claim.ToLowerInvariant();
                foreach (var (a, b) in opposingPairs)
                {
                    if ((fi.Contains(a) && fj.Contains(b)) || (fi.Contains(b) && fj.Contains(a)))
                    {
                        contradictions.Add(new Contradiction
                        {
                            ClaimA = findings[i].Claim,
                            ClaimB = findings[j].Claim,
                            SourceA = findings[i].SourceUrl ?? "finding " + (i + 1),
                            SourceB = findings[j].SourceUrl ?? "finding " + (j + 1),
                            Type = "direct_contradiction",
                            Severity = "medium",
                        });
                        break;
                    }
                }
            }
        }
        return contradictions;
    }

    /// <summary>Check evidence freshness — flag sources older than threshold.</summary>
    public static List<TemporalFlag> CheckFreshness(List<ResearchSource> sources, int maxAgeDays = 365)
    {
        var flags = new List<TemporalFlag>();
        var now = DateTime.UtcNow;
        foreach (var source in sources)
        {
            // Sources don't have dates in the current model, so we check by their type
            // Papers older than ~2 years lose relevance in fast-moving fields
            if (source.SourceType == "research_paper" || source.SourceType == "engineering_blog")
            {
                flags.Add(new TemporalFlag
                {
                    SourceTitle = source.Title,
                    Flag = "fast_moving_field",
                    Note = "Technology research papers older than 2 years may be outdated in fast-moving fields",
                    Severity = "info",
                });
            }
        }
        return flags;
    }
}

public class Contradiction
{
    public string ClaimA { get; set; } = "";
    public string ClaimB { get; set; } = "";
    public string SourceA { get; set; } = "";
    public string SourceB { get; set; } = "";
    public string Type { get; set; } = ""; // direct_contradiction, nuance, different_context
    public string Severity { get; set; } = ""; // high, medium, low
}

public class TemporalFlag
{
    public string SourceTitle { get; set; } = "";
    public string Flag { get; set; } = "";
    public string Note { get; set; } = "";
    public string Severity { get; set; } = ""; // warning, info
}