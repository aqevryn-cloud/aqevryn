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
    private const int MaxRetries = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

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
    /// waits for the default branch to be ready.
    /// </summary>
    /// <returns>True if repository is ready (exists or was created), false on failure.</returns>
    public async Task<bool> EnsureRepositoryAsync()
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("GitHub not configured — cannot ensure repository");
            return false;
        }

        // Step 1: Check if repository exists (with retry for transient errors)
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            if (await RepositoryExistsAsync())
            {
                _logger.LogInformation("Repository {Owner}/{Repo} already exists", _owner, _repo);
                return true;
            }

            if (attempt < MaxRetries)
            {
                _logger.LogDebug("Repository {Owner}/{Repo} not found on attempt {Attempt}, retrying...", _owner, _repo, attempt);
                await Task.Delay(RetryDelay);
            }
        }

        _logger.LogInformation("Repository {Owner}/{Repo} does not exist — creating...", _owner, _repo);

        // Step 2: Create the repository
        var created = await CreateRepositoryAsync();
        if (!created) return false;

        // Step 3: Wait for the default branch to be ready by polling
        _logger.LogInformation("Repository created: https://github.com/{Owner}/{Repo} — waiting for default branch to be ready...", _owner, _repo);
        var branchReady = await WaitForBranchReadyAsync();
        if (!branchReady)
        {
            _logger.LogError("Repository created but default branch '{Branch}' never became available. This could be a GitHub propagation delay — try again shortly.", _defaultBranch);
            return false;
        }

        _logger.LogInformation("Repository {Owner}/{Repo} is ready with branch '{Branch}'", _owner, _repo, _defaultBranch);
        return true;
    }

    /// <summary>
    /// Checks whether the repository already exists.
    /// </summary>
    public async Task<bool> RepositoryExistsAsync()
    {
        try
        {
            using var http = CreateClient();
            var response = await http.GetAsync($"{ApiBase}/repos/{_owner}/{_repo}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RepositoryExistsAsync check failed");
            return false;
        }
    }

    /// <summary>
    /// Polls until the default branch (or 'master' fallback) is available
    /// after a new repository creation.
    /// </summary>
    public async Task<bool> WaitForBranchReadyAsync()
    {
        for (int attempt = 1; attempt <= MaxRetries + 3; attempt++)
        {
            // Try the configured default branch first
            var sha = await GetBranchShaAsync(_defaultBranch);
            if (sha != null) return true;

            // Fallback to 'master' in case GitHub defaults there
            sha = await GetBranchShaAsync("master");
            if (sha != null)
            {
                _logger.LogInformation("Default branch is 'master', not '{Branch}' — consider updating GITHUB_DEFAULT_BRANCH in your config", _defaultBranch);
                return true;
            }

            _logger.LogDebug("Branch not ready yet (attempt {Attempt}), waiting...", attempt);
            await Task.Delay(RetryDelay);
        }

        return false;
    }

    /// <summary>
    /// Creates the repository under the owner's account with explicit default branch.
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
                default_branch = _defaultBranch,  // Explicitly set the default branch
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
    /// Retries if the branch isn't ready yet.
    /// </summary>
    public async Task<bool> ScaffoldRepositoryAsync()
    {
        try
        {
            var createdAny = false;

            // Determine the actual default branch (could be 'master' if auto_init defaulted there)
            var branch = await ResolveDefaultBranchAsync();

            // Create .gitkeep in articles/drafts and articles/published so the
            // directories exist in the repository
            var success = await CreateFileIfMissingAsync(
                branch: branch,
                path: "articles/drafts/.gitkeep",
                content: "# Draft articles from Aqevryn research pipeline\n");
            if (success) createdAny = true;

            success = await CreateFileIfMissingAsync(
                branch: branch,
                path: "articles/published/.gitkeep",
                content: "# Published articles — merged after human approval\n");
            if (success) createdAny = true;

            // Create website scaffolding
            await CreateFileIfMissingAsync(
                branch: branch,
                path: "website/README.md",
                content: "# Aqevryn Research Website\n\nGenerated by the Aqevryn pipeline. See /methodology.html for our research methodology.\n");

            return createdAny;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scaffold repository");
            return false;
        }
    }

    /// <summary>
    /// Resolves the actual default branch name of the repository.
    /// </summary>
    private async Task<string> ResolveDefaultBranchAsync()
    {
        try
        {
            using var http = CreateClient();
            var response = await http.GetAsync($"{ApiBase}/repos/{_owner}/{_repo}");
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("default_branch", out var branch))
                {
                    return branch.GetString() ?? _defaultBranch;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not resolve default branch, falling back to configured branch");
        }
        return _defaultBranch;
    }

    /// <summary>
    /// Gets the SHA of a branch. Returns null if branch doesn't exist or on error.
    /// </summary>
    public async Task<string?> GetBranchShaAsync(string branch)
    {
        try
        {
            using var http = CreateClient();
            var response = await http.GetAsync($"{ApiBase}/repos/{_owner}/{_repo}/git/ref/heads/{branch}");
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("object").GetProperty("sha").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GetBranchShaAsync failed for branch '{Branch}'", branch);
            return null;
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
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CreateFileIfMissingAsync failed for '{Path}'", path);
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
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("IsOrganizationAsync: users endpoint returned {Status}", response.StatusCode);
                return false;
            }
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var isOrg = doc.RootElement.TryGetProperty("type", out var type) && type.GetString() == "Organization";
            var ownerType = isOrg ? "an organization" : "a user";
            _logger.LogDebug("Owner '{Owner}' is {OwnerType}", owner, ownerType);
            return isOrg;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "IsOrganizationAsync failed for '{Owner}'", owner);
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