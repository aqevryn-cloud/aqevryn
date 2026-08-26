using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

public class TopicDiscoveryAgent
{
    private readonly ILogger<TopicDiscoveryAgent> _logger;
    private readonly LLMClient? _llm;

    private static readonly Dictionary<string, string[]> KeywordTopics = new()
    {
        ["artificial_intelligence"] = new[] { "ai", "artificial intelligence", "machine learning", "ml", "deep learning", "neural network", "transformer", "gpt", "llm", "large language model", "foundation model", "diffusion" },
        ["ai_agents"] = new[] { "ai agent", "agentic", "autonomous agent", "multi-agent", "agent framework", "tool use", "coding agent" },
        ["robotics"] = new[] { "robot", "robotics", "autonomous vehicle", "drone", "humanoid", "manipulation", "slam", "ros" },
        ["cybersecurity"] = new[] { "security", "cybersecurity", "vulnerability", "exploit", "malware", "ransomware", "zero-day", "threat", "encryption" },
        ["cloud_computing"] = new[] { "cloud", "aws", "azure", "gcp", "serverless", "kubernetes", "k8s", "container", "docker", "microservice", "edge computing" },
        ["distributed_systems"] = new[] { "distributed system", "consensus", "raft", "paxos", "database sharding", "distributed database", "crdt" },
        ["databases"] = new[] { "database", "sql", "nosql", "postgresql", "vector database", "pgvector", "data lake", "data warehouse" },
        ["developer_tools"] = new[] { "developer tool", "ide", "debugger", "profiler", "package manager", "build system", "ci/cd", "devops" },
        ["semiconductors"] = new[] { "semiconductor", "chip", "processor", "gpu", "tpu", "npu", "asic", "fpga", "transistor", "nanometer" },
        ["quantum_computing"] = new[] { "quantum", "qubit", "quantum computing", "quantum error correction" },
        ["networking"] = new[] { "network", "networking", "5g", "6g", "wifi", "mesh network", "software-defined networking" },
        ["ar_vr"] = new[] { "augmented reality", "virtual reality", "mixed reality", "ar", "vr", "spatial computing", "metaverse" },
        ["energy_technology"] = new[] { "energy", "battery", "solar", "renewable", "nuclear", "fusion", "power grid" },
        ["web_technologies"] = new[] { "web", "javascript", "typescript", "wasm", "webassembly", "react", "nextjs", "browser", "webgpu" },
        ["operating_systems"] = new[] { "operating system", "linux", "kernel", "rtos", "unix", "filesystem", "scheduler" },
        ["emerging_technologies"] = new[] { "emerging tech", "breakthrough", "innovation", "discovery", "lab", "prototype", "moonshot" },
    };

    public TopicDiscoveryAgent(LLMClient? llm = null, ILogger<TopicDiscoveryAgent>? logger = null)
    {
        _llm = llm;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TopicDiscoveryAgent>.Instance;
    }

    public async Task<List<TopicDiscoveryResult>> DiscoverAsync(List<Dictionary<string, object?>> articles)
    {
        _logger.LogInformation("Discovering topics from {Count} articles", articles.Count);
        if (articles.Count == 0) return new();

        var topicArticles = new Dictionary<string, List<Dictionary<string, object?>>>();

        foreach (var article in articles)
        {
            var text = GetArticleText(article).ToLowerInvariant();
            foreach (var (category, keywords) in KeywordTopics)
            {
                foreach (var keyword in keywords)
                {
                    if (text.Contains(keyword))
                    {
                        if (!topicArticles.ContainsKey(category))
                            topicArticles[category] = new();
                        topicArticles[category].Add(article);
                        break;
                    }
                }
            }
        }

        var results = new List<TopicDiscoveryResult>();
        foreach (var (category, matched) in topicArticles)
        {
            if (matched.Count < 2) continue;
            var topicName = category.Replace("_", " ");

            var evidence = matched.Select(a => a.GetValueOrDefault("title")?.ToString() ?? "").Where(t => !string.IsNullOrEmpty(t)).Take(10).ToList();
            var sourceIds = matched.Select(a => a.GetValueOrDefault("id")?.ToString() ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList();

            var relatedTags = new HashSet<string>();
            foreach (var a in matched)
            {
                if (a.GetValueOrDefault("tags") is List<object> tags)
                    foreach (var tag in tags) relatedTags.Add(tag.ToString() ?? "");
            }

            results.Add(new TopicDiscoveryResult
            {
                Topic = topicName,
                Summary = $"Found {matched.Count} articles related to {topicName}.",
                WhyTrending = $"Detected across {matched.Count} independent sources",
                Evidence = evidence,
                RelatedTopics = relatedTags.Take(10).ToList(),
                SourceIds = sourceIds,
                Category = category,
            });
        }

        results = results.OrderByDescending(r => r.Evidence.Count).ToList();
        _logger.LogInformation("Discovered {Count} topics", results.Count);
        return results;
    }

    private static string GetArticleText(Dictionary<string, object?> article)
    {
        var parts = new[]
        {
            article.GetValueOrDefault("title")?.ToString() ?? "",
            article.GetValueOrDefault("summary")?.ToString() ?? "",
            article.GetValueOrDefault("content")?.ToString() ?? "",
        };
        return string.Join(" ", parts);
    }
}