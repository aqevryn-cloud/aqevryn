using System.Xml.Linq;
using Aqevryn.Common;

namespace Aqevryn.Sources;

public class RssSourceAdapter : BaseSourceAdapter
{
    private readonly HttpClientHelper _http;

    public RssSourceAdapter(Dictionary<string, object> config, Aqevryn.Config.AqevrynSettings settings)
        : base(config, settings)
    {
        _http = new HttpClientHelper(settings.MaxRetries, settings.RequestTimeoutSeconds);
    }

    public override async Task<bool> ValidateAsync()
    {
        try
        {
            var xml = await _http.FetchStringAsync(GetString(Config, "url", ""));
            var doc = XDocument.Parse(xml);
            return doc.Root?.Descendants("item").Any() ?? false;
        }
        catch { return false; }
    }

    public override async Task<List<SourceItem>> FetchAsync()
    {
        var items = new List<SourceItem>();
        var url = GetString(Config, "url", "");

        try
        {
            var xml = await _http.FetchStringAsync(url);
            var doc = XDocument.Parse(xml);
            var entries = doc.Root?.Descendants("item").ToList() ?? new();

            foreach (var entry in entries)
            {
                var title = entry.Element("title")?.Value ?? "";
                var link = entry.Element("link")?.Value ?? "";
                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link)) continue;

                var pubDateStr = entry.Element("pubDate")?.Value ?? "";
                DateTime? pubDate = null;
                if (DateTime.TryParse(pubDateStr, out var dt)) pubDate = dt;

                var description = entry.Element("description")?.Value ?? "";
                var author = entry.Element("author")?.Value;
                var category = entry.Element("category")?.Value;

                var tags = new List<string>();
                if (!string.IsNullOrEmpty(category)) tags.Add(category);

                var item = new SourceItem
                {
                    Title = title,
                    Url = link,
                    CanonicalUrl = link,
                    SourceName = Name,
                    SourceType = "rss",
                    Author = author,
                    PublishedAt = pubDate,
                    Summary = description,
                    Content = description,
                    Tags = tags.Count > 0 ? tags : null,
                    Category = base.Category ?? category,
                    RetrievedAt = DateTime.UtcNow,
                };
                item.ComputeHash();
                items.Add(item);
            }
        }
        catch (Exception ex)
        {
            throw new SourceError($"RSS fetch failed for {Name}: {ex.Message}", ex);
        }

        return items;
    }
}