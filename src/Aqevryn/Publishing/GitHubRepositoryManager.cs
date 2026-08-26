using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Publishing;

/// <summary>
/// Manages GitHub repository lifecycle and initialization.
/// Can create a repository if it does not exist, and scaffolds
/// the required directory structure for Aqevryn publications.
/// </summary>
public class GitHubRepositoryManager
{
    private readonly string _token;
    private readonly string _owner;
    private readonly string _repo;
    private readonly string _defaultBranch;
    private readonly ILogger<GitHubRepositoryManager> _logger;
    private const string ApiBase = "https://api.github.com";

    public GitHubRepositoryManager(string token, string owner, string repo,
        string defaultBranch = "main", ILogger<GitHubRepositoryManager>? logger = null)
    {
        _token = token;
        _owner = owner;
        _repo = repo;
        _defaultBranch = defaultBranch;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GitHubRepositoryManager>.Instance;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_token) && !string.IsNullOrEmpty(_owner) && !string.IsNullOrEmpty(_repo);

    /// <summary>
    /// Ensures the repository exists. If it does not, creates it and
    /// scaffolds the articles directory structure.
    /// </summary>
    /// <returns>True if repository is ready (exists or was created), false on failure.</returns>
    public async Task<bool> EnsureRepositoryAsync()
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("GitHub not configured — cannot ensure repository");
            return false;
        }

        // Step 1: Check if repository exists
        if (await RepositoryExistsAsync())
        {
            _logger.LogInformation("Repository {Owner}/{Repo} already exists", _owner, _repo);
            return true;
        }

        _logger.LogInformation("Repository {Owner}/{Repo} does not exist — creating...", _owner, _repo);

        // Step 2: Create the repository
        var created = await CreateRepositoryAsync();
        if (!created) return false;

        // Step 3: Wait briefly for GitHub to propagate the new repo
        await Task.Delay(TimeSpan.FromSeconds(3));

        _logger.LogInformation("Repository created: https://github.com/{Owner}/{Repo}", _owner, _repo);
        return true;
    }

    /// <summary>Checks whether the repository already exists.</summary>
    public async Task<bool> RepositoryExistsAsync()
    {
        try
        {
            using var http = CreateClient();
            var response = await http.GetAsync($"{ApiBase}/repos/{_owner}/{_repo}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates the repository under the owner's account.
    /// </summary>
    private async Task<bool> CreateRepositoryAsync()
    {
        try
        {
            using var http = CreateClient();
            // Determine if owner is a user or an organization
            var isOrg = await IsOrganizationAsync(_owner);

            var payload = JsonSerializer.Serialize(new
            {
                name = _repo,
                description = "Aqevryn — Autonomous Technology Research & Publishing",
                @private = true,
                has_issues = true,
                has_wiki = true,
                auto_init = true,  // Creates with initial commit
            });

            var endpoint = isOrg
                ? $"{ApiBase}/orgs/{_owner}/repos"
                : $"{ApiBase}/user/repos";

            var response = await http.PostAsync(endpoint,
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create repository: {Status} {Body}",
                    response.StatusCode, errorBody);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while creating repository");
            return false;
        }
    }

    /// <summary>
    /// Scaffolds the articles directory structure by creating placeholder files.
    /// Called after repository creation or if needed files are missing.
    /// </summary>
    public async Task<bool> ScaffoldRepositoryAsync()
    {
        try
        {
            // Create .gitkeep in articles/drafts and articles/published so the
            // directories exist in the repository
            var success = await CreateFileIfMissingAsync(
                branch: _defaultBranch,
                path: "articles/drafts/.gitkeep",
                content: "# Draft articles from Aqevryn research pipeline\n");
            if (!success) success = await CreateFileIfMissingAsync(
                branch: _defaultBranch,
                path: "articles/published/.gitkeep",
                content: "# Published articles — merged after human approval\n");

            // Create website scaffolding
            await CreateFileIfMissingAsync(
                branch: _defaultBranch,
                path: "website/README.md",
                content: "# Aqevryn Research Website\n\nGenerated by the Aqevryn pipeline. See /methodology.html for our research methodology.\n");

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scaffold repository");
            return false;
        }
    }

    private async Task<bool> CreateFileIfMissingAsync(string branch, string path, string content)
    {
        try
        {
            using var http = CreateClient();

            // Check if file already exists
            var checkResponse = await http.GetAsync($"{ApiBase}/repos/{_owner}/{_repo}/contents/{path}?ref={branch}");
            if (checkResponse.IsSuccessStatusCode) return true; // Already exists

            // Create the file
            var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content));
            var payload = JsonSerializer.Serialize(new
            {
                message = $"chore: create {path}",
                content = encoded,
                branch,
            });

            var response = await http.PutAsync($"{ApiBase}/repos/{_owner}/{_repo}/contents/{path}",
                new StringContent(payload, System.Text.Encoding.UTF8, "application/json"));

            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Determines whether an owner string is an organization or a user.</summary>
    private async Task<bool> IsOrganizationAsync(string owner)
    {
        try
        {
            using var http = CreateClient();
            var response = await http.GetAsync($"{ApiBase}/users/{owner}");
            if (!response.IsSuccessStatusCode) return false;
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "Organization";
        }
        catch
        {
            return false;
        }
    }

    private HttpClient CreateClient()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_token}");
        http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");
        http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return http;
    }
}