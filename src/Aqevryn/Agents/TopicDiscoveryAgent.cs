using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

public class TopicDiscoveryAgent
{
    private readonly ILogger<TopicDiscoveryAgent> _logger;
    private readonly LLMClient? _llm;

    private static readonly Dictionary<string, string[]> KeywordTopics = new()
    {
        // === TECHNOLOGY ===
        ["artificial_intelligence"] = new[] { "ai", "artificial intelligence", "machine learning", "ml", "deep learning", "neural network", "transformer", "gpt", "llm", "large language model", "foundation model", "diffusion" },
        ["ai_agents"] = new[] { "ai agent", "agentic", "autonomous agent", "multi-agent", "agent framework", "tool use", "coding agent" },
        ["robotics"] = new[] { "robot", "robotics", "autonomous vehicle", "drone", "humanoid", "manipulation", "slam", "ros" },
        ["cybersecurity"] = new[] { "security", "cybersecurity", "vulnerability", "exploit", "malware", "ransomware", "zero-day", "threat", "encryption" },
        ["cloud_computing"] = new[] { "cloud", "aws", "azure", "gcp", "serverless", "kubernetes", "container", "docker", "microservice", "edge computing" },
        ["semiconductors"] = new[] { "semiconductor", "chip", "processor", "gpu", "tpu", "npu", "asic", "fpga", "nanometer" },
        ["quantum_computing"] = new[] { "quantum", "qubit", "quantum computing", "quantum error correction" },

        // === SCIENCE ===
        ["physics"] = new[] { "physics", "particle", "quantum mechanics", "relativity", "nuclear physics", "astrophysics", "cosmology", "string theory" },
        ["biology"] = new[] { "biology", "genetics", "dna", "rna", "evolution", "cell biology", "molecular biology", "genome", "crispr" },
        ["medicine"] = new[] { "medicine", "clinical trial", "drug", "vaccine", "cancer", "disease", "therapy", "diagnosis", "treatment", "patient" },
        ["chemistry"] = new[] { "chemistry", "chemical", "compound", "molecule", "catalyst", "reaction", "material science", "polymer" },
        ["neuroscience"] = new[] { "neuroscience", "brain", "neuron", "cognitive science", "consciousness", "neuroplasticity", "neurodegenerative" },
        ["space"] = new[] { "space", "nasa", "esa", "spacex", "astronomy", "telescope", "planet", "star", "galaxy", "exoplanet", "mars", "lunar", "satellite" },

        // === ECONOMICS & FINANCE ===
        ["economics"] = new[] { "economics", "economy", "inflation", "gdp", "monetary policy", "fiscal policy", "interest rate", "central bank", "recession", "unemployment" },
        ["finance"] = new[] { "finance", "market", "stock", "bond", "investment", "trading", "banking", "asset", "portfolio", "derivative", "crypto" },
        ["trade"] = new[] { "trade", "tariff", "export", "import", "supply chain", "global trade", "sanction" },

        // === BUSINESS ===
        ["business"] = new[] { "business", "startup", "entrepreneurship", "strategy", "management", "leadership", "innovation", "corporate", "industry" },
        ["markets"] = new[] { "market", "competition", "monopoly", "antitrust", "regulation", "merger", "acquisition", "ipo" },

        // === HISTORY ===
        ["history"] = new[] { "history", "historical", "ancient", "medieval", "war", "revolution", "empire", "civilization", "archaeology", "historian" },

        // === GEOPOLITICS ===
        ["geopolitics"] = new[] { "geopolitics", "foreign policy", "diplomacy", "alliance", "conflict", "military", "intelligence", "strategy", "nato", "united nations" },
        ["international_relations"] = new[] { "international relations", "global affairs", "soft power", "geostrategic", "world order", "bilateral" },

        // === CLIMATE & ENVIRONMENT ===
        ["climate_change"] = new[] { "climate change", "global warming", "carbon", "emission", "greenhouse", "paris agreement", "net zero", "climate crisis" },
        ["environment"] = new[] { "environment", "pollution", "biodiversity", "conservation", "ecosystem", "sustainability", "renewable", "deforestation" },

        // === ENERGY ===
        ["energy"] = new[] { "energy", "fossil fuel", "renewable energy", "solar", "wind", "nuclear", "fusion", "grid", "battery", "hydrogen", "oil", "gas" },

        // === EDUCATION ===
        ["education"] = new[] { "education", "learning", "teaching", "school", "university", "curriculum", "student", "pedagogy", "online learning" },

        // === LAW ===
        ["law"] = new[] { "law", "legal", "supreme court", "constitution", "legislation", "regulation", "compliance", "privacy", "intellectual property", "copyright" },

        // === CULTURE & SOCIETY ===
        ["culture"] = new[] { "culture", "art", "music", "literature", "film", "media", "philosophy", "religion", "language" },
        ["society"] = new[] { "society", "demographics", "inequality", "poverty", "urbanization", "immigration", "social media", "public health", "welfare" },

        // === POLITICS ===
        ["politics"] = new[] { "politics", "election", "democracy", "government", "policy", "reform", "campaign", "voting", "congress", "parliament" },

        // === PSYCHOLOGY ===
        ["psychology"] = new[] { "psychology", "mental health", "behavior", "cognition", "personality", "therapy", "trauma", "depression", "anxiety" },

        // === REMAINING TECHNOLOGY (for balance) ===
        ["software_engineering"] = new[] { "software engineering", "programming", "coding", "developer", "open source", "github", "api", "framework" },
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