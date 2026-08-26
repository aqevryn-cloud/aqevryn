using Xunit;
using Aqevryn.Publishing;
using Aqevryn.Common;

namespace Aqevryn.Tests;

public class MarkdownGeneratorTests
{
    [Fact]
    public void Generate_ProducesMarkdown()
    {
        var gen = new MarkdownGenerator();
        var article = new GeneratedArticle
        {
            Title = "Research: AI Coding Agents",
            Slug = "ai-coding-agents",
            Description = "An evidence-based analysis.",
            Introduction = "This article examines AI coding agents.",
            ResearchQuestion = "What affects reliability?",
            TechnicalAnalysis = "Technical analysis here.",
            Conclusion = "Final conclusion.",
            References = new() { "[1] Source A" },
        };
        var md = gen.Generate(article);
        Assert.StartsWith("---", md);
        Assert.Contains("title:", md);
        Assert.Contains("Introduction", md);
        Assert.Contains("Conclusion", md);
    }

    [Fact]
    public void Generate_WithScores_IncludesFrontmatter()
    {
        var gen = new MarkdownGenerator();
        var article = new GeneratedArticle
        {
            Title = "Test", Slug = "test", Description = "Desc",
            Introduction = "Intro", Conclusion = "Conclusion",
        };
        var scores = new Dictionary<string, double> { ["editorial"] = 93, ["trend"] = 91 };
        var md = gen.Generate(article, scores);
        Assert.Contains("research_score: 93", md);
        Assert.Contains("trend_score: 91", md);
    }
}

public class GitHubPublisherTests
{
    [Fact]
    public async Task Publish_DryRun_DoesNotPublish()
    {
        var publisher = new GitHubPublisher("token", "owner", "repo");
        var result = await publisher.PublishAsync("# Test", "test.md", "Test Topic", null, dryRun: true);
        Assert.True(result.Success);
        Assert.Contains("Dry run", result.Error!);
    }

    [Fact]
    public async Task Publish_MissingToken_Fails()
    {
        var publisher = new GitHubPublisher("", "owner", "repo");
        var result = await publisher.PublishAsync("# Test", "test.md", "Test");
        Assert.False(result.Success);
        Assert.Contains("token", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Publish_MissingOwner_Fails()
    {
        var publisher = new GitHubPublisher("token", "", "");
        var result = await publisher.PublishAsync("# Test", "test.md", "Test");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }
}