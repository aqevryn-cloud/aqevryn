using System.Text.Json;
using Aqevryn.Config;

namespace Aqevryn.Common;

/// <summary>
/// Engages with the Moltbook community based on Aqevryn's soul.md principles.
/// 
/// What it does:
/// 1. Periodically checks the Moltbook feed for interesting discussions
/// 2. Uses LLM to determine which posts align with its research mission
/// 3. Comments on posts with evidence-based contributions
/// 4. Identifies knowledge gaps and opportunities for deeper research
/// 5. Challenges claims that lack evidence, supports claims with evidence
/// </summary>
public class MoltbookEngager
{
    private readonly MoltbookClient _client;
    private readonly AqevrynSettings _settings;
    private bool _initialized;

    // Track which posts we've already engaged with to avoid repeating
    private static readonly HashSet<string> _engagedPostIds = new();
    private static readonly string _stateFile = Path.Combine(
        AppContext.BaseDirectory, "data", "moltbook_engagement.json");

    public MoltbookEngager(MoltbookClient client, AqevrynSettings settings)
    {
        _client = client;
        _settings = settings;
        LoadState();
    }

    private void LoadState()
    {
        try
        {
            if (File.Exists(_stateFile))
            {
                var json = File.ReadAllText(_stateFile);
                var ids = JsonSerializer.Deserialize<List<string>>(json);
                if (ids != null)
                    foreach (var id in ids) _engagedPostIds.Add(id);
            }
        }
        catch { }
    }

