using Xunit;
using Aqevryn.Common;

namespace Aqevryn.Tests;

public class SourceItemTests
{
    [Fact]
    public void CreateMinimalItem()
    {
        var item = new SourceItem { Title = "Test", Url = "https://example.com", SourceName = "Src", SourceType = "rss" };
        item.ComputeHash();
        Assert.Equal("Test", item.Title);
        Assert.NotNull(item.ContentHash);
        Assert.Equal(64, item.ContentHash.Length);
    }

    [Fact]
    public void CreateFullItem()
    {
        var dt = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var item = new SourceItem
        {
            Title = "Full Article", Url = "https://example.com/article", SourceName = "Tech Blog",
            SourceType = "rss", Author = "Jane Doe", PublishedAt = dt,
            Summary = "A summary", Content = "Full content here",
            Tags = new() { "ai", "ml" }, Category = "technology",
        };
        item.ComputeHash();
        Assert.Equal("Jane Doe", item.Author);
        Assert.Equal(dt, item.PublishedAt);
        Assert.Contains("ai", item.Tags!);
    }

    [Fact]
    public void ContentHashConsistency()
    {
        var item1 = new SourceItem { Title = "Same", Url = "https://example.com/a", SourceName = "Src", SourceType = "rss" };
        item1.ComputeHash();
        var item2 = new SourceItem { Title = "Same", Url = "https://example.com/a", SourceName = "Src", SourceType = "rss" };
        item2.ComputeHash();
        Assert.Equal(item1.ContentHash, item2.ContentHash);
    }

    [Fact]
    public void DifferentContentDifferentHash()
    {
        var item1 = new SourceItem { Title = "Title A", Url = "https://example.com/a", SourceName = "Src", SourceType = "rss" };
        item1.ComputeHash();
        var item2 = new SourceItem { Title = "Title B", Url = "https://example.com/b", SourceName = "Src", SourceType = "rss" };
        item2.ComputeHash();
        Assert.NotEqual(item1.ContentHash, item2.ContentHash);
    }
}

public class AgentResultsTests
{
    [Fact]
    public void TopicDiscoveryResult_Defaults()
    {
        var r = new TopicDiscoveryResult { Topic = "AI Agents", Summary = "Summary" };
        Assert.Equal("AI Agents", r.Topic);
        Assert.Empty(r.Evidence);
    }

    [Fact]
    public void RankedTopic_Validation()
    {
        var r = new RankedTopic
        {
            Topic = "AI Agents", TrendScore = 90, ResearchabilityScore = 85,
            MarketViabilityScore = 80, FinalScore = 82.5, Decision = "SELECTED"
        };
        Assert.Equal(82.5, r.FinalScore);
        Assert.Equal("SELECTED", r.Decision);
    }

    [Fact]
    public void EditorialReviewResult_Defaults()
    {
        var r = new EditorialReviewResult();
        Assert.Equal("REJECT", r.PublishRecommendation);
        Assert.Empty(r.CriticalIssues);
    }

    [Fact]
    public void PipelineContext_Defaults()
    {
        var ctx = new PipelineContext { DryRun = true, Topic = "AI Agents" };
        Assert.True(ctx.DryRun);
        Assert.Equal("AI Agents", ctx.Topic);
        Assert.Equal("INITIALIZED", ctx.Stage);
    }
}