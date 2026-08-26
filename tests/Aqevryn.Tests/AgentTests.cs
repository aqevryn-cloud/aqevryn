using Xunit;
using Aqevryn.Agents;
using Aqevryn.Common;

namespace Aqevryn.Tests;

public class TopicDiscoveryTests
{
    [Fact]
    public async Task Discover_WithAIModels_ShouldFindTopics()
    {
        var agent = new TopicDiscoveryAgent();
        var articles = new List<Dictionary<string, object?>>
        {
            new() { ["title"] = "New AI Model Breakthrough", ["content"] = "Content about AI", ["tags"] = new List<object> { "ai", "ml" } },
            new() { ["title"] = "LLM Agents Rise", ["content"] = "Content about LLM agents", ["tags"] = new List<object> { "ai", "agents" } },
            new() { ["title"] = "GPT-5 Launch", ["content"] = "OpenAI launches GPT-5", ["tags"] = new List<object> { "ai", "gpt" } },
        };
        var results = await agent.DiscoverAsync(articles);
        Assert.NotEmpty(results);
        Assert.Equal("artificial_intelligence", results[0].Category);
    }

    [Fact]
    public async Task Discover_EmptyArticles_ReturnsEmpty()
    {
        var agent = new TopicDiscoveryAgent();
        var results = await agent.DiscoverAsync(new());
        Assert.Empty(results);
    }
}

public class TopicClusteringTests
{
    [Fact]
    public async Task Cluster_RelatedTopics_ShouldMerge()
    {
        var agent = new TopicClusteringAgent();
        var topics = new List<Dictionary<string, object?>>
        {
            new() { ["topic"] = "AI Agents", ["id"] = "1" },
            new() { ["topic"] = "Agentic AI", ["id"] = "2" },
            new() { ["topic"] = "Large Language Models", ["id"] = "3" },
        };
        var clusters = await agent.ClusterAsync(topics);
        Assert.NotEmpty(clusters);
        Assert.Contains(clusters, c => c.CanonicalTopic.Contains("AI Agents"));
    }

    [Fact]
    public async Task Cluster_Empty_ReturnsEmpty()
    {
        var agent = new TopicClusteringAgent();
        var clusters = await agent.ClusterAsync(new());
        Assert.Empty(clusters);
    }
}

public class TrendAnalysisTests
{
    [Fact]
    public async Task Analyze_WithArticles_ComputesScore()
    {
        var agent = new TrendAnalysisAgent();
        var articles = new List<Dictionary<string, object?>>
        {
            new() { ["title"] = "A", ["source_name"] = "Src1", ["published_at"] = DateTime.UtcNow },
            new() { ["title"] = "B", ["source_name"] = "Src2", ["published_at"] = DateTime.UtcNow },
            new() { ["title"] = "C", ["source_name"] = "Src1", ["published_at"] = DateTime.UtcNow },
        };
        var result = await agent.AnalyzeAsync("AI Agents", articles);
        Assert.True(result.Score > 0);
        Assert.Equal(3, result.MentionCount);
        Assert.Equal(2, result.IndependentSources);
    }

    [Fact]
    public async Task Analyze_NoArticles_ReturnsZero()
    {
        var agent = new TrendAnalysisAgent();
        var result = await agent.AnalyzeAsync("Unknown", new());
        Assert.Equal(0, result.Score);
    }
}

public class ResearchabilityTests
{
    [Fact]
    public async Task Evaluate_WithArticles_ComputesScore()
    {
        var agent = new ResearchabilityAgent();
        var articles = new List<Dictionary<string, object?>>
        {
            new() { ["title"] = "Paper 1", ["source_type"] = "arxiv", ["content"] = "Deep content about architecture and algorithms for AI agents tool use planning and evaluation methodology benchmark results." },
            new() { ["title"] = "Paper 2", ["source_type"] = "arxiv", ["content"] = "Another paper about implementation and performance evaluation of AI agents with experimental results." },
        };
        var result = await agent.EvaluateAsync("AI Agents", articles);
        Assert.True(result.Score > 0);
        Assert.True(result.HasAcademicPapers);
    }

