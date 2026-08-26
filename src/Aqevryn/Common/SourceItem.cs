namespace Aqevryn.Common;

public class SourceItem
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string SourceName { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string ContentHash { get; set; } = "";
    public string? CanonicalUrl { get; set; }
    public string? Author { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public List<string>? Tags { get; set; }
    public string? Category { get; set; }
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Raw { get; set; }

    public void ComputeHash()
    {
        if (string.IsNullOrEmpty(ContentHash))
        {
            var raw = $"{Title}|{Url}|{Content ?? Summary ?? ""}";
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(raw);
            ContentHash = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
        }
    }
}

public class SourceError : Exception
{
    public SourceError(string message) : base(message) { }
    public SourceError(string message, Exception inner) : base(message, inner) { }
}

public interface ISourceAdapter
{
    string Name { get; }
    bool Enabled { get; }
    string? Category { get; }
    Task<List<SourceItem>> FetchAsync();
    Task<bool> ValidateAsync();
}