using System.Text.Json;
using Aqevryn.Common;

namespace Aqevryn.Sources;

public class RedditSourceAdapter : BaseSourceAdapter
{
    private readonly HttpClientHelper _http;
    private const string ApiBase = "https://www.reddit.com";

    public RedditSourceAdapter(Dictionary<string, object> config, Aqevryn.Config.AqevrynSettings settings)
        : base(config, settings)
    {
        _http = new HttpClientHelper(settings.MaxRetries, settings.RequestTimeoutSeconds);
    }

    public override async Task<bool> ValidateAsync()
    {
        try
        {
            var headers = new Dictionary<string, string> { { "User-Agent", "Aqevryn/1.0" } };
            await _http.FetchStringAsync($"{ApiBase}/r/all/hot.json?limit=1", headers);
            return true;
        }
        catch { return false; }
    }

    public override async Task<List<SourceItem>> FetchAsync()
    {
        var items = new List<SourceItem>();
        var subreddits = Config.TryGetValue("subreddits", out var s) && s is List<object> sl
            ? sl.Select(x => x?.ToString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList()
            : new List<string> { "MachineLearning", "artificial", "technology", "programming" };
        var feedType = GetString(Config, "feed_type", "hot");
        var limit = Math.Min(GetInt(Config, "limit", 25), 100);
        var minScore = GetInt(Config, "min_score", 1);
        var headers = new Dictionary<string, string> { { "User-Agent", "Aqevryn/1.0" } };

        foreach (var subreddit in subreddits)
        {
            try
            {
                var json = await _http.FetchStringAsync($"{ApiBase}/r/{subreddit}/{feedType}.json?limit={limit}", headers);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("children", out var children))
                    continue;

                foreach (var child in children.EnumerateArray())
                {
                    var post = child.GetProperty("data");
                    var title = post.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var score = post.TryGetProperty("score", out var sc) ? sc.GetInt32() : 0;
                    if (score < minScore || string.IsNullOrEmpty(title)) continue;

                    var url = post.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "";
                    var permalink = post.TryGetProperty("permalink", out var p) ? p.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(url) || url.Contains("reddit.com"))
                        url = $"https://www.reddit.com{permalink}";

                    var selftext = post.TryGetProperty("selftext", out var st) ? st.GetString() ?? "" : "";
                    var author = post.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "";
                    var created = post.TryGetProperty("created_utc", out var cr) ? cr.GetDouble() : 0.0;
                    var numComments = post.TryGetProperty("num_comments", out var nc) ? nc.GetInt32() : 0;

                    var tags = new List<string> { subreddit.ToLower() };
                    foreach (var kw in new[] { "ai", "ml", "llm", "gpt", "python", "rust", "docker", "kubernetes", "security", "cloud", "database", "agent", "automation", "coding", "programming", "open source", "startup", "quantum", "robotics" })
                        if (title.Contains(kw, StringComparison.OrdinalIgnoreCase)) tags.Add(kw);

                    var item = new SourceItem
                    {
                        Title = title,
                        Url = url,
                        CanonicalUrl = url,
                        SourceName = $"r/{subreddit}",
                        SourceType = "reddit",
                        Author = author,
                        PublishedAt = DateTimeOffset.FromUnixTimeSeconds((long)created).UtcDateTime,
                        Summary = selftext.Length > 500 ? selftext[..500] : selftext,
                        Content = selftext,
                        Tags = tags.Distinct().ToList(),
                        Category = base.Category ?? "technology",
                        RetrievedAt = DateTime.UtcNow,
                        Raw = new() { ["score"] = score, ["num_comments"] = numComments, ["subreddit"] = subreddit },
                    };
                    item.ComputeHash();
                    items.Add(item);
                }
            }
            catch { continue; }
        }
        return items;
    }
}