    [Fact]
    public async Task Evaluate_NoArticles_ReturnsZero()
    {
        var agent = new ResearchabilityAgent();
        var result = await agent.EvaluateAsync("Unknown", new());
        Assert.Equal(0, result.Score);
    }
}

public class MarketViabilityTests
{
    [Fact]
    public async Task Evaluate_WithArticles_DetectsCompanies()
    {
        var agent = new MarketViabilityAgent();
        var articles = new List<Dictionary<string, object?>>
        {
            new() { ["title"] = "Google and Microsoft Launch AI Partnership", ["content"] = "Google and Microsoft are collaborating on AI agents for enterprise." },
            new() { ["title"] = "OpenAI Enterprise Product", ["content"] = "OpenAI's new enterprise product targets businesses with AI." },
        };
        var result = await agent.EvaluateAsync("AI Agents", articles);
        Assert.NotEmpty(result.CompaniesInvolved);
        Assert.True(result.Score > 0);
    }

    [Fact]
    public async Task Evaluate_NoArticles_ReturnsZero()
    {
        var agent = new MarketViabilityAgent();
        var result = await agent.EvaluateAsync("Unknown", new());
        Assert.Equal(0, result.Score);
    }
}

public class TopicRankerTests
{
    [Fact]
    public void Rank_SingleTopic_ComputesScore()
    {
        var ranker = new TopicRanker(minResearchability: 0);
        var topics = new List<Dictionary<string, object?>>
        {
            new() { ["topic"] = "AI Agents", ["category"] = "ai" },
        };
        var ranked = ranker.Rank(topics,
            new() { ["AI Agents"] = 90 }, new() { ["AI Agents"] = 85 }, new() { ["AI Agents"] = 80 }, new() { ["AI Agents"] = 20 });
        Assert.Single(ranked);
        Assert.Equal("SELECTED", ranked[0].Decision);
        Assert.True(ranked[0].FinalScore > 0);
    }

    [Fact]
    public void Rank_RejectLowResearchability()
    {
        var ranker = new TopicRanker(minResearchability: 50);
        var topics = new List<Dictionary<string, object?>>
        {
            new() { ["topic"] = "Weak Topic" },
        };
        var ranked = ranker.Rank(topics,
            new() { ["Weak Topic"] = 90 }, new() { ["Weak Topic"] = 20 }, new() { ["Weak Topic"] = 80 }, new() { ["Weak Topic"] = 10 });
        Assert.Equal("REJECTED", ranked[0].Decision);
    }

    [Fact]
    public void SelectTopN_ReturnsCorrectCount()
    {
        var ranker = new TopicRanker(minResearchability: 0, minEvidence: 0);
        var topics = new List<Dictionary<string, object?>>
        {
            new() { ["topic"] = "A" }, new() { ["topic"] = "B" }, new() { ["topic"] = "C" },
        };
        var all = ranker.Rank(topics, articleCounts: new() { ["A"] = 5, ["B"] = 5, ["C"] = 5 });
        var selected = ranker.SelectTopN(all, 2);
        Assert.Equal(2, selected.Count);
    }
}

public class ResearchPlannerTests
{
    [Fact]
    public async Task CreatePlan_WithArticles_ProducesPlan()
    {
        var agent = new ResearchPlannerAgent();
        var articles = new List<Dictionary<string, object?>>
        {
            new() { ["title"] = "Article 1", ["source_type"] = "arxiv" },
            new() { ["title"] = "Article 2", ["source_type"] = "rss" },
        };
        var plan = await agent.CreatePlanAsync("AI Agents", 85, 90, 80, articles);
        Assert.NotNull(plan.ResearchQuestion);
        Assert.NotEmpty(plan.Objectives);
        Assert.NotEmpty(plan.SearchQueries);
    }
}

public class ResearchAgentTests
{
    [Fact]
    public async Task Research_WithArticles_ProducesFindings()
    {
        var agent = new ResearchAgent();
        var articles = new List<Dictionary<string, object?>>
        {
            new() { ["title"] = "Paper 1", ["url"] = "https://arxiv.org/abs/001", ["source_type"] = "arxiv" },
            new() { ["title"] = "Paper 2", ["url"] = "https://arxiv.org/abs/002", ["source_type"] = "arxiv" },
        };
        var result = await agent.ResearchAsync("Test Question", "AI Agents", articles);
        Assert.NotEmpty(result.SourcesAnalyzed);
        Assert.NotEmpty(result.Findings);
        Assert.NotEmpty(result.Conclusions);
    }

