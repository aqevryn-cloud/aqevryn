using System.Text.RegularExpressions;

namespace Aqevryn.Publishing;

public class SiteGenerator
{
    private readonly string _outputDir;
    private readonly string _articlesDir;

    public SiteGenerator(string outputDir = "website", string articlesDir = "articles/published")
    {
        _outputDir = outputDir;
        _articlesDir = articlesDir;
    }

    public void Generate()
    {
        Directory.CreateDirectory(_outputDir);
        Directory.CreateDirectory(Path.Combine(_outputDir, "articles"));

        var articles = CollectArticles();
        GenerateHomePage(articles);
        GenerateAboutPage();
        GenerateMethodologyPage();
        GenerateCss();

        foreach (var article in articles)
            GenerateArticlePage(article);

        Console.WriteLine($"Site generated: {_outputDir}/ — {articles.Count} articles");
    }

    private List<ArticleInfo> CollectArticles()
    {
        var articles = new List<ArticleInfo>();
        if (!Directory.Exists(_articlesDir)) return articles;

        foreach (var file in Directory.GetFiles(_articlesDir, "*.md").OrderByDescending(f => f))
        {
            try
            {
                var content = File.ReadAllText(file);
                var article = ParseArticle(content, Path.GetFileName(file));
                if (article != null) articles.Add(article);
            }
            catch { continue; }
        }
        return articles;
    }

    private ArticleInfo? ParseArticle(string content, string filename)
    {
        var title = filename.Replace(".md", "").Replace("-", " ");

        // Extract front matter title
        var match = Regex.Match(content, @"^---\s*\n.*?title:\s*""?(.+?)""?\n", RegexOptions.Singleline);
        if (match.Success) title = match.Groups[1].Value;

        // Extract description
        var descMatch = Regex.Match(content, @"description:\s*""?(.+?)""?\n");
        var description = descMatch.Success ? descMatch.Groups[1].Value : "";

        // Extract date from filename (YYYY-MM-DD-*)
        var date = "";
        var dateMatch = Regex.Match(filename, @"^(\d{4}-\d{2}-\d{2})");
        if (dateMatch.Success) date = dateMatch.Groups[1].Value;

        // Remove front matter for body
        var body = content;
        if (content.StartsWith("---"))
        {
            var idx = content.IndexOf("---", 3);
            if (idx > 0) body = content[(idx + 3)..].Trim();
        }

        // Create slug
        var slug = filename.Replace(".md", "");
        if (dateMatch.Success) slug = slug[11..]; // Remove date prefix

        return new ArticleInfo
        {
            Title = title, Description = description, Date = date,
            Slug = slug, Body = body, Filename = filename
        };
    }

