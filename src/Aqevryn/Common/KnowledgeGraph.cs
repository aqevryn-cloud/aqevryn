using System.Text.Json;
using Aqevryn.Config;

namespace Aqevryn.Common;

/// <summary>
/// Persistent knowledge graph that stores findings, contradictions, open questions,
/// and research lineage across all projects. Implements soul.md §7, §23, §25.
/// </summary>
public class KnowledgeGraph
{
    private static readonly string _storagePath = Path.Combine(AppContext.BaseDirectory, "data", "knowledge_graph.json");
    private static List<KnowledgeEntry> _entries = new();
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
                if (File.Exists(_storagePath))
                {
                    var json = File.ReadAllText(_storagePath);
                    _entries = JsonSerializer.Deserialize<List<KnowledgeEntry>>(json) ?? new();
                }
            }
            catch { _entries = new(); }
            _loaded = true;
        }
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_storagePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(_storagePath, JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    /// <summary>Record a finding from research.</summary>
    public static void RecordFinding(string projectTopic, string claim, string findingType,
        double confidence, string? sourceUrl, string? supportingEvidence,
        string? contradictionClaim = null, string? contradictionSource = null)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _entries.Add(new KnowledgeEntry
            {
                Type = "finding",
                ProjectTopic = projectTopic,
                Claim = claim,
                FindingType = findingType,
                Confidence = confidence,
                SourceUrl = sourceUrl,
                SupportingEvidence = supportingEvidence,
                ContradictionClaim = contradictionClaim,
                ContradictionSource = contradictionSource,
                CreatedAt = DateTime.UtcNow,
            });
            Save();
        }
    }

    /// <summary>Record an identified knowledge gap.</summary>
    public static void RecordGap(string topic, string description)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _entries.Add(new KnowledgeEntry
            {
                Type = "knowledge_gap",
                ProjectTopic = topic,
                Claim = description,
                CreatedAt = DateTime.UtcNow,
            });
            Save();
        }
    }

    /// <summary>Record an open question for future research.</summary>
    public static void RecordQuestion(string topic, string question, string? sourceUrl = null)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _entries.Add(new KnowledgeEntry
            {
                Type = "open_question",
                ProjectTopic = topic,
                Claim = question,
                SourceUrl = sourceUrl,
                CreatedAt = DateTime.UtcNow,
            });
            Save();
        }
    }

    /// <summary>Record a contradiction between two claims.</summary>
    public static void RecordContradiction(string topic, string claimA, string claimB,
        string sourceA, string sourceB, string? resolution = null)
    {
        EnsureLoaded();
        lock (_lock)
        {
            _entries.Add(new KnowledgeEntry
            {
                Type = "contradiction",
                ProjectTopic = topic,
                Claim = $"Contradiction: '{claimA}' vs '{claimB}'",
                SupportingEvidence = $"Source A: {sourceA}\nSource B: {sourceB}",
                ContradictionClaim = resolution,
                CreatedAt = DateTime.UtcNow,
            });
            Save();
        }
    }

    /// <summary>Get all entries for a topic.</summary>
    public static List<KnowledgeEntry> GetForTopic(string topic)
    {
        EnsureLoaded();
        lock (_lock) { return _entries.Where(e => e.ProjectTopic.Equals(topic, StringComparison.OrdinalIgnoreCase)).ToList(); }
    }

    /// <summary>Get all open questions across all topics.</summary>
    public static List<KnowledgeEntry> GetOpenQuestions()
    {
        EnsureLoaded();
        lock (_lock) { return _entries.Where(e => e.Type == "open_question").ToList(); }
    }

    /// <summary>Get all contradictions across all topics.</summary>
    public static List<KnowledgeEntry> GetContradictions()
    {
        EnsureLoaded();
        lock (_lock) { return _entries.Where(e => e.Type == "contradiction").ToList(); }
    }

    /// <summary>Get all knowledge gaps.</summary>
    public static List<KnowledgeEntry> GetGaps()
    {
        EnsureLoaded();
        lock (_lock) { return _entries.Where(e => e.Type == "knowledge_gap").ToList(); }
    }

    /// <summary>Get the complete research lineage for a topic.</summary>
    public static ResearchLineage? GetLineage(string topic)
    {
        EnsureLoaded();
        lock (_lock)
        {
            var entries = _entries.Where(e => e.ProjectTopic.Equals(topic, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!entries.Any()) return null;
            return new ResearchLineage
            {
                Topic = topic,
                Findings = entries.Where(e => e.Type == "finding").ToList(),
                Gaps = entries.Where(e => e.Type == "knowledge_gap").ToList(),
                Questions = entries.Where(e => e.Type == "open_question").ToList(),
                Contradictions = entries.Where(e => e.Type == "contradiction").ToList(),
                LastUpdated = entries.Max(e => e.CreatedAt),
            };
        }
    }

    /// <summary>Check if there's evidence that could disprove a claim — falsifiability check.</summary>
    public static List<KnowledgeEntry> SearchDisconfirmingEvidence(string claim)
    {
        EnsureLoaded();
        lock (_lock)
        {
            var keywords = claim.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3).Select(w => w.ToLowerInvariant()).ToList();
            return _entries.Where(e =>
                keywords.Any(k => e.Claim?.ToLowerInvariant().Contains(k) == true) &&
                e.FindingType is "contradiction" or "knowledge_gap").ToList();
        }
    }
}

public class KnowledgeEntry
{
    public string Type { get; set; } = ""; // finding, knowledge_gap, open_question, contradiction
    public string ProjectTopic { get; set; } = "";
    public string Claim { get; set; } = "";
    public string FindingType { get; set; } = "research_finding";
    public double Confidence { get; set; } = 0.5;
    public string? SourceUrl { get; set; }
    public string? SupportingEvidence { get; set; }
    public string? ContradictionClaim { get; set; }
    public string? ContradictionSource { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ResearchLineage
{
    public string Topic { get; set; } = "";
    public List<KnowledgeEntry> Findings { get; set; } = new();
    public List<KnowledgeEntry> Gaps { get; set; } = new();
    public List<KnowledgeEntry> Questions { get; set; } = new();
    public List<KnowledgeEntry> Contradictions { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}