    [Fact]
    public async Task Research_EmptyArticles_ReturnsEmpty()
    {
        var agent = new ResearchAgent();
        var result = await agent.ResearchAsync("Test", "Unknown", new());
        Assert.Empty(result.SourcesAnalyzed);
        Assert.Empty(result.Findings);
    }
}

public class EvidenceValidatorTests
{
    [Fact]
    public async Task Validate_ValidFinding_Passes()
    {
        var validator = new EvidenceValidator();
        var finding = new Dictionary<string, object?>
        {
            ["claim"] = "AI agents improve developer productivity by 30%",
            ["source_url"] = "https://example.com/study",
        };
        var result = await validator.ValidateFindingAsync(finding);
        Assert.True((bool)result["is_valid"]);
    }

    [Fact]
    public async Task Validate_MissingUrl_Fails()
    {
        var validator = new EvidenceValidator();
        var finding = new Dictionary<string, object?>
        {
            ["claim"] = "Test claim", ["source_url"] = "",
        };
        var result = await validator.ValidateFindingAsync(finding);
        Assert.False((bool)result["is_valid"]);
    }

    [Fact]
    public void CheckFabrication_DetectsUnsupportedStats()
    {
        var validator = new EvidenceValidator();
        var findings = new List<Dictionary<string, object?>>
        {
            new() { ["claim"] = "75% of developers use AI tools", ["source_url"] = "" },
        };
        var warnings = validator.CheckFabrication(findings);
        Assert.NotEmpty(warnings);
    }
}

public class ArticleWriterTests
{
    [Fact]
    public async Task Write_WithResearch_ProducesArticle()
    {
        var agent = new ArticleWriterAgent();
        var result = new ResearchResult
        {
            ResearchQuestion = "Test question?",
            Findings = new() { new() { Claim = "Finding 1", FindingType = "research_finding", Confidence = 0.85 } },
            SourcesAnalyzed = new() { new() { Title = "Source 1", Url = "https://example.com", SourceType = "research_paper" } },
            Conclusions = new() { "Conclusion text" },
        };
        var article = await agent.WriteAsync("AI Coding Agents", result);
        Assert.NotNull(article.Title);
        Assert.NotNull(article.Slug);
        Assert.NotNull(article.Introduction);
        Assert.NotEmpty(article.References);
    }
}

public class EditorialReviewTests
{
    [Fact]
    public async Task Review_GoodArticle_Approves()
    {
        var agent = new EditorialReviewAgent();
        var article = new GeneratedArticle
        {
            Title = "Research: AI Coding Agents — An Evidence-Based Analysis",
            Slug = "ai-coding-agents",
            Description = "An evidence-based investigation into AI coding agents.",
            Introduction = "This article examines AI coding agents in depth.",
            ResearchQuestion = "What affects reliability?",
            TechnicalAnalysis = "The architecture uses transformer-based models with tool-use APIs.",
            Findings = "Tool-use reliability is critical for agent performance.",
            Limitations = "This analysis is based on publicly available sources.",
            Conclusion = "AI coding agents represent a significant advancement.",
            Methodology = "Based on analysis of 10 sources.",
            References = new() { "[1] Paper A", "[2] Paper B" },
        };
        var result = await agent.ReviewAsync(article);
        Assert.True(result.OverallScore > 60);
        Assert.Empty(result.CriticalIssues);
    }

    [Fact]
    public async Task Review_MissingReferences_Rejects()
    {
        var agent = new EditorialReviewAgent();
        var article = new GeneratedArticle
        {
            Title = "Bad Article", Slug = "bad-article", Description = "",
            Introduction = "Content.", Conclusion = "Conclusion.",
        };
        var result = await agent.ReviewAsync(article);
        Assert.NotEmpty(result.CriticalIssues);
        Assert.Equal("REJECT", result.PublishRecommendation);
    }
}