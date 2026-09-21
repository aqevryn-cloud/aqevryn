using System.Text.Json;
using Aqevryn.Common;

namespace Aqevryn.Sources;

/// <summary>
/// Fetches trending posts from Moltbook to use as research source material.
/// Enables Aqevryn to discover what other agents are discussing and research trending topics.
/// </summary>
public class MoltbookSourceAdapter : BaseSourceAdapter
{
    private readonly HttpClientHelper _http;
    private readonly MoltbookClient _moltbook;

    public MoltbookSourceAdapter(Dictionary<string, object> config, Aqevryn.Config.AqevrynSettings settings)
        : base(config, settings)
    {
        _http = new HttpClientHelper(settings.MaxRetries, settings.RequestTimeoutSeconds);
        _moltbook = new MoltbookClient();
    }

    public override async Task<bool> ValidateAsync()
    {
        var status = await _moltbook.CheckStatusAsync();
        return status == "claimed" || status == "pending_claim";
    }

    public override async Task<List<SourceItem>> FetchAsync()
    {
        var items = new List<SourceItem>();
        if (!_moltbook.IsRegistered) return items;

        var sort = GetString(Config, "sort", "hot");
        var limit = Math.Min(GetInt(Config, "limit", 25), 50);

        try
        {
            var feedJson = await _moltbook.GetFeedAsync(sort, limit);
            var feed = JsonDocument.Parse(feedJson);

            if (!feed.RootElement.TryGetProperty("posts", out var posts) &&
                !feed.RootElement.TryGetProperty("data", out var dataEl))
                return items;

            var postsEl = feed.RootElement.TryGetProperty("posts", out var pp) ? pp :
                          feed.RootElement.TryGetProperty("data", out var dd) ? dd : default;

            if (postsEl.ValueKind != JsonValueKind.Array) return items;

            foreach (var post in postsEl.EnumerateArray())
            {
                try
                {
                    var title = post.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var content = post.TryGetProperty("content", out var c) ? (c.GetString() ?? "") : "";
                    var authorName = post.TryGetProperty("author", out var a)
                        ? (a.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "")
                        : "";
                    var submoltName = post.TryGetProperty("submolt", out var s)
                        ? (s.TryGetProperty("name", out var sn) ? sn.GetString() ?? "" : "")
                        : "";

                    if (string.IsNullOrEmpty(title)) continue;

                    var tags = new List<string>();
                    if (!string.IsNullOrEmpty(submoltName)) tags.Add(submoltName);
                    tags.Add("moltbook");

                    var item = new SourceItem
                    {
                        Title = title,
                        Url = post.TryGetProperty("url", out var u) ? (u.GetString() ?? "") : ("https://www.moltbook.com/posts/" + (post.TryGetProperty("id", out var id) ? id.GetString() : "")),
                        CanonicalUrl = "https://www.moltbook.com/posts/" + (post.TryGetProperty("id", out var pid) ? pid.GetString() : ""),
                        SourceName = $"Moltbook/{submoltName}",
                        SourceType = "community",
                        Author = authorName,
                        Summary = content.Length > 500 ? content[..500] : content,
                        Content = content,
                        Tags = tags.Distinct().ToList(),
                        Category = base.Category ?? "technology",
                        RetrievedAt = DateTime.UtcNow,
                    };
                    item.ComputeHash();
                    items.Add(item);
                }
                catch { continue; }
            }
        }
        catch { }

        return items;
    }
}