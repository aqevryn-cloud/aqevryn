using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

public class MarketViabilityAgent
{
    private readonly ILogger<MarketViabilityAgent> _logger;
    private static readonly string[] KnownCompanies = { "google", "alphabet", "deepmind", "microsoft", "openai", "meta", "facebook", "apple", "amazon", "aws", "ibm", "nvidia", "intel", "tesla", "anthropic", "cohere", "hugging face", "stability ai", "databricks", "snowflake", "mongodb", "elastic", "cloudflare", "datadog", "hashicorp", "gitlab", "github", "docker", "red hat", "oracle", "salesforce", "adobe", "cisco", "vmware", "samsung", "qualcomm", "pinecone", "weaviate", "chroma", "langchain", "fixie", "cognition ai", "perplexity", "runway", "scale ai", "anyscale", "replicate", "modal", "together ai", "fireworks ai" };

    public MarketViabilityAgent(ILogger<MarketViabilityAgent>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MarketViabilityAgent>.Instance;
    }

    public Task<MarketViabilityResult> EvaluateAsync(string topic, List<Dictionary<string, object?>> articles)
    {
        _logger.LogInformation("Evaluating market for {Topic} with {Count} articles", topic, articles.Count);
        if (articles.Count == 0)
            return Task.FromResult(new MarketViabilityResult { Topic = topic, Score = 0, Evidence = new(), Limitations = new() { "No articles available" } });

        var companies = ExtractCompanies(articles);
        var commercialProducts = CountCommercialProducts(articles);
        var openSourceScore = ScoreOpenSourceActivity(articles);
        var industryAdoption = ScoreIndustryAdoption(articles, companies);
        var enterpriseActivity = ScoreEnterpriseActivity(articles, companies);
        var developerActivity = ScoreDeveloperActivity(articles);

        var score = (industryAdoption * 0.25 + enterpriseActivity * 0.20 + developerActivity * 0.20 + openSourceScore * 0.20 + Math.Min(1.0, commercialProducts / 5.0) * 0.15) * 100;
        score = Math.Clamp(score, 0, 100);

        var evidence = new List<string>();
        if (companies.Count > 0) evidence.Add($"Companies involved: {string.Join(", ", companies.Take(5))}");
        if (commercialProducts > 0) evidence.Add($"{commercialProducts} commercial products/services identified");
        if (openSourceScore > 0.3) evidence.Add("Significant open-source activity detected");

        var limitations = new List<string>();
        if (companies.Count == 0) limitations.Add("No major companies identified in this space");
        if (commercialProducts == 0) limitations.Add("No commercial products found");

        return Task.FromResult(new MarketViabilityResult
        {
            Topic = topic, Score = Math.Round(score, 1),
            IndustryAdoption = Math.Round(industryAdoption, 3), EnterpriseActivity = Math.Round(enterpriseActivity, 3),
            DeveloperActivity = Math.Round(developerActivity, 3), CommercialProducts = commercialProducts,
            OpenSourceActivity = Math.Round(openSourceScore, 3), CompaniesInvolved = companies.Take(10).ToList(),
            Evidence = evidence, Limitations = limitations,
        });
    }

    private List<string> ExtractCompanies(List<Dictionary<string, object?>> articles)
    {
        var found = new HashSet<string>();
        foreach (var a in articles)
        {
            var text = string.Join(" ", a.GetValueOrDefault("title")?.ToString() ?? "", a.GetValueOrDefault("summary")?.ToString() ?? "", a.GetValueOrDefault("content")?.ToString() ?? "", a.GetValueOrDefault("author")?.ToString() ?? "").ToLower();
            foreach (var company in KnownCompanies)
                if (text.Contains(company)) found.Add(char.ToUpper(company[0]) + company[1..]);
        }
        return found.OrderBy(c => c).ToList();
    }

    private int CountCommercialProducts(List<Dictionary<string, object?>> articles)
    {
        var keywords = new[] { "product", "service", "platform", "api", "enterprise", "saas", "cloud", "subscription", "pricing", "launch", "beta", "release" };
        return articles.Count(a => keywords.Any(k => (a.GetValueOrDefault("title")?.ToString() + " " + a.GetValueOrDefault("summary")?.ToString() + " " + a.GetValueOrDefault("content")?.ToString()).ToLower().Contains(k)));
    }

    private double ScoreOpenSourceActivity(List<Dictionary<string, object?>> articles)
    {
        var keywords = new[] { "open source", "github", "repository", "stars", "fork", "contributor", "pull request", "pypi", "npm", "library" };
        var matches = articles.Count(a => keywords.Any(k => (a.GetValueOrDefault("title")?.ToString() + " " + a.GetValueOrDefault("summary")?.ToString() + " " + a.GetValueOrDefault("content")?.ToString()).ToLower().Contains(k)));
        return articles.Count > 0 ? Math.Min(1.0, matches / (double)articles.Count * 2.0) : 0;
    }

    private double ScoreIndustryAdoption(List<Dictionary<string, object?>> articles, List<string> companies)
    {
        var keywords = new[] { "adoption", "deployed", "production", "enterprise", "industry", "market", "customer", "user base", "implementation", "integration", "workflow" };
        var matches = articles.Count(a => keywords.Any(k => (a.GetValueOrDefault("title")?.ToString() + " " + a.GetValueOrDefault("summary")?.ToString() + " " + a.GetValueOrDefault("content")?.ToString()).ToLower().Contains(k)));
        var ratio = articles.Count > 0 ? matches / (double)articles.Count : 0;
        var companyBonus = Math.Min(1.0, companies.Count / 10.0);
        return ratio * 0.6 + companyBonus * 0.4;
    }

    private double ScoreEnterpriseActivity(List<Dictionary<string, object?>> articles, List<string> companies)
    {
        var keywords = new[] { "enterprise", "business", "corporate", "organization", "team", "workflow", "productivity", "collaboration", "scaling", "deployment", "infrastructure" };
        var matches = articles.Count(a => keywords.Any(k => (a.GetValueOrDefault("title")?.ToString() + " " + a.GetValueOrDefault("summary")?.ToString() + " " + a.GetValueOrDefault("content")?.ToString()).ToLower().Contains(k)));
        var ratio = articles.Count > 0 ? matches / (double)articles.Count : 0;
        var bigTech = new[] { "google", "microsoft", "meta", "amazon", "apple", "nvidia", "openai", "anthropic" };
        var bigTechMatches = companies.Count(c => bigTech.Contains(c.ToLower()));
        var bonus = Math.Min(1.0, bigTechMatches / 3.0);
        return ratio * 0.5 + bonus * 0.5;
    }

    private double ScoreDeveloperActivity(List<Dictionary<string, object?>> articles)
    {
        var keywords = new[] { "developer", "programmer", "engineer", "hacker", "api", "sdk", "library", "framework", "tool", "tutorial", "documentation", "example", "guide", "community", "forum" };
        var matches = articles.Count(a => keywords.Any(k => (a.GetValueOrDefault("title")?.ToString() + " " + a.GetValueOrDefault("summary")?.ToString() + " " + a.GetValueOrDefault("content")?.ToString()).ToLower().Contains(k)));
        return articles.Count > 0 ? Math.Min(1.0, matches / (double)articles.Count * 1.5) : 0;
    }
}