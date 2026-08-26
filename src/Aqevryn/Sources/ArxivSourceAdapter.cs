using System.Xml.Linq;
using Aqevryn.Common;

namespace Aqevryn.Sources;

public class ArxivSourceAdapter : BaseSourceAdapter
{
    private readonly HttpClientHelper _http;
    private const string ArxivApiUrl = "http://export.arxiv.org/api/query";

    public ArxivSourceAdapter(Dictionary<string, object> config, Aqevryn.Config.AqevrynSettings settings)
        : base(config, settings)
    {
        _http = new HttpClientHelper(settings.MaxRetries, settings.RequestTimeoutSeconds);
    }

    public override async Task<bool> ValidateAsync()
    {
        try
        {
            await _http.FetchStringAsync($"{ArxivApiUrl}?search_query=all:test&max_results=1");
            return true;
        }
        catch { return false; }
    }

    public override async Task<List<SourceItem>> FetchAsync()
    {
        var items = new List<SourceItem>();
        var categories = Config.TryGetValue("categories", out var cats) && cats is List<object> catList
            ? catList.Select(c => c?.ToString() ?? "").Where(c => !string.IsNullOrEmpty(c)).ToList()
            : new List<string> { "cs.AI", "cs.LG", "cs.CL" };
        var maxResults = GetInt(Config, "max_results", 50);

        var queryParts = categories.Select(c => $"cat:{c}");
        var searchQuery = string.Join("+OR+", queryParts);
        var url = $"{ArxivApiUrl}?search_query=({searchQuery})&sortBy=submittedDate&sortOrder=descending&max_results={maxResults}";

        try
        {
            var xml = await _http.FetchStringAsync(url);
            var doc = XDocument.Parse(xml);
            XNamespace ns = "http://www.w3.org/2005/Atom";
            XNamespace arxivNs = "http://arxiv.org/schemas/atom";

            foreach (var entry in doc.Descendants(ns + "entry"))
            {
                var title = entry.Element(ns + "title")?.Value?.Trim().Replace("\n", " ") ?? "";
                var id = entry.Element(ns + "id")?.Value?.Trim() ?? "";
                if (string.IsNullOrEmpty(title)) continue;

                var summary = entry.Element(ns + "summary")?.Value?.Trim() ?? "";
                var published = entry.Element(ns + "published")?.Value ?? "";
                DateTime? pubDate = DateTime.TryParse(published, out var dt) ? dt : null;

                var authors = entry.Descendants(ns + "author")
                    .Select(a => a.Element(ns + "name")?.Value)
                    .Where(a => a != null)
                    .ToList();
                var author = authors.FirstOrDefault();

                var tags = entry.Descendants(ns + "category")
                    .Select(c => c.Attribute("term")?.Value)
                    .Where(c => c != null)
                    .Cast<string>()
                    .ToList();

                var item = new SourceItem
                {
                    Title = title,
                    Url = id,
                    CanonicalUrl = id,
                    SourceName = Name,
                    SourceType = "arxiv",
                    Author = author,
                    PublishedAt = pubDate,
                    Summary = summary.Length > 2000 ? summary[..2000] : summary,
                    Content = summary,
                    Tags = tags.Count > 0 ? tags : null,
                    Category = base.Category ?? "artificial_intelligence",
                    RetrievedAt = DateTime.UtcNow,
                };
                item.ComputeHash();
                items.Add(item);
            }
        }
        catch (Exception ex)
        {
            throw new SourceError($"arXiv fetch failed for {Name}: {ex.Message}", ex);
        }

        return items;
    }
}