using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aqevryn.Api;

/// <summary>
/// Generates HTML pages for every completed research paper for GitHub Pages.
/// Creates per-paper pages at docs/research/<slug>.html
/// Also updates the completed_research.json in the repo root.
/// </summary>
public class ResearchPaperSite
{
    private readonly string _outputDir;

    public ResearchPaperSite(string outputDir = "docs")
    {
        _outputDir = outputDir;
    }

    public void Generate()
    {
        Directory.CreateDirectory(Path.Combine(_outputDir, "research"));
        Directory.CreateDirectory(Path.Combine(_outputDir, "articles"));

        var completed = Common.CompletedResearchRegistry.GetAll();
        var papers = new List<object>();

        foreach (var r in completed)
        {
            var slug = Slugify(r.Topic);
            var articlePath = Path.Combine(AppContext.BaseDirectory, "webdata", "articles", $"{slug}.json");
            var hasArticle = File.Exists(articlePath);

            papers.Add(new
            {
                r.Topic,
                r.ResearchQuestion,
                r.FinalScore,
                r.EditorialScore,
                r.ArticleTitle,
                r.ArticleCount,
                r.FindingCount,
                r.PrUrl,
                Slug = slug,
                r.CompletedAt,
                HasArticle = hasArticle,
                ArticleUrl = hasArticle ? $"/articles/{slug}.html" : null,
            });

            // Generate the research paper page
            if (hasArticle)
            {
                var json = File.ReadAllText(articlePath);
                GenerateArticlePage(slug, json);
            }
            else
            {
                GenerateMetadataPage(r, slug);
            }
        }

        // Update completed_research.json in the output
        var fullJson = JsonSerializer.Serialize(papers, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(_outputDir, "completed_research.json"), fullJson);

        Console.WriteLine($"📄 Generated {completed.Count} research paper pages in {_outputDir}/research/");
    }

