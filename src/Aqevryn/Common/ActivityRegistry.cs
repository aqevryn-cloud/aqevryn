using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aqevryn.Common;

/// <summary>
/// Records an agent's activity. Used to track what the pipeline is doing.
/// Each agent logs its operation with input, output, timing, and results.
/// </summary>
public class AgentActivityLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string AgentName { get; set; } = "";
    public string Stage { get; set; } = "";
    public string Status { get; set; } = "RUNNING"; // RUNNING, COMPLETED, FAILED
    public string? InputSummary { get; set; }
    public string? OutputSummary { get; set; }
    public string? Details { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}

/// <summary>
/// Central registry of all agent activities. The pipeline and agents
/// log their activity here, and the dashboard/watch commands read from it.
/// Persists to a JSON file so data survives between CLI invocations.
/// </summary>
public class ActivityRegistry
{
    private static readonly List<AgentActivityLog> _logs = new();
    private static readonly object _lock = new();
    private static readonly string _storagePath = Path.Combine(AppContext.BaseDirectory, "activity_log.json");
    private static bool _loaded = false;

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;
            try
            {
                if (File.Exists(_storagePath))
                {
                    var json = File.ReadAllText(_storagePath);
                    var items = JsonSerializer.Deserialize<List<AgentActivityLog>>(json);
                    if (items != null)
                    {
                        _logs.Clear();
                        _logs.AddRange(items);
                    }
                }
            }
            catch { /* ignore read errors */ }
            _loaded = true;
        }
    }

    private static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_logs.OrderByDescending(l => l.StartedAt).Take(500).ToList(),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_storagePath, json);
        }
        catch { /* ignore write errors */ }
    }

    public static void Log(AgentActivityLog log)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _logs.Add(log);
            if (_logs.Count > 1000)
                _logs.RemoveRange(0, _logs.Count - 1000);
            Save();
        }
    }

    public static void LogActivity(string agentName, string stage, string status,
        string? inputSummary = null, string? outputSummary = null, string? details = null, string? error = null)
    {
        var log = new AgentActivityLog
        {
            AgentName = agentName,
            Stage = stage,
            Status = status,
            InputSummary = inputSummary,
            OutputSummary = outputSummary,
            Details = details,
            Error = error,
            CompletedAt = status != "RUNNING" ? DateTime.UtcNow : null,
        };
        Log(log);
    }

    public static void LogStart(string agentName, string stage, string? inputSummary = null)
    {
        Log(new AgentActivityLog
        {
            AgentName = agentName,
            Stage = stage,
            Status = "RUNNING",
            InputSummary = inputSummary,
        });
    }

    public static void LogComplete(string agentName, string stage, string? outputSummary = null, string? details = null)
    {
        var log = new AgentActivityLog
        {
            AgentName = agentName,
            Stage = stage,
            Status = "COMPLETED",
            OutputSummary = outputSummary,
            Details = details,
            CompletedAt = DateTime.UtcNow,
        };
        Log(log);
    }

    public static void LogError(string agentName, string stage, string error)
    {
        var log = new AgentActivityLog
        {
            AgentName = agentName,
            Stage = stage,
            Status = "FAILED",
            Error = error,
            CompletedAt = DateTime.UtcNow,
        };
        Log(log);
    }

    public static List<AgentActivityLog> GetRecent(int count = 50)
    {
        EnsureLoaded();
        lock (_lock)
        {
            return _logs.OrderByDescending(l => l.StartedAt).Take(count).ToList();
        }
    }

    public static List<AgentActivityLog> GetByStage(string stage, int count = 20)
    {
        lock (_lock)
        {
            return _logs.Where(l => l.Stage == stage).OrderByDescending(l => l.StartedAt).Take(count).ToList();
        }
    }

    public static PipelineSummary GetSummary()
    {
        EnsureLoaded();
        lock (_lock)
        {
            var lastRun = _logs.LastOrDefault(l => l.Status == "COMPLETED");
            var recent = _logs.Where(l => l.StartedAt > DateTime.UtcNow.AddHours(-24)).ToList();

            return new PipelineSummary
            {
                TotalRuns = recent.Count(l => l.Stage == "discover" || l.Stage == "pipeline"),
                LastRunTime = lastRun?.StartedAt,
                LastRunStatus = lastRun?.Status,
                LastRunDuration = lastRun?.Duration,
                LastRunOutput = lastRun?.OutputSummary,
                RunsToday = recent.Count(l => l.Stage == "discover" && l.Status == "COMPLETED"),
                ArticlesCollectedToday = recent
                    .Where(l => l.Stage == "discover" && l.Status == "COMPLETED" && l.OutputSummary != null)
                    .Sum(l =>
                    {
                        var parts = l.OutputSummary!.Split(' ');
                        return int.TryParse(parts.FirstOrDefault(), out var n) ? n : 0;
                    }),
                TopicsDiscoveredToday = recent
                    .Where(l => l.Stage == "topic_discovery" && l.Status == "COMPLETED" && l.OutputSummary != null)
                    .Sum(l =>
                    {
                        var parts = l.OutputSummary!.Split(' ');
                        return int.TryParse(parts.FirstOrDefault(), out var n) ? n : 0;
                    }),
                ArticlesPublishedToday = recent.Count(l => l.Stage == "publish" && l.Status == "COMPLETED"),
                RecentErrors = recent.Where(l => l.Status == "FAILED").Select(l => $"{l.AgentName}: {l.Error}").ToList(),
            };
        }
    }

    public static void Clear()
    {
        lock (_lock) { _logs.Clear(); }
    }
}

public class PipelineSummary
{
    public int TotalRuns { get; set; }
    public DateTime? LastRunTime { get; set; }
    public string? LastRunStatus { get; set; }
    public TimeSpan? LastRunDuration { get; set; }
    public string? LastRunOutput { get; set; }
    public int RunsToday { get; set; }
    public int ArticlesCollectedToday { get; set; }
    public int TopicsDiscoveredToday { get; set; }
    public int ArticlesPublishedToday { get; set; }
    public List<string> RecentErrors { get; set; } = new();
}