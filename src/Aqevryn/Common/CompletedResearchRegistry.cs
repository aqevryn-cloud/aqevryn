using System.Text.Json;

namespace Aqevryn.Common;

/// <summary>
/// Tracks completed research topics so the pipeline doesn't repeat them.
/// Persists to a JSON file so data survives between runs.
/// </summary>
public class CompletedResearchRegistry
{
    private static readonly string StoragePath = Path.Combine(AppContext.BaseDirectory, "completed_research.json");
    private static List<CompletedResearch> _completed = new();
    private static bool _loaded = false;
    private static readonly object _lock = new();

    public static void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;
            try
            {
                if (File.Exists(StoragePath))
                {
                    var json = File.ReadAllText(StoragePath);
                    var items = JsonSerializer.Deserialize<List<CompletedResearch>>(json);
                    if (items != null) _completed = items;
                }
            }
            catch { }
            _loaded = true;
        }
    }

    private static void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_completed, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StoragePath, json);
        }
        catch { }
    }

    /// <summary>Record a completed research project (minimum editorial score of 60 required).</summary>
    public static void Record(string topic, string researchQuestion, double finalScore,
        double editorialScore, string? prUrl, string? articleTitle, int articleCount, int findingCount, string? slug = null)
    {
        // Don't record low-quality research as completed
        if (editorialScore < 60)
        {
            return;
        }
        EnsureLoaded();
        lock (_lock)
        {
            // Remove any previous entry for same topic to keep only latest
            _completed.RemoveAll(r => r.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase));
            _completed.Add(new CompletedResearch
            {
                Topic = topic,
                Slug = slug ?? topic.ToLower().Replace(" ", "-").Replace("/", "-"),
                ResearchQuestion = researchQuestion,
                FinalScore = finalScore,
                EditorialScore = editorialScore,
                PrUrl = prUrl,
                ArticleTitle = articleTitle,
                ArticleCount = articleCount,
                FindingCount = findingCount,
                CompletedAt = DateTime.UtcNow,
            });
            Save();
        }
    }

    /// <summary>Check if a topic has already been researched.</summary>
    public static bool IsAlreadyResearched(string topic)
    {
        EnsureLoaded();
        lock (_lock)
        {
            return _completed.Any(r => r.Topic.Equals(topic, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Get all completed research, newest first.</summary>
    public static List<CompletedResearch> GetAll()
    {
        EnsureLoaded();
        lock (_lock)
        {
            return _completed.OrderByDescending(r => r.CompletedAt).ToList();
        }
    }

    /// <summary>Get the count of completed research topics.</summary>
    public static int Count()
    {
        EnsureLoaded();
        lock (_lock) { return _completed.Count; }
    }
}

public class CompletedResearch
{
    public string Topic { get; set; } = "";
    public string ResearchQuestion { get; set; } = "";
    public double FinalScore { get; set; }
    public double EditorialScore { get; set; }
    public string? PrUrl { get; set; }
    public string? ArticleTitle { get; set; }
    public string Slug { get; set; } = "";
    public int ArticleCount { get; set; }
    public int FindingCount { get; set; }
    public DateTime CompletedAt { get; set; }
}