using Aqevryn.Common;

namespace Aqevryn.Research;

public class ResearchReport
{
    public string ResearchQuestion { get; set; } = "";
    public string ExecutiveSummary { get; set; } = "";
    public string Background { get; set; } = "";
    public List<string> KeyFindings { get; set; } = new();
    public List<string> TechnicalFindings { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
    public List<string> ConflictingEvidence { get; set; } = new();
    public List<string> Limitations { get; set; } = new();
    public List<string> OpenQuestions { get; set; } = new();
    public string Conclusion { get; set; } = "";
    public List<string> Sources { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string ToMarkdown()
    {
        var lines = new List<string>
        {
            "# Research Report", "",
            $"## Research Question\n\n{ResearchQuestion}\n",
            $"## Executive Summary\n\n{ExecutiveSummary}\n",
            $"## Background\n\n{Background}\n",
        };
        AddSection(lines, "Key Findings", KeyFindings);
        AddSection(lines, "Technical Findings", TechnicalFindings);
        AddSection(lines, "Evidence", Evidence);
        AddSection(lines, "Conflicting Evidence", ConflictingEvidence);
        AddSection(lines, "Limitations", Limitations);
        AddSection(lines, "Open Questions", OpenQuestions);
        lines.Add($"## Conclusion\n\n{Conclusion}\n");
        AddSection(lines, "Sources", Sources);
        return string.Join("\n", lines);
    }

    private static void AddSection(List<string> lines, string heading, List<string> items)
    {
        if (items.Count == 0) return;
        lines.Add($"## {heading}\n");
        foreach (var item in items) lines.Add($"- {item}");
        lines.Add("");
    }
}

public class ResearchReportGenerator
{
    public ResearchReport Generate(ResearchResult result, string topic)
    {
        var sources = result.SourcesAnalyzed.Select(s => $"{s.Title} ({s.SourceType}) — {s.Url}").ToList();
        var keyFindings = result.Findings.Select(f => $"{f.Claim} [{f.FindingType}, conf: {f.Confidence:P0}]").ToList();
        var technicalFindings = result.Findings.Where(f => f.FindingType is "verified_fact" or "research_finding")
            .Select(f => f.Claim).ToList();

        return new ResearchReport
        {
            ResearchQuestion = result.ResearchQuestion,
            ExecutiveSummary = $"This report analyzes {topic} based on {result.SourcesAnalyzed.Count} sources, yielding {result.Findings.Count} findings.",
            Background = $"Background research on {topic}.",
            KeyFindings = keyFindings,
            TechnicalFindings = technicalFindings,
            Evidence = keyFindings,
            ConflictingEvidence = result.KnowledgeGaps,
            Limitations = result.KnowledgeGaps.Count > 0 ? result.KnowledgeGaps : new() { "No significant limitations identified." },
            OpenQuestions = new() { "Further primary research needed to validate findings." },
            Conclusion = result.Conclusions.Count > 0 ? string.Join("\n", result.Conclusions) : "Insufficient evidence for conclusions.",
            Sources = sources,
        };
    }
}