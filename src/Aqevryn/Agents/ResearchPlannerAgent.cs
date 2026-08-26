using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

public class ResearchPlannerAgent
{
    private readonly ILogger<ResearchPlannerAgent> _logger;
    private readonly LLMClient? _llm;

    public ResearchPlannerAgent(LLMClient? llm = null, ILogger<ResearchPlannerAgent>? logger = null)
    {
        _llm = llm; _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ResearchPlannerAgent>.Instance;
    }

    public Task<ResearchPlan> CreatePlanAsync(string topic, double researchabilityScore,
        double trendScore, double marketScore, List<Dictionary<string, object?>> articles)
    {
        _logger.LogInformation("Creating research plan for {Topic}", topic);

        var question = GenerateQuestion(topic, articles);
        var plan = new ResearchPlan
        {
            ResearchQuestion = question,
            Objectives = new() { $"Identify and evaluate key concepts in {topic}", "Analyze evidence from primary and secondary sources", "Compare competing approaches and identify trade-offs", "Assess practical implications and limitations", "Formulate evidence-based conclusions" },
            Subquestions = new() { $"What are the fundamental concepts underlying {topic}?", $"What evidence supports current claims about {topic}?", $"How does {topic} compare to alternative approaches?", $"What are the primary limitations and challenges?", $"What is the likely trajectory of {topic} development?" },
            RequiredEvidence = new() { "Primary research papers or technical reports", "Official documentation or technical specifications", "Real-world usage data or case studies", "Performance benchmarks or comparative evaluations", "Expert analysis or industry reports" },
            PrimarySourcesRequired = new() { "arXiv research papers", "GitHub repositories", "Engineering blogs" },
            AcademicSourcesRequired = new() { "arXiv", "Google Scholar", "Semantic Scholar" },
            TechnicalDocumentation = new() { $"Official {topic} documentation", "API references", "Implementation guides", "Performance benchmarks" },
            SearchQueries = new() { $"{topic} research paper", $"{topic} implementation", $"{topic} benchmark evaluation", $"{topic} limitations challenges", $"{topic} industry adoption", $"{topic} comparison alternatives", $"{topic} future trends" },
            PotentialCounterarguments = new() { $"Alternative approaches may outperform {topic} in specific scenarios", $"Current {topic} implementations may have scalability limitations", $"Safety and reliability concerns could limit {topic} adoption" },
            ExpectedLimitations = new() { $"Available evidence may be insufficient for definitive conclusions", $"Technology landscape may evolve rapidly during research" },
        };

        if (articles.Count < 10) plan.ExpectedLimitations.Add($"Limited source material ({articles.Count} articles available)");
        if (!articles.Any(a => a.GetValueOrDefault("source_type")?.ToString() == "arxiv"))
            plan.ExpectedLimitations.Add("No academic/research papers available in source material");

        return Task.FromResult(plan);
    }

    private static string GenerateQuestion(string topic, List<Dictionary<string, object?>> articles)
    {
        var lower = topic.ToLower();
        if (lower.Contains("agent")) return $"What are the primary failure modes affecting {topic} reliability in production environments?";
        if (lower.Contains("security")) return $"What are the most effective approaches for mitigating {topic} risks in modern infrastructure?";
        if (lower.Contains("cloud")) return $"How does {topic} adoption affect operational efficiency and cost in enterprise deployments?";
        if (lower.Contains("database")) return $"What factors most significantly impact {topic} performance and scalability?";
        if (lower.Contains("robotics")) return $"How do current {topic} approaches compare in terms of real-world deployment readiness?";
        if (lower.Contains("quantum")) return $"What are the practical barriers to {topic} adoption in commercial applications?";
        return $"What are the key factors driving {topic} adoption and what limitations remain?";
    }
}