    public void GenerateHomePage(List<ArticleInfo> articles)
    {
        var articlesHtml = string.Join("\n", articles.Select(a => $@"
        <article class=""article-card"">
            <time datetime=""{a.Date}"">{a.Date}</time>
            <h2><a href=""/articles/{a.Slug}.html"">{a.Title}</a></h2>
            <p>{a.Description}</p>
        </article>"));

        var html = RenderPage("Aqevryn Research", "Autonomous technology research and analysis", $@"
        <section class=""hero"">
            <h1>Aqevryn Research</h1>
            <p>Autonomous technology intelligence, research, and analysis.</p>
        </section>
        <section class=""articles"">
            <h2>Latest Research</h2>
            {(articles.Count > 0 ? articlesHtml : "<p>No articles published yet.</p>")}
        </section>");

        File.WriteAllText(Path.Combine(_outputDir, "index.html"), html);
    }

    public void GenerateArticlePage(ArticleInfo article)
    {
        var bodyHtml = MarkdownToHtml(article.Body);
        var html = RenderPage($"{article.Title} — Aqevryn Research", article.Description, $@"
        <article class=""research-article"">
            <header>
                <h1>{article.Title}</h1>
                <time datetime=""{article.Date}"">{article.Date}</time>
                <p class=""description"">{article.Description}</p>
            </header>
            <div class=""article-body"">{bodyHtml}</div>
        </article>");

        File.WriteAllText(Path.Combine(_outputDir, "articles", $"{article.Slug}.html"), html);
    }

    public void GenerateAboutPage()
    {
        var html = RenderPage("About — Aqevryn Research", "About Aqevryn", @"
        <section class=""about"">
            <h1>About Aqevryn</h1>
            <p>Aqevryn is an autonomous technology research and publishing system.</p>
            <p>It continuously discovers emerging technology developments, evaluates their significance, conducts evidence-based research, and publishes original findings.</p>
            <p>The system prioritizes research quality over publication volume.</p>
            <h2>Architecture</h2>
            <ul>
                <li>Source collection from RSS, arXiv, GitHub, Hacker News, and Reddit</li>
                <li>Topic discovery and trend analysis</li>
                <li>Evidence-based deep research</li>
                <li>Editorial review with quality gates</li>
                <li>GitHub Pull Request workflow for human approval</li>
            </ul>
        </section>");
        File.WriteAllText(Path.Combine(_outputDir, "about.html"), html);
    }

    public void GenerateMethodologyPage()
    {
        var html = RenderPage("Methodology — Aqevryn Research", "Research methodology", @"
        <section class=""methodology"">
            <h1>Research Methodology</h1>
            <h2>How Topics Are Discovered</h2>
            <p>Topics are discovered through keyword-based extraction from collected articles across 16 technology categories.</p>
            <h2>How Sources Are Evaluated</h2>
            <p>Sources are classified by type: research papers (1.0), official documentation (0.95), engineering blogs (0.90), reputable publications (0.85), industry reports (0.80), community sources (0.55).</p>
            <h2>How Topics Are Scored</h2>
            <p>Topics are scored across five dimensions: Trend Score (25%), Researchability (25%), Market Viability (20%), Novelty (15%), Technical Significance (15%).</p>
            <h2>How Research Is Performed</h2>
            <p>Research uses a multi-stage pipeline: planning, source retrieval, evidence extraction, contradiction detection, and conclusion formation.</p>
            <h2>How Citations Are Validated</h2>
            <p>Every claim is validated against its source. The system checks for source URL validity and detects potentially fabricated statistics.</p>
            <h2>How Articles Are Reviewed</h2>
            <p>Articles are scored across six dimensions: factual accuracy, research quality, originality, technical quality, writing quality, and SEO. Critical issues block publication.</p>
            <h2>Limitations</h2>
            <ul>
                <li>Aqevryn is an AI research assistant, not a human researcher</li>
                <li>Claims should be verified against original sources</li>
                <li>The technology landscape evolves rapidly</li>
            </ul>
        </section>");
        File.WriteAllText(Path.Combine(_outputDir, "methodology.html"), html);
    }

    public void GenerateCss()
    {
        var css = @"
* { margin: 0; padding: 0; box-sizing: border-box; }
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 16px; line-height: 1.6; color: #1f2328; max-width: 800px; margin: 0 auto; padding: 20px; }
header { border-bottom: 1px solid #d0d7de; padding: 20px 0; margin-bottom: 20px; }
header nav a { color: #0969da; text-decoration: none; margin-right: 16px; }
header nav a:hover { text-decoration: underline; }
h1 { font-size: 1.8em; margin-bottom: 16px; }
h2 { font-size: 1.4em; margin: 24px 0 12px; }
.hero { padding: 40px 0; text-align: center; }
.hero h1 { font-size: 2.4em; }
.hero p { font-size: 1.2em; color: #656d76; }
.article-card { padding: 20px; border: 1px solid #d0d7de; border-radius: 6px; margin-bottom: 16px; }
.article-card time { color: #656d76; font-size: 0.9em; }
.article-card h2 { margin: 8px 0; }
.article-card h2 a { color: #1f2328; text-decoration: none; }
.article-card h2 a:hover { color: #0969da; }
.article-card p { color: #656d76; }
.research-article .article-body { margin-top: 24px; }
.research-article .article-body h2 { border-bottom: 1px solid #d0d7de; padding-bottom: 8px; }
.research-article .article-body ul { margin-bottom: 16px; padding-left: 24px; }
.research-article .article-body code { background: #f6f8fa; padding: 2px 6px; border-radius: 3px; }
.research-article .article-body pre { background: #f6f8fa; padding: 16px; border-radius: 6px; overflow-x: auto; }
footer { border-top: 1px solid #d0d7de; padding: 20px 0; margin-top: 40px; color: #656d76; font-size: 0.9em; }
@media (max-width: 600px) { body { padding: 12px; } h1 { font-size: 1.4em; } }";
        File.WriteAllText(Path.Combine(_outputDir, "style.css"), css);
    }

    private string RenderPage(string title, string description, string content)
    {
        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
    <meta name=""description"" content=""{description}"">
    <link rel=""stylesheet"" href=""/style.css"">
</head>
<body>
    <header>
        <nav>
            <a href=""/"">Home</a>
            <a href=""/about.html"">About</a>
            <a href=""/methodology.html"">Methodology</a>
        </nav>
    </header>
    <main>{content}</main>
    <footer>
        <p>Generated by <a href=""https://github.com/aqevryn"">Aqevryn</a> — Autonomous Technology Research</p>
    </footer>
</body>
</html>";
    }

    private static string MarkdownToHtml(string md)
    {
        var html = md;
        html = Regex.Replace(html, @"^### (.+)$", "<h3>$1</h3>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^## (.+)$", "<h2>$1</h2>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"^# (.+)$", "<h1>$1</h1>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        html = Regex.Replace(html, @"_(.+?)_", "<em>$1</em>");
        html = Regex.Replace(html, @"\[(.+?)\]\((.+?)\)", "<a href=\"$2\">$1</a>");
        html = Regex.Replace(html, @"```(\w*)\n(.*?)```", "<pre><code>$2</code></pre>", RegexOptions.Singleline);
        html = Regex.Replace(html, @"`(.+?)`", "<code>$1</code>");
        html = Regex.Replace(html, @"^- (.+)$", "<li>$1</li>", RegexOptions.Multiline);
        html = Regex.Replace(html, @"(<li>.*</li>\n)+", m => $"<ul>{m.Value}</ul>");
        html = "<p>" + Regex.Replace(html, @"\n\n", "</p><p>") + "</p>";
        return html;
    }

    public class ArticleInfo
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Date { get; set; } = "";
        public string Slug { get; set; } = "";
        public string Body { get; set; } = "";
        public string Filename { get; set; } = "";
    }
}