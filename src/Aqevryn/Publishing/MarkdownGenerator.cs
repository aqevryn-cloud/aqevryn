using Aqevryn.Common;
using System.Text.Json;

namespace Aqevryn.Publishing;

public class MarkdownGenerator
{
    public string Generate(GeneratedArticle article, Dictionary<string, double>? scores = null)
    {
        var fm = new Dictionary<string, object>
        {
            ["title"] = article.Title,
            ["description"] = article.Description,
            ["date"] = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            ["categories"] = new[] { "technology" },
            ["tags"] = new[] { article.Slug },
            ["author"] = "Aqevryn Research",
            ["status"] = "draft",
        };

        if (scores != null)
        {
            fm["research_score"] = scores.GetValueOrDefault("editorial", 0);
            fm["trend_score"] = scores.GetValueOrDefault("trend", 0);
        }

        var frontMatter = "---\n";
        foreach (var (key, value) in fm)
        {
            if (value is string s) frontMatter += $"{key}: \"{s}\"\n";
            else if (value is double d) frontMatter += $"{key}: {d}\n";
            else if (value is Array arr) frontMatter += $"{key}:\n  - {string.Join("\n  - ", arr)}\n";
        }
        frontMatter += "---\n\n";

        var body = BuildBody(article);
        return frontMatter + body;
    }

    private static string BuildBody(GeneratedArticle a)
    {
        var sections = new List<string>();
        if (!string.IsNullOrEmpty(a.Introduction)) sections.Add($"## Introduction\n\n{a.Introduction}");
        if (!string.IsNullOrEmpty(a.WhyThisMatters)) sections.Add($"## Why This Matters\n\n{a.WhyThisMatters}");
        if (!string.IsNullOrEmpty(a.Background)) sections.Add($"## Background\n\n{a.Background}");
        if (!string.IsNullOrEmpty(a.ResearchQuestion)) sections.Add($"## Research Question\n\n{a.ResearchQuestion}");
        if (!string.IsNullOrEmpty(a.TechnicalAnalysis)) sections.Add($"## Technical Analysis\n\n{a.TechnicalAnalysis}");
        if (!string.IsNullOrEmpty(a.Findings)) sections.Add($"## Research Findings\n\n{a.Findings}");
        if (!string.IsNullOrEmpty(a.MarketImplications)) sections.Add($"## Market Implications\n\n{a.MarketImplications}");
        if (!string.IsNullOrEmpty(a.Limitations)) sections.Add($"## Limitations\n\n{a.Limitations}");
        if (!string.IsNullOrEmpty(a.FutureOutlook)) sections.Add($"## Future Outlook\n\n{a.FutureOutlook}");
        if (!string.IsNullOrEmpty(a.Conclusion)) sections.Add($"## Conclusion\n\n{a.Conclusion}");
        if (a.References.Count > 0) sections.Add($"## References\n\n{string.Join("\n", a.References.Select(r => $"- {r}"))}");
        if (!string.IsNullOrEmpty(a.Methodology)) sections.Add($"## Methodology\n\n{a.Methodology}");
        return string.Join("\n\n", sections);
    }

    public string GetFilename(string slug) => $"{DateTime.UtcNow:yyyy-MM-dd}-{slug}.md";
}