    private void SaveState()
    {
        try
        {
            var dir = Path.GetDirectoryName(_stateFile);
            if (dir != null) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_engagedPostIds.ToList());
            File.WriteAllText(_stateFile, json);
        }
        catch { }
    }

    /// <summary>Run one engagement cycle: browse feed, find interesting posts, comment.</summary>
    public async Task RunEngagementCycleAsync()
    {
        if (!_client.IsRegistered) return;

        _initialized = true;
        var feed = await _client.GetFeedAsync("hot", 20);
        if (string.IsNullOrEmpty(feed) || feed == "[]") return;

        try
        {
            var doc = JsonDocument.Parse(feed);
            var posts = doc.RootElement.TryGetProperty("posts", out var p) ? p : default;
            if (posts.ValueKind != JsonValueKind.Array) return;

            var candidates = new List<JsonElement>();
            foreach (var post in posts.EnumerateArray())
            {
                var id = post.TryGetProperty("id", out var i) ? i.GetString() : "";
                if (string.IsNullOrEmpty(id) || _engagedPostIds.Contains(id)) continue;
                candidates.Add(post);
            }

            if (candidates.Count == 0) return;

            // Select the most relevant post to engage with
            var selected = await SelectBestPostAsync(candidates);
            if (selected == null) return;

            var postId = selected.Value.TryGetProperty("id", out var idEl) ? idEl.GetString() : "";
            var title = selected.Value.TryGetProperty("title", out var t) ? t.GetString() : "";
            var content = selected.Value.TryGetProperty("content", out var c) ? c.GetString() : "";
            var author = selected.Value.TryGetProperty("author", out var a)
                ? (a.TryGetProperty("name", out var n) ? n.GetString() : "") : "";

            if (string.IsNullOrEmpty(postId) || string.IsNullOrEmpty(title)) return;

            // Generate a thoughtful comment using LLM
            var comment = await GenerateCommentAsync(title, content, author);
            if (string.IsNullOrEmpty(comment)) return;

            // Post the comment
            var payload = new { content = comment };
            var json = JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            try
            {
                var http = new HttpClient();
                http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_client.GetApiKey()}");
                http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");
                var response = await http.PostAsync(
                    $"https://www.moltbook.com/api/v1/posts/{postId}/comments", httpContent);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _engagedPostIds.Add(postId);
                    SaveState();
                    Console.WriteLine($"💬 Commented on Moltbook: \"{title}\"");
                }
                else
                {
                    Console.WriteLine($"❌ Failed to comment: {responseJson}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Comment error: {ex.Message}");
            }
        }
        catch { }
    }

    /// <summary>Use the LLM to select the most interesting post based on soul.md principles.</summary>
    private async Task<JsonElement?> SelectBestPostAsync(List<JsonElement> candidates)
    {
        // If no LLM, just pick the first unengaged post
        if (_settings.LlmProvider == "mock" || string.IsNullOrEmpty(_settings.LlmApiKey))
        {
            return candidates.FirstOrDefault();
        }

        // Build a prompt with the available posts
        var postsText = new List<string>();
        foreach (var post in candidates.Take(10))
        {
            var title = post.TryGetProperty("title", out var t) ? t.GetString() : "";
            var content = post.TryGetProperty("content", out var c) ? (c.GetString() ?? "") : "";
            var author = post.TryGetProperty("author", out var a)
                ? (a.TryGetProperty("name", out var n) ? n.GetString() : "") : "";
            var upvotes = post.TryGetProperty("upvotes", out var u) ? u.GetInt32() : 0;
            postsText.Add($"- \"{title}\" by {author} ({upvotes} upvotes)");
        }

        var prompt = $"I am Aqevryn, a technology research agent. My purpose is to discover meaningful questions, investigate evidence, and expand understanding.\n\nI'm browsing Moltbook and need to decide which post to engage with. Here are the available discussions:\n\n{string.Join("\n", postsText)}\n\nBased on my principles (research what matters, identify knowledge gaps, challenge assumptions, evaluate evidence), which post should I engage with? Choose the one where I can add the most value through a research perspective.\n\nReturn JSON with: {{\"selected\": <number (0-indexed)>}}";

        try
        {
            var baseUrl = _settings.LlmProvider switch
            {
                "openai" => "https://api.openai.com/v1",
                "openrouter" => "https://openrouter.ai/api/v1",
                _ => "https://api.openai.com/v1"
            };
            var model = _settings.LlmModel == "gpt-5-nano" || string.IsNullOrEmpty(_settings.LlmModel) ? "gpt-4o-mini" : _settings.LlmModel;

            var payloadObj = new
            {
                model = model,
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = 100,
                temperature = 0.3,
                response_format = new { type = "json_object" }
            };

            var http = new HttpClient();
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.LlmApiKey}");
            http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");
            if (_settings.LlmProvider == "openrouter")
            {
                http.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/aqevryn-cloud/aqevryn");
                http.DefaultRequestHeaders.Add("X-Title", "Aqevryn Research");
            }

            var httpContent = new StringContent(JsonSerializer.Serialize(payloadObj), System.Text.Encoding.UTF8, "application/json");
            var response = await http.PostAsync($"{baseUrl}/chat/completions", httpContent);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
            var result = JsonSerializer.Deserialize<Dictionary<string, int>>(text);
            if (result != null && result.TryGetValue("selected", out var idx) && idx >= 0 && idx < candidates.Count)
                return candidates[idx];
        }
        catch { }

        return candidates.FirstOrDefault();
    }

    /// <summary>Generate a thoughtful, evidence-based comment using LLM.</summary>
    private async Task<string?> GenerateCommentAsync(string title, string content, string author)
    {
        if (_settings.LlmProvider == "mock" || string.IsNullOrEmpty(_settings.LlmApiKey))
        {
            return $"Interesting perspective, @{author}! I've been researching related topics. The evidence I've gathered suggests there are several factors worth considering here.";
        }

        var contentPreview = (content ?? "").Length > 2000 ? content[..2000] + "..." : (content ?? "");

        var systemPrompt = @"You are Aqevryn, an autonomous technology research agent. Your purpose is to expand understanding through evidence-based analysis.

Guidelines for your comment:
- Be concise and substantive (2-4 paragraphs)
- Add value: share a research perspective, identify a gap, or challenge an assumption
- Be evidence-aware: reference what evidence supports or contradicts claims
- Be intellectually honest: acknowledge uncertainty when appropriate
- Stay on topic: engage with the specific claims in the post
- Do NOT be generic or overly agreeable
- Do NOT include your own research URLs or self-promote
- Sign with: — Aqevryn";

        var userPrompt = $"A post on Moltbook titled \"{title}\" by @{author} says:\n\n{contentPreview}\n\nWrite a thoughtful comment that contributes to this discussion from a research perspective.";

        try
        {
            var baseUrl = _settings.LlmProvider switch
            {
                "openai" => "https://api.openai.com/v1",
                "openrouter" => "https://openrouter.ai/api/v1",
                _ => "https://api.openai.com/v1"
            };
            var model = _settings.LlmModel == "gpt-5-nano" || string.IsNullOrEmpty(_settings.LlmModel) ? "gpt-4o-mini" : _settings.LlmModel;

            var payloadObj = new
            {
                model = model,
                messages = new[] {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                max_tokens = 1024,
                temperature = 0.4,
            };

            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.LlmApiKey}");
            http.DefaultRequestHeaders.Add("User-Agent", "Aqevryn/1.0");
            if (_settings.LlmProvider == "openrouter")
            {
                http.DefaultRequestHeaders.Add("HTTP-Referer", "https://github.com/aqevryn-cloud/aqevryn");
                http.DefaultRequestHeaders.Add("X-Title", "Aqevryn Research");
            }

            var httpContent = new StringContent(JsonSerializer.Serialize(payloadObj), System.Text.Encoding.UTF8, "application/json");
            var response = await http.PostAsync($"{baseUrl}/chat/completions", httpContent);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            if (!string.IsNullOrEmpty(text))
            {
                // Append signature
                text = text.TrimEnd() + "\n\n— Aqevryn";
                return text;
            }
        }
        catch { }

        return null;
    }
}

public static class MoltbookClientExtensions
{
    public static string GetApiKey(this MoltbookClient client)
    {
        // We need to access the private _apiKey field
        // This is a workaround - reflect to get it
        var field = typeof(MoltbookClient).GetField("_apiKey",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return field?.GetValue(client) as string ?? "";
    }
}