using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aqevryn.Config;

namespace Aqevryn.Api;

/// <summary>
/// Full-featured web dashboard for Aqevryn.
/// Shows articles, topics, pipeline activity, and system status.
/// </summary>
public class WebDashboard
{
    private readonly int _port;
    private readonly string _version;

    public WebDashboard(int port = 9888, string version = "0.1.0")
    {
        _port = port;
        _version = version;
    }

    public async Task StartAsync()
    {
        System.Net.Sockets.TcpListener? tcpListener = null;
        
        // Try different ports starting from the configured one
        for (int port = _port; port < _port + 10; port++)
        {
            try
            {
                tcpListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                tcpListener.Start();
                Console.WriteLine($"Aqevryn Web Dashboard: http://localhost:{port}/");
                Console.WriteLine("Press Ctrl+C to stop.");
                break;
            }
            catch
            {
                tcpListener = null;
                continue;
            }
        }

        if (tcpListener == null)
        {
            Console.Error.WriteLine("Failed to start web server on any port.");
            return;
        }

        while (true)
        {
            try
            {
                var client = await tcpListener.AcceptTcpClientAsync();
                _ = HandleTcpClientAsync(client);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Web server error: {ex.Message}");
            }
        }
    }

    private async Task HandleTcpClientAsync(System.Net.Sockets.TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new System.IO.StreamReader(stream))
        {
            try
            {
                var requestLine = await reader.ReadLineAsync();
                if (requestLine == null) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;

                var method = parts[0];
                var path = parts[1].ToLower();

                // Read headers
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.Length == 0) break;
                }

                // Handle the request
                var (statusCode, contentType, content) = await GetResponseAsync(method, path);

                // Build HTTP response
                var response = $"HTTP/1.1 {statusCode} {(statusCode == 200 ? "OK" : statusCode == 404 ? "Not Found" : "Error")}\r\n" +
                              $"Content-Type: {contentType}\r\n" +
                              $"Content-Length: {System.Text.Encoding.UTF8.GetByteCount(content)}\r\n" +
                              $"Access-Control-Allow-Origin: *\r\n" +
                              $"Connection: close\r\n" +
                              $"\r\n" +
                              $"{content}";

