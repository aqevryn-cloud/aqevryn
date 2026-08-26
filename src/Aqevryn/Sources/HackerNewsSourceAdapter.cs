using System.Text.Json;
using Aqevryn.Common;

namespace Aqevryn.Sources;

public class HackerNewsSourceAdapter : BaseSourceAdapter
{
    private readonly HttpClientHelper _http;
    private const string ApiBase = "https://hacker-news.firebaseio.com/v0";

    public HackerNewsSourceAdapter(Dictionary<string, object> config, Aqevryn.Config.AqevrynSettings settings)
        : base(config, settings)
    {
        _http = new HttpClientHelper(settings.MaxRetries, settings.RequestTimeoutSeconds);
    }

    public override async Task<bool> ValidateAsync()
    {
        try { await _http.FetchStringAsync($"{ApiBase}/topstories.json"); return true; }
        catch { return false; }
    }

    public override async Task<List<SourceItem>> FetchAsync()
    {
        var items = new List<SourceItem>();
        var feedType = GetString(Config, "feed_type", "topstories");
        var maxItems = Math.Min(GetInt(Config, "max_items", 30), 100);
        var minScore = GetInt(Config, "min_score", 1);

        try
        {
            var json = await _http.FetchStringAsync($"{ApiBase}/{feedType}.json");
            var ids = JsonSerializer.Deserialize<int[]>(json) ?? Array.Empty<int>();

            foreach (var id in ids.Take(maxItems))
            {
                try
                {
                    var storyJson = await _http.FetchStringAsync($"{ApiBase}/item/{id}.json");
                    var story = JsonDocument.Parse(storyJson).RootElement;

                    if (!story.TryGetProperty("type", out var typeEl) || typeEl.GetString() != "story") continue;
                    var score = story.TryGetProperty("score", out var sc) ? sc.GetInt32() : 0;
                    if (score < minScore) continue;

                    var title = story.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var url = story.TryGetProperty("url", out var u) ? u.GetString() ?? "" : $"https://news.ycombinator.com/item?id={id}";
                    var by = story.TryGetProperty("by", out var b) ? b.GetString() ?? "" : "";
                    var time = story.TryGetProperty("time", out var tm) ? tm.GetInt64() : 0L;
                    var descendants = story.TryGetProperty("descendants", out var desc) ? desc.GetInt32() : 0;

                    var tags = new List<string>();
                    foreach (var kw in new[] { "ai", "ml", "llm", "python", "rust", "security", "cloud", "database", "startup" })
                        if (title.Contains(kw, StringComparison.OrdinalIgnoreCase)) tags.Add(kw);

                    var item = new SourceItem
                    {
                        Title = title,
                        Url = url,
                        CanonicalUrl = url,
                        SourceName = Name,
                        SourceType = "hackernews",
                        Author = by,
                        PublishedAt = time > 0 ? DateTimeOffset.FromUnixTimeSeconds(time).UtcDateTime : null,
                        Summary = $"[HN] Score: {score} | Comments: {descendants} | By: {by}",
                        Content = title,
                        Tags = tags.Count > 0 ? tags : null,
                        Category = base.Category ?? "technology",
                        RetrievedAt = DateTime.UtcNow,
                        Raw = new() { ["score"] = score, ["descendants"] = descendants },
                    };
                    item.ComputeHash();
                    items.Add(item);
                }
                catch { continue; }
            }
        }
        catch (Exception ex)
        {
            throw new SourceError($"HN fetch failed: {ex.Message}", ex);
        }
        return items;
    }
}