using System.Text.Json;
using Aqevryn.Common;

namespace Aqevryn.Sources;

public class GitHubSourceAdapter : BaseSourceAdapter
{
    private readonly HttpClientHelper _http;
    private const string ApiBase = "https://api.github.com";

    public GitHubSourceAdapter(Dictionary<string, object> config, Aqevryn.Config.AqevrynSettings settings)
        : base(config, settings)
    {
        _http = new HttpClientHelper(settings.MaxRetries, settings.RequestTimeoutSeconds);
    }

    public override async Task<bool> ValidateAsync()
    {
        try { await _http.FetchStringAsync($"{ApiBase}/rate_limit"); return true; }
        catch { return false; }
    }

    public override async Task<List<SourceItem>> FetchAsync()
    {
        var items = new List<SourceItem>();
        var topics = Config.TryGetValue("topics", out var t) && t is List<object> tl
            ? tl.Select(x => x?.ToString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList()
            : new List<string> { "ai", "machine-learning", "llm", "agents" };
        var minStars = GetInt(Config, "min_stars", 50);
        var sort = GetString(Config, "sort", "updated");

        foreach (var topic in topics)
        {
            try
            {
                var query = Uri.EscapeDataString($"topic:{topic} stars:>={minStars}");
                var url = $"{ApiBase}/search/repositories?q={query}&sort={sort}&order=desc&per_page=10";
                var headers = new Dictionary<string, string> { { "Accept", "application/vnd.github+json" } };
                var json = await _http.FetchStringAsync(url, headers);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("items", out var itemsEl)) continue;

                foreach (var repo in itemsEl.EnumerateArray())
                {
                    var name = repo.GetProperty("full_name").GetString() ?? "";
                    var htmlUrl = repo.GetProperty("html_url").GetString() ?? "";
                    var desc = repo.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    var lang = repo.TryGetProperty("language", out var l) ? l.GetString() ?? "" : "";
                    var stars = repo.TryGetProperty("stargazers_count", out var s) ? s.GetInt32() : 0;
                    var owner = repo.TryGetProperty("owner", out var o) ? o.GetProperty("login").GetString() ?? "" : "";

                    var repoTopics = new List<string> { topic };
                    if (!string.IsNullOrEmpty(lang)) repoTopics.Add(lang);

                    var item = new SourceItem
                    {
                        Title = name,
                        Url = htmlUrl,
                        CanonicalUrl = htmlUrl,
                        SourceName = Name,
                        SourceType = "github",
                        Author = owner,
                        Summary = desc,
                        Content = desc,
                        Tags = repoTopics,
                        Category = base.Category ?? "developer_tools",
                        RetrievedAt = DateTime.UtcNow,
                        Raw = new() { ["stars"] = stars, ["language"] = lang },
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