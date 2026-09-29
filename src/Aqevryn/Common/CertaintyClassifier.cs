namespace Aqevryn.Common;

/// <summary>
/// Certainty classification system per soul.md §12.
/// Maps confidence scores to descriptive badges.
/// </summary>
public static class CertaintyClassifier
{
    public static CertaintyLevel Classify(double confidence, string findingType)
    {
        if (findingType == "verified_fact") return new CertaintyLevel("Established", "🟢", 0.9);
        if (confidence >= 0.85 && findingType == "research_finding")
            return new CertaintyLevel("Strongly Supported", "🟢", confidence);
        if (confidence >= 0.70) return new CertaintyLevel("Supported", "🔵", confidence);
        if (confidence >= 0.50) return new CertaintyLevel("Mixed Evidence", "🟡", confidence);
        if (confidence >= 0.30) return new CertaintyLevel("Weakly Supported", "🟠", confidence);
        return new CertaintyLevel("Speculative", "🔴", confidence);
    }

    public static string GetBadgeHtml(CertaintyLevel level)
    {
        return $"<span style=\"background:{level.Color};color:white;padding:2px 8px;border-radius:10px;font-size:0.8em;\">{level.Icon} {level.Label}</span>";
    }

    /// <summary>Get a certainty summary for a list of findings.</summary>
    public static CertaintySummary GetSummary(List<ResearchFinding> findings)
    {
        if (findings == null || findings.Count == 0)
            return new CertaintySummary { Overall = "Unknown", FindingsBreakdown = new() };

        var breakdown = findings.Select(f => new FindingCertainty
        {
            Claim = f.Claim,
            Level = Classify(f.Confidence, f.FindingType),
        }).ToList();

        var avgConfidence = findings.Average(f => f.Confidence);
        var overall = avgConfidence >= 0.85 ? "Strongly Supported" :
                      avgConfidence >= 0.70 ? "Supported" :
                      avgConfidence >= 0.50 ? "Mixed Evidence" :
                      avgConfidence >= 0.30 ? "Weakly Supported" : "Speculative";

        return new CertaintySummary
        {
            Overall = overall,
            AverageConfidence = avgConfidence,
            Established = findings.Count(f => f.Confidence >= 0.85),
            Supported = findings.Count(f => f.Confidence >= 0.70 && f.Confidence < 0.85),
            Mixed = findings.Count(f => f.Confidence >= 0.50 && f.Confidence < 0.70),
            Weak = findings.Count(f => f.Confidence < 0.50),
            FindingsBreakdown = breakdown,
        };
    }
}

public record CertaintyLevel(string Label, string Icon, double Confidence)
{
    public string Color => Label switch
    {
        "Established" => "#1a7f37",
        "Strongly Supported" => "#1a7f37",
        "Supported" => "#0969da",
        "Mixed Evidence" => "#d4a72c",
        "Weakly Supported" => "#e16f24",
        "Speculative" => "#cf222e",
        _ => "#656d76",
    };
}

public class CertaintySummary
{
    public string Overall { get; set; } = "";
    public double AverageConfidence { get; set; }
    public int Established { get; set; }
    public int Supported { get; set; }
    public int Mixed { get; set; }
    public int Weak { get; set; }
    public List<FindingCertainty> FindingsBreakdown { get; set; } = new();
}

public class FindingCertainty
{
    public string Claim { get; set; } = "";
    public CertaintyLevel Level { get; set; } = new("Unknown", "❓", 0);
}