                var bytes = System.Text.Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(bytes);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Request handler error: {ex.Message}");
            }
        }
    }

    private async Task<(int, string, string)> GetResponseAsync(string method, string path)
    {
        try
        {
            return path switch
            {
                "/api/health" => (200, "application/json", ToJson(GetHealth())),
                "/api/status" => (200, "application/json", ToJson(GetStatus())),
                "/api/articles" => (200, "application/json", ToJson(GetArticles())),
                "/api/topics" => (200, "application/json", ToJson(GetTopics())),
                "/api/activity" => (200, "application/json", ToJson(GetActivity())),
                "/api/dashboard" => (200, "application/json", ToJson(GetDashboardData())),
                "/" or "/index.html" => (200, "text/html; charset=utf-8", GetIndexHtml()),
                "/articles" or "/articles.html" => (200, "text/html; charset=utf-8", GetArticlesHtml()),
                "/dashboard" or "/dashboard.html" => (200, "text/html; charset=utf-8", GetDashboardHtml()),
                "/api-docs" or "/api.html" => (200, "text/html; charset=utf-8", GetApiDocsHtml()),
                _ => (404, "text/html; charset=utf-8", GetErrorHtml(404, "Page not found")),
            };
        }
        catch (Exception ex)
        {
            return (500, "application/json", ToJson(new { error = ex.Message }));
        }
    }

    private static string ToJson(object data)
    {
        return System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    // ===== API DATA =====

    private object GetHealth() => new
    {
        status = "ok",
        version = _version,
        uptime_seconds = 0,
        timestamp = DateTime.UtcNow.ToString("o")
    };

    private object GetStatus() => new
    {
        application = new { name = "Aqevryn", version = _version },
        sources = new { rss = "ok", arxiv = "ok", hackernews = "ok", reddit = "ok", github = "error" },
        llm = new { provider = Environment.GetEnvironmentVariable("LLM_PROVIDER") ?? "not configured" },
    };

    private object GetDashboardData()
    {
        var summary = Common.ActivityRegistry.GetSummary();
        var recent = Common.ActivityRegistry.GetRecent(20);
        return new
        {
            summary = new
            {
                totalRuns = summary.TotalRuns,
                runsToday = summary.RunsToday,
                lastRunTime = summary.LastRunTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                lastRunStatus = summary.LastRunStatus,
                lastRunDuration = summary.LastRunDuration?.TotalSeconds,
                articlesCollected = summary.ArticlesCollectedToday,
                topicsDiscovered = summary.TopicsDiscoveredToday,
                articlesPublished = summary.ArticlesPublishedToday,
                recentErrors = summary.RecentErrors,
            },
            recentActivity = recent.Select(l => new
            {
                agent = l.AgentName,
                stage = l.Stage,
                status = l.Status,
                input = l.InputSummary,
                output = l.OutputSummary,
                time = l.StartedAt.ToString("HH:mm:ss"),
                duration = l.Duration?.TotalSeconds,
            }),
            collectedArticles = GetCollectedArticles().Count,
            collectedTopics = GetCollectedTopics().Count,
        };
    }

    private List<object> GetArticles()
    {
        var articles = GetCollectedArticles();
        return articles.Select(a => (object)new
        {
            title = a.GetValueOrDefault("title") ?? "",
            url = a.GetValueOrDefault("url") ?? "",
            source = a.GetValueOrDefault("source_name") ?? "",
            sourceType = a.GetValueOrDefault("source_type") ?? "",
            category = a.GetValueOrDefault("category") ?? "",
            summary = a.GetValueOrDefault("summary")?.ToString()?.Length > 200
                ? a.GetValueOrDefault("summary")?.ToString()?[..200] + "..."
                : a.GetValueOrDefault("summary") ?? "",
        }).ToList();
    }

    private List<object> GetTopics()
    {
        var topics = GetCollectedTopics();
        return topics.Select(t => (object)new
        {
            topic = t.GetValueOrDefault("topic") ?? "",
            summary = t.GetValueOrDefault("summary") ?? "",
            category = t.GetValueOrDefault("category") ?? "",
            evidenceCount = (t.GetValueOrDefault("evidence") as List<object>)?.Count ?? 0,
        }).ToList();
    }

    private List<object> GetActivity()
    {
        var logs = Common.ActivityRegistry.GetRecent(50);
        return logs.Select(l => (object)new
        {
            agent = l.AgentName,
            stage = l.Stage,
            status = l.Status,
            input = l.InputSummary,
            output = l.OutputSummary,
            error = l.Error,
            time = l.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            duration = l.Duration?.TotalSeconds,
        }).ToList();
    }

    // ===== DATA LOADING =====

    private List<Dictionary<string, object?>> GetCollectedArticles()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "webdata", "pipeline_data.json");
        if (!File.Exists(dataPath)) return new();

        try
        {
            var json = File.ReadAllText(dataPath);
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("articles", out var articlesEl))
                return new();

            var result = new List<Dictionary<string, object?>>();
            foreach (var a in articlesEl.EnumerateArray())
            {
                result.Add(new Dictionary<string, object?>
                {
                    ["title"] = a.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    ["url"] = a.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                    ["source_name"] = a.TryGetProperty("source_name", out var sn) ? sn.GetString() ?? "" : "",
                    ["source_type"] = a.TryGetProperty("source_type", out var st) ? st.GetString() ?? "" : "",
                    ["category"] = a.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "",
                    ["summary"] = a.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
                });
            }
            return result;
        }
        catch { return new(); }
    }

    private List<Dictionary<string, object?>> GetCollectedTopics()
    {
        var dataPath = Path.Combine(AppContext.BaseDirectory, "webdata", "pipeline_data.json");
        if (!File.Exists(dataPath)) return new();

        try
        {
            var json = File.ReadAllText(dataPath);
            var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("topics", out var topicsEl))
                return new();

            var result = new List<Dictionary<string, object?>>();
            foreach (var t in topicsEl.EnumerateArray())
            {
                result.Add(new Dictionary<string, object?>
                {
                    ["topic"] = t.TryGetProperty("topic", out var tp) ? tp.GetString() ?? "" : "",
                    ["summary"] = t.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
                    ["category"] = t.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "",
                    ["evidence"] = new List<object>(),
                });
            }
            return result;
        }
        catch { return new(); }
    }

    // ===== HTML PAGES =====

    private string GetIndexHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Aqevryn — Research Dashboard</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 16px; line-height: 1.6; color: #1f2328; background: #f6f8fa; }
        .container { max-width: 1200px; margin: 0 auto; padding: 20px; }
        header { background: #24292f; color: white; padding: 16px 0; }
        header .container { display: flex; align-items: center; justify-content: space-between; }
        header h1 { font-size: 1.4em; color: white; }
        header nav a { color: #8b949e; text-decoration: none; margin-left: 20px; }
        header nav a:hover { color: white; }
        .cards { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: 16px; margin: 20px 0; }
        .card { background: white; border: 1px solid #d0d7de; border-radius: 6px; padding: 20px; }
        .card h3 { font-size: 0.9em; color: #656d76; text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 8px; }
        .card .value { font-size: 2em; font-weight: 600; }
        .card .value.green { color: #2da44e; }
        .card .value.yellow { color: #d4a72c; }
        .card .value.red { color: #cf222e; }
        table { width: 100%; border-collapse: collapse; background: white; border: 1px solid #d0d7de; border-radius: 6px; }
        th, td { padding: 12px 16px; text-align: left; border-bottom: 1px solid #d0d7de; }
        th { background: #f6f8fa; font-weight: 600; font-size: 0.9em; color: #656d76; }
        .status-badge { display: inline-block; padding: 2px 8px; border-radius: 12px; font-size: 0.85em; font-weight: 500; }
        .status-completed { background: #dafbe1; color: #116329; }
        .status-running { background: #ddf4ff; color: #0969da; }
        .status-failed { background: #ffeef0; color: #cf222e; }
        .section { margin: 24px 0; }
        .section h2 { font-size: 1.3em; margin-bottom: 12px; }
        .btn { display: inline-block; padding: 8px 16px; background: #2da44e; color: white; border-radius: 6px; text-decoration: none; font-size: 0.9em; margin-right: 8px; }
        .btn:hover { background: #218838; }
        .btn-outline { background: transparent; border: 1px solid #d0d7de; color: #1f2328; }
        .btn-outline:hover { background: #f6f8fa; }
        .error { color: #cf222e; background: #ffeef0; padding: 12px; border-radius: 6px; margin: 8px 0; }
        .empty { color: #656d76; padding: 40px; text-align: center; }
        @media (max-width: 600px) { .container { padding: 12px; } .cards { grid-template-columns: 1fr; } }
    </style>
</head>
<body>
    <header>
        <div class=""container"">
            <h1>Aqevryn Research</h1>
            <nav>
                <a href=""/"">Dashboard</a>
                <a href=""/articles"">Articles</a>
                <a href=""/dashboard"">Activity</a>
                <a href=""/api-docs"">API</a>
            </nav>
        </div>
    </header>
    <div class=""container"">
        <div class=""cards"" id=""summary-cards"">
            <div class=""card""><h3>Articles</h3><div class=""value"">—</div></div>
            <div class=""card""><h3>Topics</h3><div class=""value"">—</div></div>
            <div class=""card""><h3>Runs Today</h3><div class=""value"">—</div></div>
            <div class=""card""><h3>Status</h3><div class=""value"">—</div></div>
        </div>

        <div class=""section"">
            <h2>Recent Activity</h2>
            <table>
                <thead><tr><th>Time</th><th>Agent</th><th>Stage</th><th>Status</th><th>Output</th></tr></thead>
                <tbody id=""activity-table"">
                    <tr><td colspan=""5"" class=""empty"">Loading...</td></tr>
                </tbody>
            </table>
        </div>
    </div>
    <script>
        async function loadDashboard() {
            try {
                const resp = await fetch('/api/dashboard');
                const data = await resp.json();
                
                // Update cards
                const cards = document.getElementById('summary-cards').children;
                cards[0].querySelector('.value').textContent = data.collectedArticles || '0';
                cards[1].querySelector('.value').textContent = data.collectedTopics || '0';
                cards[2].querySelector('.value').textContent = data.summary.runsToday || '0';
                const statusEl = cards[3].querySelector('.value');
                statusEl.textContent = data.summary.lastRunStatus || 'No runs';
                statusEl.className = 'value ' + (data.summary.lastRunStatus === 'COMPLETED' ? 'green' : 'yellow');

                // Update activity table
                const table = document.getElementById('activity-table');
                if (data.recentActivity && data.recentActivity.length > 0) {
                    table.innerHTML = data.recentActivity.map(a => `
                        <tr>
                            <td>${a.time}</td>
                            <td>${a.agent}</td>
                            <td>${a.stage}</td>
                            <td><span class=""status-badge status-${a.status.toLowerCase()}"">${a.status}</span></td>
                            <td>${a.output || ''}</td>
                        </tr>
                    `).join('');
                } else {
                    table.innerHTML = '<tr><td colspan=""5"" class=""empty"">No activity yet. Run the pipeline first.</td></tr>';
                }
            } catch(e) {
                console.error('Failed to load dashboard:', e);
            }
        }
        loadDashboard();
        setInterval(loadDashboard, 5000);
    </script>
</body>
</html>";
    }

    private string GetArticlesHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Aqevryn — Articles</title>
    <link rel=""stylesheet"" href=""/style.css"">
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 16px; line-height: 1.6; color: #1f2328; background: #f6f8fa; }
        .container { max-width: 1200px; margin: 0 auto; padding: 20px; }
        header { background: #24292f; color: white; padding: 16px 0; }
        header .container { display: flex; align-items: center; justify-content: space-between; }
        header h1 { font-size: 1.4em; color: white; }
        header nav a { color: #8b949e; text-decoration: none; margin-left: 20px; }
        header nav a:hover { color: white; }
        .article-card { background: white; border: 1px solid #d0d7de; border-radius: 6px; padding: 20px; margin-bottom: 12px; }
        .article-card h3 { margin-bottom: 8px; }
        .article-card .meta { color: #656d76; font-size: 0.9em; margin-bottom: 8px; }
        .article-card .meta span { margin-right: 16px; }
        .article-card p { color: #656d76; }
        .badge { display: inline-block; padding: 1px 6px; border-radius: 8px; font-size: 0.8em; background: #ddf4ff; color: #0969da; }
        .empty { color: #656d76; padding: 60px; text-align: center; }
        .tabs { display: flex; gap: 4px; margin-bottom: 20px; }
        .tab { padding: 8px 16px; background: white; border: 1px solid #d0d7de; border-radius: 6px 6px 0 0; cursor: pointer; }
        .tab.active { background: #24292f; color: white; border-color: #24292f; }
        .tab-content { display: none; }
        .tab-content.active { display: block; }
        @media (max-width: 600px) { .container { padding: 12px; } }
    </style>
</head>
<body>
    <header>
        <div class=""container"">
            <h1>Aqevryn Research</h1>
            <nav>
                <a href=""/"">Dashboard</a>
                <a href=""/articles"">Articles</a>
                <a href=""/dashboard"">Activity</a>
                <a href=""/api-docs"">API</a>
            </nav>
        </div>
    </header>
    <div class=""container"">
        <div class=""tabs"">
            <div class=""tab active"" onclick=""switchTab('articles')"">Articles</div>
            <div class=""tab"" onclick=""switchTab('topics')"">Topics</div>
        </div>
        
        <div id=""tab-articles"" class=""tab-content active"">
            <div id=""articles-list""><div class=""empty"">Loading articles...</div></div>
        </div>
        
        <div id=""tab-topics"" class=""tab-content"">
            <div id=""topics-list""><div class=""empty"">Loading topics...</div></div>
        </div>
    </div>
    <script>
        function switchTab(name) {
            document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
            document.querySelectorAll('.tab-content').forEach(t => t.classList.remove('active'));
            document.querySelector(`.tab[onclick*='${name}']`).classList.add('active');
            document.getElementById(`tab-${name}`).classList.add('active');
        }

        async function loadArticles() {
            try {
                const resp = await fetch('/api/articles');
                const articles = await resp.json();
                const list = document.getElementById('articles-list');
                if (articles.length === 0) {
                    list.innerHTML = '<div class=""empty"">No articles collected yet. Run <code>aqevryn run</code> first.</div>';
                    return;
                }
                list.innerHTML = articles.map(a => `
                    <div class=""article-card"">
                        <h3>${a.title}</h3>
                        <div class=""meta"">
                            <span>Source: ${a.source}</span>
                            <span class=""badge"">${a.sourceType}</span>
                            <span>${a.category}</span>
                        </div>
                        <p>${a.summary}</p>
                    </div>
                `).join('');
            } catch(e) { console.error(e); }
        }

        async function loadTopics() {
            try {
                const resp = await fetch('/api/topics');
                const topics = await resp.json();
                const list = document.getElementById('topics-list');
                if (topics.length === 0) {
                    list.innerHTML = '<div class=""empty"">No topics discovered yet.</div>';
                    return;
                }
                list.innerHTML = topics.map(t => `
                    <div class=""article-card"">
                        <h3>${t.topic}</h3>
                        <div class=""meta"">
                            <span class=""badge"">${t.category || 'technology'}</span>
                            <span>${t.evidenceCount} articles</span>
                        </div>
                        <p>${t.summary}</p>
                    </div>
                `).join('');
            } catch(e) { console.error(e); }
        }

        loadArticles();
        loadTopics();
    </script>
</body>
</html>";
    }

    private string GetDashboardHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Aqevryn — Activity Log</title>
    <link rel=""stylesheet"" href=""/style.css"">
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 14px; line-height: 1.5; color: #1f2328; background: #f6f8fa; }
        .container { max-width: 1200px; margin: 0 auto; padding: 20px; }
        header { background: #24292f; color: white; padding: 16px 0; }
        header .container { display: flex; align-items: center; justify-content: space-between; }
        header h1 { font-size: 1.4em; color: white; }
        header nav a { color: #8b949e; text-decoration: none; margin-left: 20px; }
        header nav a:hover { color: white; }
        .log-entry { padding: 8px 12px; border-bottom: 1px solid #e1e4e8; font-family: 'SFMono-Regular', Consolas, monospace; font-size: 0.9em; }
        .log-entry:hover { background: #f6f8fa; }
        .log-entry .time { color: #656d76; width: 80px; display: inline-block; }
        .log-entry .icon { width: 20px; display: inline-block; }
        .log-entry .agent { color: #0969da; }
        .log-entry .output { color: #656d76; margin-left: 100px; }
        .empty { color: #656d76; padding: 60px; text-align: center; }
        #auto-refresh { margin: 12px 0; }
        @media (max-width: 600px) { .container { padding: 12px; } .log-entry .time { width: auto; } }
    </style>
</head>
<body>
    <header>
        <div class=""container"">
            <h1>Aqevryn Research</h1>
            <nav>
                <a href=""/"">Dashboard</a>
                <a href=""/articles"">Articles</a>
                <a href=""/dashboard"">Activity</a>
                <a href=""/api-docs"">API</a>
            </nav>
        </div>
    </header>
    <div class=""container"">
        <h2>Agent Activity Log</h2>
        <p id=""auto-refresh"">Auto-refreshing every 5 seconds</p>
        <div id=""activity-log""><div class=""empty"">Loading...</div></div>
    </div>
    <script>
        async function loadLog() {
            try {
                const resp = await fetch('/api/activity');
                const logs = await resp.json();
                const logDiv = document.getElementById('activity-log');
                if (logs.length === 0) {
                    logDiv.innerHTML = '<div class=""empty"">No activity yet. Run <code>aqevryn run</code> first.</div>';
                    return;
                }
                logDiv.innerHTML = logs.map(l => {
                    const icon = l.status === 'COMPLETED' ? '✅' : l.status === 'FAILED' ? '❌' : '🔄';
                    return `<div class=""log-entry"">
                        <span class=""icon"">${icon}</span>
                        <span class=""time"">${l.time}</span>
                        <span class=""agent"">${l.agent}.${l.stage}</span>
                        ${l.duration ? '[' + l.duration.toFixed(1) + 's]' : ''}
                        ${l.error ? '<span style=""color:#cf222e"">ERROR: ' + l.error + '</span>' : ''}
                        <div class=""output"">${l.output || l.input || ''}</div>
                    </div>`;
                }).join('');
            } catch(e) { console.error(e); }
        }
        loadLog();
        setInterval(loadLog, 5000);
    </script>
</body>
</html>";
    }

    private string GetApiDocsHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Aqevryn — API Documentation</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size: 16px; line-height: 1.6; color: #1f2328; background: #f6f8fa; }
        .container { max-width: 800px; margin: 0 auto; padding: 20px; }
        header { background: #24292f; color: white; padding: 16px 0; }
        header .container { display: flex; align-items: center; justify-content: space-between; }
        header h1 { font-size: 1.4em; color: white; }
        header nav a { color: #8b949e; text-decoration: none; margin-left: 20px; }
        header nav a:hover { color: white; }
        h1 { margin-bottom: 20px; }
        .endpoint { background: white; border: 1px solid #d0d7de; border-radius: 6px; padding: 20px; margin-bottom: 16px; }
        .endpoint .method { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 0.85em; font-weight: 600; color: white; background: #2da44e; margin-right: 8px; }
        .endpoint .path { font-family: monospace; font-size: 1.1em; }
        .endpoint p { margin: 8px 0; color: #656d76; }
        .endpoint code { background: #f6f8fa; padding: 2px 6px; border-radius: 3px; font-family: monospace; }
        pre { background: #f6f8fa; padding: 16px; border-radius: 6px; overflow-x: auto; font-size: 0.9em; }
    </style>
</head>
<body>
    <header>
        <div class=""container"">
            <h1>Aqevryn Research</h1>
            <nav>
                <a href=""/"">Dashboard</a>
                <a href=""/articles"">Articles</a>
                <a href=""/dashboard"">Activity</a>
                <a href=""/api-docs"">API</a>
            </nav>
        </div>
    </header>
    <div class=""container"">
        <h1>API Documentation</h1>
        <p>All endpoints return JSON. The web dashboard consumes these same APIs.</p>

        <div class=""endpoint"">
            <span class=""method"">GET</span><span class=""path"">/api/health</span>
            <p>Health check endpoint. Returns application status and version.</p>
            <pre>{""status"": ""ok"", ""version"": ""0.1.0""}</pre>
        </div>

        <div class=""endpoint"">
            <span class=""method"">GET</span><span class=""path"">/api/dashboard</span>
            <p>Dashboard summary data including pipeline stats, recent activity, article and topic counts.</p>
        </div>

        <div class=""endpoint"">
            <span class=""method"">GET</span><span class=""path"">/api/articles</span>
            <p>List of collected articles from the most recent pipeline run.</p>
        </div>

        <div class=""endpoint"">
            <span class=""method"">GET</span><span class=""path"">/api/topics</span>
            <p>List of discovered technology topics from the most recent pipeline run.</p>
        </div>

        <div class=""endpoint"">
            <span class=""method"">GET</span><span class=""path"">/api/activity</span>
            <p>Recent agent activity log entries.</p>
        </div>

        <div class=""endpoint"">
            <span class=""method"">GET</span><span class=""path"">/api/status</span>
            <p>Source status and system configuration.</p>
        </div>

        <h2>Web Pages</h2>
        <div class=""endpoint"">
            <span class=""method"">GET</span><span class=""path"">/</span>
            <p>Dashboard home page with summary cards and recent activity.</p>
        </div>
        <div class=""endpoint"">
            <span class=""method"">GET</span><span class=""path"">/articles</span>
            <p>Articles and topics browser with tabs.</p>
        </div>
        <div class=""endpoint"">
            <span class=""method"">GET</span><span class=""path"">/dashboard</span>
            <p>Full agent activity log with auto-refresh.</p>
        </div>
    </div>
</body>
</html>";
    }

    private string GetErrorHtml(int code, string message)
    {
        return $@"<!DOCTYPE html>
<html><head><meta charset=""UTF-8""><title>{code} — Aqevryn</title>
<style>body {{ font-family: -apple-system, sans-serif; text-align: center; padding: 80px 20px; }}</style>
</head><body>
<h1>{code}</h1><p>{message}</p>
<a href=""/"">← Back to Dashboard</a>
</body></html>";
    }

    public static async Task RunAsync(int port = 9888)
    {
        var dashboard = new WebDashboard(port: port);
        await dashboard.StartAsync();
    }
}