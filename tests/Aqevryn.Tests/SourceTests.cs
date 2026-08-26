using Xunit;
using Aqevryn.Sources;
using Aqevryn.Common;
using Aqevryn.Config;

namespace Aqevryn.Tests;

public class NormalizerTests
{
    [Fact]
    public void NormalizeText_CollapsesWhitespace()
    {
        var result = ContentNormalizer.NormalizeText("  Hello\n  World!\tMultiple   spaces  ");
        Assert.Equal("Hello World! Multiple spaces", result);
    }

    [Fact]
    public void MakeExcerpt_ShortText()
    {
        var result = ContentNormalizer.MakeExcerpt("Short text", 100);
        Assert.Equal("Short text", result);
    }

    [Fact]
    public void MakeExcerpt_Truncated()
    {
        var text = string.Join(" ", Enumerable.Repeat("Word", 200));
        var result = ContentNormalizer.MakeExcerpt(text, 100);
        Assert.True(result.Length <= 103);
        Assert.EndsWith("...", result);
    }

    [Fact]
    public void NormalizeTags_LowercaseDedupe()
    {
        var result = ContentNormalizer.NormalizeTags(new() { " AI ", "ML", " ai ", "  Deep Learning  " });
        Assert.Equal(3, result.Count);
        Assert.Contains("ai", result);
        Assert.Contains("ml", result);
        Assert.Contains("deep learning", result);
    }

    [Fact]
    public void Normalize_FullItem()
    {
        var normalizer = new ContentNormalizer();
        var item = new SourceItem
        {
            Title = "  Hello  World  ", Url = "https://example.com", SourceName = "Test", SourceType = "rss",
            Content = "Some content here", Tags = new() { " AI ", "ML" },
        };
        var result = normalizer.Normalize(item);
        Assert.Equal("Hello World", result.Title);
        Assert.Contains("ai", result.Tags!);
    }
}

public class DeduplicatorTests
{
    [Fact]
    public void UrlDedup()
    {
        var dedup = new Deduplicator();
        var item1 = new SourceItem { Title = "A", Url = "https://example.com/a", SourceName = "S", SourceType = "rss" };
        item1.ComputeHash();
        var item2 = new SourceItem { Title = "A", Url = "https://example.com/a", SourceName = "S", SourceType = "rss" };
        item2.ComputeHash();
        Assert.False(dedup.IsDuplicate(item1));
        dedup.MarkSeen(item1);
        Assert.True(dedup.IsDuplicate(item2));
    }

    [Fact]
    public void DeduplicateList()
    {
        var dedup = new Deduplicator();
        var items = new List<SourceItem>
        {
            new() { Title = "A", Url = "https://a.com", SourceName = "S", SourceType = "rss" },
            new() { Title = "B", Url = "https://b.com", SourceName = "S", SourceType = "rss" },
            new() { Title = "A", Url = "https://a.com", SourceName = "S", SourceType = "rss" },
        };
        items.ForEach(i => i.ComputeHash());
        var unique = dedup.Deduplicate(items);
        Assert.Equal(2, unique.Count);
    }
}

public class SourceCollectorTests
{
    [Fact]
    public async Task CollectAll_EmptyConfig()
    {
        var collector = new SourceCollector(new(), new AqevrynSettings());
        var items = await collector.CollectAllAsync();
        Assert.Empty(items);
    }

    [Fact]
    public async Task CollectAll_DisabledSource()
    {
        var configs = new List<Dictionary<string, object>>
        {
            new() { ["name"] = "Disabled", ["type"] = "rss", ["url"] = "https://x.com", ["enabled"] = false },
        };
        var collector = new SourceCollector(configs, new AqevrynSettings());
        var items = await collector.CollectAllAsync();
        Assert.Empty(items);
    }
}