    private void GenerateArticlePage(string slug, string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var title = GetString(root, "title") ?? "Research Paper";
            var description = GetString(root, "description") ?? "";
            var introduction = GetString(root, "introduction") ?? "";
            var whyThisMatters = GetString(root, "whyThisMatters") ?? "";
            var background = GetString(root, "background") ?? "";
            var researchQuestion = GetString(root, "researchQuestion") ?? "";
            var technicalAnalysis = GetString(root, "technicalAnalysis") ?? "";
            var findings = GetString(root, "findings") ?? "";
            var marketImpl = GetString(root, "marketImplications") ?? "";
            var limitations = GetString(root, "limitations") ?? "";
            var futureOutlook = GetString(root, "futureOutlook") ?? "";
            var conclusion = GetString(root, "conclusion") ?? "";
            var methodology = GetString(root, "methodology") ?? "";
            var editorialScore = root.TryGetProperty("editorialScore", out var es) ? es.GetDouble() : 0;
            var articleCount = root.TryGetProperty("articleCount", out var ac) ? ac.GetInt32() : 0;
            var findingCount = root.TryGetProperty("findingCount", out var fc) ? fc.GetInt32() : 0;
            var completedAt = GetString(root, "completedAt") ?? "";

            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>{title} — Aqevryn Research</title>
<link rel=""stylesheet"" href=""/style.css"">
<style>
*{{margin:0;padding:0;box-sizing:border-box}}
body{{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif;font-size:16px;line-height:1.6;color:#1f2328;max-width:800px;margin:0 auto;padding:20px}}
header{{border-bottom:1px solid #d0d7de;padding:20px 0;margin-bottom:20px}}
header h1{{font-size:1.4em}}
header nav a{{color:#0969da;text-decoration:none;margin-right:16px}}
h1{{font-size:1.8em;margin-bottom:8px}}
.meta{{color:#656d76;font-size:0.9em;margin-bottom:24px}}
.meta span{{margin-right:16px}}
h2{{font-size:1.4em;margin:24px 0 12px;border-bottom:1px solid #d0d7de;padding-bottom:8px}}
h3{{font-size:1.2em;margin:16px 0 8px}}
p{{margin-bottom:16px;line-height:1.7}}
pre{{background:#f6f8fa;padding:16px;border-radius:6px;overflow-x:auto}}
footer{{border-top:1px solid #d0d7de;padding:20px 0;margin-top:40px;color:#656d76;font-size:0.9em}}
@media(max-width:600px){{body{{padding:12px}}h1{{font-size:1.4em}}}}
</style>
</head>
<body>
<header>
<h1><a href=""/"" style=""color:inherit;text-decoration:none"">Aqevryn Research</a></h1>
<nav><a href=""/"">Home</a><a href=""/research"">Research</a><a href=""/about"">About</a></nav>
</header>
<main>
<article>
<h1>{title}</h1>
<div class=""meta"">
<span>Editorial Score: {editorialScore}/100</span>
<span>{articleCount} sources</span>
<span>{findingCount} findings</span>
<span>{completedAt}</span>
</div>
<p><em>{description}</em></p>
{(researchQuestion != "" ? $"<h2>Research Question</h2><p>{researchQuestion}</p>" : "")}
{(introduction != "" ? $"<h2>Introduction</h2><p>{introduction}</p>" : "")}
{(whyThisMatters != "" ? $"<h2>Why This Matters</h2><p>{whyThisMatters}</p>" : "")}
{(background != "" ? $"<h2>Background</h2><p>{background}</p>" : "")}
{(technicalAnalysis != "" ? $"<h2>Technical Analysis</h2><p>{technicalAnalysis}</p>" : "")}
{(findings != "" ? $"<h2>Key Findings</h2><p>{findings}</p>" : "")}
{(marketImpl != "" ? $"<h2>Market Implications</h2><p>{marketImpl}</p>" : "")}
{(limitations != "" ? $"<h2>Limitations</h2><p>{limitations}</p>" : "")}
{(futureOutlook != "" ? $"<h2>Future Outlook</h2><p>{futureOutlook}</p>" : "")}
{(conclusion != "" ? $"<h2>Conclusion</h2><p>{conclusion}</p>" : "")}
{(methodology != "" ? $"<h2>Methodology</h2><p>{methodology}</p>" : "")}
</article>
</main>
<footer><p>Generated by <a href=""https://github.com/aqevryn-cloud/aqevryn"">Aqevryn</a></p></footer>
</body>
</html>";

            File.WriteAllText(Path.Combine(_outputDir, "research", $"{slug}.html"), html);
        }
        catch { }
    }

    private void GenerateMetadataPage(Common.CompletedResearch r, string slug)
    {
        var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>{r.Topic} — Aqevryn Research</title>
<link rel=""stylesheet"" href=""/style.css"">
</head>
<body>
<header><h1><a href=""/"" style=""color:inherit;text-decoration:none"">Aqevryn Research</a></h1>
<nav><a href=""/"">Home</a><a href=""/research"">Research</a><a href=""/about"">About</a></nav></header>
<main>
<h1>{r.Topic}</h1>
<div class=""meta"">
<span>Score: {r.EditorialScore}/100</span>
<span>{r.ArticleCount} sources</span>
<span>{r.FindingCount} findings</span>
</div>
<p>{r.ResearchQuestion}</p>
{(r.ArticleTitle != null ? $"<p><em>{r.ArticleTitle}</em></p>" : "")}
{(r.PrUrl != null ? "<p><a href=\"" + r.PrUrl + "\">View on GitHub</a></p>" : "")}
<p><em>Full article content not yet available. Run the pipeline to generate the complete paper.</em></p>
</main>
<footer><p>Generated by Aqevryn</p></footer>
</body>
</html>";

        File.WriteAllText(Path.Combine(_outputDir, "research", $"{slug}.html"), html);
    }

    private static string Slugify(string text)
    {
        var slug = text.ToLower().Replace(" ", "-").Replace("/", "-");
        slug = Regex.Replace(slug, @"[^a-z0-9-]", "");
        return slug.Trim('-')[..Math.Min(slug.Length, 60)];
    }

    private static string? GetString(JsonElement el, string key)
    {
        return el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    }
}