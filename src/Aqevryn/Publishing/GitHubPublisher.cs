using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Publishing;

public class GitHubPublishResult
{
    public bool Success { get; set; }
    public string Branch { get; set; } = "";
    public string? CommitSha { get; set; }
    public int? PrNumber { get; set; }
    public string? PrUrl { get; set; }
    public string? Error { get; set; }
}

public class GitHubPublisher
{
    private readonly string _token;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _defaultBranch;
    private readonly bool _autoPublish;
    private readonly ILogger<GitHubPublisher> _logger;
    private const string ApiBase = "https://api.github.com";

    public GitHubPublisher(string token, string owner, string repo, string defaultBranch = "main",
        bool autoPublish = false, ILogger<GitHubPublisher>? logger = null)
    {
        _token = token; _owner = owner; _repo = repo; _defaultBranch = defaultBranch;
        _autoPublish = autoPublish;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubPublisher>.Instance;
    }

    public async Task<GitHubPublishResult> PublishAsync(string articleContent, string filename,
        string topic, Dictionary<string, double>? scores = null, bool dryRun = false)
    {
        var branchName = $"research/{topic.ToLower().Replace(" ", "-").Replace(":", "").Replace(".", "")[..Math.Min(60, topic.Length)]}";

        if (dryRun)
        {
            _logger.LogInformation("Dry run: would create branch {Branch} with file {File}", branchName, filename);
            return new GitHubPublishResult { Success = true, Branch = branchName, Error = "Dry run — no actual publish" };
        }

        if (string.IsNullOrEmpty(_token))
            return new GitHubPublishResult { Success = false, Error = "GitHub token not configured" };
        if (string.IsNullOrEmpty(_owner) || string.IsNullOrEmpty(_repo))
            return new GitHubPublishResult { Success = false, Error = "GitHub owner/repo not configured" };

        try
        {
            var mainSha = await GetBranchShaAsync(_defaultBranch);
            if (mainSha == null) return new GitHubPublishResult { Success = false, Error = $"Could not get SHA for branch '{_defaultBranch}'" };

            await CreateBranchAsync(branchName, mainSha);
            var commitSha = await CreateOrUpdateFileAsync(branchName, filename, articleContent);
            var pr = await CreatePullRequestAsync(branchName, topic, scores);

            _logger.LogInformation("Created PR #{PrNumber}: {PrUrl}", pr?.GetValueOrDefault("number"), pr?.GetValueOrDefault("html_url"));
            return new GitHubPublishResult
            {
                Success = true, Branch = branchName, CommitSha = commitSha,
                PrNumber = pr?.GetValueOrDefault("number") as int?,
                PrUrl = pr?.GetValueOrDefault("html_url")?.ToString(),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GitHub publish failed");
            return new GitHubPublishResult { Success = false, Branch = branchName, Error = ex.Message };
        }
    }

    private async Task<string?> GetBranchShaAsync(string branch)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");
        var response = await http.GetAsync($"{ApiBase}/repos/{_owner}/{_repo}/git/ref/heads/{branch}");
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("object").GetProperty("sha").GetString();
    }

    private async Task<bool> CreateBranchAsync(string branch, string sha)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");
        var payload = JsonSerializer.Serialize(new { @ref = $"refs/heads/{branch}", sha });
        var response = await http.PostAsync($"{ApiBase}/repos/{_owner}/{_repo}/git/refs",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
        return response.IsSuccessStatusCode;
    }

    private async Task<string?> CreateOrUpdateFileAsync(string branch, string path, string content)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");
        var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content));
        var payload = JsonSerializer.Serialize(new { message = $"Research article: {path}", content = encoded, branch });
        var response = await http.PutAsync($"{ApiBase}/repos/{_owner}/{_repo}/contents/articles/{path}",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement.GetProperty("commit").GetProperty("sha").GetString();
    }

    private async Task<Dictionary<string, object?>?> CreatePullRequestAsync(string branch, string topic, Dictionary<string, double>? scores)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");

        var body = $"## Topic\n\n{topic}\n\n## Scores\n";
        if (scores != null)
            foreach (var (key, value) in scores)
                body += $"| {char.ToUpper(key[0]) + key[1..]} | {value} |\n";

        var payload = JsonSerializer.Serialize(new
        {
            title = $"Research: {topic}",
            body = body + "\n---\n_Generated by Aqevryn_",
            head = branch,
            @base = _defaultBranch,
            draft = !_autoPublish,
        });

        var response = await http.PostAsync($"{ApiBase}/repos/{_owner}/{_repo}/pulls",
            new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return new Dictionary<string, object?>
            {
                ["number"] = doc.RootElement.GetProperty("number").GetInt32(),
                ["html_url"] = doc.RootElement.GetProperty("html_url").GetString(),
            };
        }
        return null;
    }
}