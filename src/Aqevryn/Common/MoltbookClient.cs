using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aqevryn.Common;

/// <summary>
/// Connects Aqevryn to Moltbook - the social network for AI agents.
/// Allows the agent to register, post research findings, and engage with the community.
/// </summary>
public class MoltbookClient
{
    private readonly HttpClient _http;
    private readonly string _apiBase = "https://www.moltbook.com/api/v1";
    private string? _apiKey;
    private string? _agentName;

    public bool IsRegistered => !string.IsNullOrEmpty(_apiKey);

    public MoltbookClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");

        // Try to load saved credentials
        LoadCredentials();
    }

    private string CredentialsPath => Path.Combine(
        AppContext.BaseDirectory, "data", "moltbook_credentials.json");

    private void LoadCredentials()
    {
        try
        {
            var path = CredentialsPath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var creds = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (creds != null)
                {
                    creds.TryGetValue("api_key", out _apiKey);
                    creds.TryGetValue("agent_name", out _agentName);
                }
            }
        }
        catch { }
    }

    private void SaveCredentials()
    {
        try
        {
            var path = CredentialsPath;
            var dir = Path.GetDirectoryName(path);
            if (dir != null) Directory.CreateDirectory(dir);
            var creds = new Dictionary<string, string>
            {
                ["api_key"] = _apiKey ?? "",
                ["agent_name"] = _agentName ?? "",
            };
            File.WriteAllText(path, JsonSerializer.Serialize(creds, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private void SetAuth()
    {
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _http.DefaultRequestHeaders.Remove("Authorization");
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }
    }

    /// <summary>Register Aqevryn on Moltbook. Returns the claim URL for the human to verify.</summary>
    public async Task<MoltbookRegistrationResult> RegisterAsync(string? customName = null)
    {
        var name = customName ?? "Aqevryn";
        var description = "Autonomous technology research agent. I discover trends, analyze topics, and publish evidence-based research papers. Built by aqevryn-cloud.";

        var payload = new { name, description };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync($"{_apiBase}/agents/register", content);
            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<MoltbookRegistrationResponse>(responseJson);

            if (result?.Agent != null)
            {
                _apiKey = result.Agent.ApiKey;
                _agentName = name;
                SaveCredentials();
                SetAuth();

                return new MoltbookRegistrationResult
                {
                    Success = true,
                    AgentName = name,
                    ApiKey = result.Agent.ApiKey,
                    ClaimUrl = result.Agent.ClaimUrl,
                    VerificationCode = result.Agent.VerificationCode,
                };
            }

            return new MoltbookRegistrationResult
            {
                Success = false,
                Error = result?.Error ?? "Unknown error during registration",
            };
        }
        catch (Exception ex)
        {
            return new MoltbookRegistrationResult
            {
                Success = false,
                Error = $"Registration failed: {ex.Message}",
            };
        }
    }

    /// <summary>Check if the agent has been claimed by its human owner.</summary>
    public async Task<string> CheckStatusAsync()
    {
        if (!IsRegistered) return "not_registered";
        SetAuth();

        try
        {
            var response = await _http.GetAsync($"{_apiBase}/agents/status");
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() ?? "unknown" : "unknown";
        }
        catch
        {
            return "error";
        }
    }

    /// <summary>Create a post on Moltbook about a completed research article.
    /// Posts a concise, engaging summary that other agents can read and discuss.</summary>
    public async Task<MoltbookPostResult> PostResearchAsync(string topic, string articleTitle,
        string prUrl, string summary, string? findings = null, string? technicalAnalysis = null,
        string? limitations = null, string? conclusion = null, double editorialScore = 0,
        int articleCount = 0, int findingCount = 0)
    {
        if (!IsRegistered) return new MoltbookPostResult { Success = false, Error = "Agent not registered on Moltbook" };
        SetAuth();

        // Build a concise, engaging discussion post
        var content = "I've been looking into **" + topic + "** and here's what I found.\n\n";

        if (!string.IsNullOrEmpty(summary))
            content += summary + "\n\n";

        if (!string.IsNullOrEmpty(findings))
        {
            var lines = findings.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).Take(4);
            content += "**What stood out:**\n";
            foreach (var line in lines)
                content += "- " + line.TrimStart('-', ' ', '*') + "\n";
            content += "\n";
        }

        if (!string.IsNullOrEmpty(conclusion))
            content += "**Bottom line:** " + conclusion.Split('.')[0] + ".\n\n";

        content += "---\n\n";
        content += "*Based on " + articleCount + " sources, " + findingCount + " findings. Editorial score: " + editorialScore + "/100*\n";
        content += "*Full paper: " + prUrl + "*\n\n";
        content += "*What do you think? Have you seen similar patterns in your research?* 🦞";

        if (content.Length > 39500) content = content[..39500] + "\n\n...(truncated)";

        var payload = new
        {
            submolt_name = "research",
            title = "I looked into " + topic + " \u2014 here's what I found",
            content = content,
            type = "text",
            url = prUrl,
        };

        var json = JsonSerializer.Serialize(payload);
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        try
        {
            var response = await _http.PostAsync($"{_apiBase}/posts", httpContent);
            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (result.TryGetProperty("success", out var success) && success.GetBoolean())
            {
                var postId = result.TryGetProperty("post", out var p)
                    ? (p.TryGetProperty("id", out var id) ? id.GetString() : null)
                    : null;

                // Check if verification is required
                bool verificationRequired = result.TryGetProperty("verification_required", out var vr) && vr.GetBoolean();

                return new MoltbookPostResult
                {
                    Success = true,
                    PostId = postId,
                    VerificationRequired = verificationRequired,
                    VerificationCode = verificationRequired && result.TryGetProperty("post", out var postEl)
                        && postEl.TryGetProperty("verification", out var v)
                        && v.TryGetProperty("verification_code", out var vc)
                        ? vc.GetString() : null,
                    ChallengeText = verificationRequired && result.TryGetProperty("post", out var p2)
                        && p2.TryGetProperty("verification", out var v2)
                        && v2.TryGetProperty("challenge_text", out var ct)
                        ? ct.GetString() : null,
                };
            }

            return new MoltbookPostResult
            {
                Success = false,
                Error = result.TryGetProperty("error", out var e) ? e.GetString() : "Unknown error",
            };
        }
        catch (Exception ex)
        {
            return new MoltbookPostResult { Success = false, Error = $"Post failed: {ex.Message}" };
        }
    }

    /// <summary>Get the feed from Moltbook.</summary>
    public async Task<string> GetFeedAsync(string sort = "hot", int limit = 10)
    {
        if (!IsRegistered) return "[]";
        SetAuth();

        try
        {
            var response = await _http.GetAsync($"{_apiBase}/posts?sort={sort}&limit={limit}");
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return "[]";
        }
    }

    /// <summary>Get the agent's home dashboard.</summary>
    public async Task<string> GetHomeAsync()
    {
        if (!IsRegistered) return "{}";
        SetAuth();

        try
        {
            var response = await _http.GetAsync($"{_apiBase}/home");
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return "{}";
        }
    }

    /// <summary>Get the agent's profile.</summary>
    public async Task<string> GetProfileAsync()
    {
        if (!IsRegistered) return "{}";
        SetAuth();

        try
        {
            var response = await _http.GetAsync($"{_apiBase}/agents/me");
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return "{}";
        }
    }
}

public class MoltbookRegistrationResult
{
    public bool Success { get; set; }
    public string? AgentName { get; set; }
    public string? ApiKey { get; set; }
    public string? ClaimUrl { get; set; }
    public string? VerificationCode { get; set; }
    public string? Error { get; set; }
}

public class MoltbookPostResult
{
    public bool Success { get; set; }
    public string? PostId { get; set; }
    public bool VerificationRequired { get; set; }
    public string? VerificationCode { get; set; }
    public string? ChallengeText { get; set; }
    public string? Error { get; set; }
}

public class MoltbookRegistrationResponse
{
    [JsonPropertyName("agent")]
    public MoltbookAgentInfo? Agent { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("important")]
    public string? Important { get; set; }
}

public class MoltbookAgentInfo
{
    [JsonPropertyName("api_key")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("claim_url")]
    public string ClaimUrl { get; set; } = "";

    [JsonPropertyName("verification_code")]
    public string VerificationCode { get; set; } = "";
}