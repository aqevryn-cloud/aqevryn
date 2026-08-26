using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

public class TopicClusteringAgent
{
    private readonly ILogger<TopicClusteringAgent> _logger;
    private static readonly Dictionary<string, string[]> TopicAliases = new()
    {
        ["AI Agents"] = new[] { "agentic ai", "autonomous ai", "ai automation", "multi-agent systems", "ai agent", "agent framework" },
        ["Large Language Models"] = new[] { "llm", "large language model", "foundation model", "gpt", "transformer model", "language model", "llama", "claude", "gemini" },
        ["Machine Learning"] = new[] { "machine learning", "deep learning", "neural network", "transfer learning", "federated learning" },
        ["DevOps & Infrastructure"] = new[] { "devops", "ci/cd", "infrastructure as code", "terraform", "continuous integration", "platform engineering" },
        ["Cloud Computing"] = new[] { "cloud computing", "cloud native", "serverless", "aws", "azure", "gcp", "edge computing" },
        ["Container & Orchestration"] = new[] { "kubernetes", "docker", "container", "k8s", "container orchestration", "service mesh" },
        ["Cybersecurity"] = new[] { "cybersecurity", "security", "vulnerability", "exploit", "threat detection", "zero trust", "encryption" },
        ["Software Engineering"] = new[] { "software engineering", "programming", "coding", "developer tools", "ide", "debugging", "testing" },
        ["Robotics"] = new[] { "robotics", "robot", "autonomous vehicle", "humanoid", "drone", "manipulation" },
        ["Databases"] = new[] { "database", "sql", "nosql", "vector database", "postgresql", "data storage", "query engine" },
        ["Semiconductors & Hardware"] = new[] { "semiconductor", "chip", "processor", "gpu", "hardware accelerator", "asic", "fpga", "npu" },
        ["Quantum Computing"] = new[] { "quantum computing", "quantum", "qubit", "quantum error correction" },
        ["AR/VR/Spatial Computing"] = new[] { "augmented reality", "virtual reality", "mixed reality", "spatial computing", "metaverse", "ar", "vr" },
        ["Web Technologies"] = new[] { "web", "javascript", "typescript", "react", "webassembly", "browser", "web3" },
    };

    public TopicClusteringAgent(ILogger<TopicClusteringAgent>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<TopicClusteringAgent>.Instance;
    }

    public Task<List<TopicClusterResult>> ClusterAsync(List<Dictionary<string, object?>> topics)
    {
        _logger.LogInformation("Clustering {Count} topics", topics.Count);
        if (topics.Count == 0) return Task.FromResult(new List<TopicClusterResult>());

        var aliasMap = new Dictionary<string, string>();
        foreach (var (canonical, aliases) in TopicAliases)
        {
            aliasMap[canonical.ToLowerInvariant()] = canonical.ToLowerInvariant();
            foreach (var alias in aliases)
                aliasMap[alias.ToLowerInvariant()] = canonical.ToLowerInvariant();
        }

        var clusters = new Dictionary<string, TopicClusterResult>();

        foreach (var topic in topics)
        {
            var topicName = topic.GetValueOrDefault("topic")?.ToString()?.Trim() ?? "";
            var topicId = topic.GetValueOrDefault("id")?.ToString() ?? "";
            if (string.IsNullOrEmpty(topicName)) continue;

            var lower = topicName.ToLowerInvariant();
            var matched = aliasMap.GetValueOrDefault(lower) ?? lower;

            if (!clusters.ContainsKey(matched))
            {
                var display = TopicAliases.Keys.FirstOrDefault(k => k.ToLowerInvariant() == matched) ?? topicName;
                clusters[matched] = new TopicClusterResult { CanonicalTopic = display };
            }

            var cluster = clusters[matched];
            if (!cluster.MemberTopics.Contains(topicName)) cluster.MemberTopics.Add(topicName);
            if (!string.IsNullOrEmpty(topicId) && !cluster.TopicIds.Contains(topicId)) cluster.TopicIds.Add(topicId);
            if (!topicName.Equals(cluster.CanonicalTopic, StringComparison.OrdinalIgnoreCase) && !cluster.Aliases.Contains(topicName))
                cluster.Aliases.Add(topicName);
        }

        foreach (var cluster in clusters.Values)
            cluster.ArticleCount = cluster.TopicIds.Count;

        var result = clusters.Values.OrderByDescending(c => c.ArticleCount).ToList();
        _logger.LogInformation("Clustered into {Count} groups", result.Count);
        return Task.FromResult(result);
    }
}