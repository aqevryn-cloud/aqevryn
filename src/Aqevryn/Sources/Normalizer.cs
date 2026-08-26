using System.Security.Cryptography;
using System.Text;
using Aqevryn.Common;

namespace Aqevryn.Sources;

public class ContentNormalizer
{
    public SourceItem Normalize(SourceItem item)
    {
        item.Title = NormalizeText(item.Title);
        if (!string.IsNullOrEmpty(item.Summary)) item.Summary = NormalizeText(item.Summary);
        if (!string.IsNullOrEmpty(item.Content)) item.Content = NormalizeText(item.Content);
        if (item.Tags != null) item.Tags = NormalizeTags(item.Tags);

        if (string.IsNullOrEmpty(item.Summary) && !string.IsNullOrEmpty(item.Content))
            item.Summary = MakeExcerpt(item.Content);

        item.ComputeHash();
        return item;
    }

    public static string NormalizeText(string text)
    {
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[\r\n\t]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    public static string MakeExcerpt(string content, int maxLength = 512)
    {
        var flat = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ").Trim();
        if (flat.Length <= maxLength) return flat;
        var truncated = flat[..maxLength];
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxLength * 0.6) truncated = truncated[..lastSpace];
        return truncated + "...";
    }

    public static List<string> NormalizeTags(List<string> tags)
    {
        return tags
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();
    }
}

public class Deduplicator
{
    private readonly HashSet<string> _seenUrls = new();
    private readonly HashSet<string> _seenHashes = new();

    public void Reset()
    {
        _seenUrls.Clear();
        _seenHashes.Clear();
    }

    public bool IsDuplicate(SourceItem item)
    {
        var url = item.CanonicalUrl ?? item.Url;
        if (!string.IsNullOrEmpty(url) && _seenUrls.Contains(url)) return true;
        if (!string.IsNullOrEmpty(item.ContentHash) && _seenHashes.Contains(item.ContentHash)) return true;
        return false;
    }

    public void MarkSeen(SourceItem item)
    {
        var url = item.CanonicalUrl ?? item.Url;
        if (!string.IsNullOrEmpty(url)) _seenUrls.Add(url);
        if (!string.IsNullOrEmpty(item.ContentHash)) _seenHashes.Add(item.ContentHash);
    }

    public List<SourceItem> Deduplicate(List<SourceItem> items)
    {
        var unique = new List<SourceItem>();
        foreach (var item in items)
        {
            if (!IsDuplicate(item))
            {
                MarkSeen(item);
                unique.Add(item);
            }
        }
        return unique;
    }
}