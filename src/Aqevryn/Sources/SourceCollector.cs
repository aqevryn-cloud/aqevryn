using Aqevryn.Common;
using Aqevryn.Config;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Sources;

public class SourceCollector
{
    private readonly List<Dictionary<string, object>> _sourceConfigs;
    private readonly AqevrynSettings _settings;
    private readonly ILogger<SourceCollector> _logger;
    private readonly ContentNormalizer _normalizer = new();
    private readonly Deduplicator _deduplicator = new();
    private readonly int _maxConcurrency;

    public List<SourceItem> Results { get; private set; } = new();
    public List<(string Name, string Error)> Errors { get; private set; } = new();

    public SourceCollector(List<Dictionary<string, object>> sourceConfigs, AqevrynSettings settings,
        ILogger<SourceCollector>? logger = null, int maxConcurrency = 5)
    {
        _sourceConfigs = sourceConfigs;
        _settings = settings;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SourceCollector>.Instance;
        _maxConcurrency = maxConcurrency;
    }

    public async Task<List<SourceItem>> CollectAllAsync()
    {
        Results = new();
        Errors = new();
        var enabled = _sourceConfigs.Where(s => GetBool(s, "enabled", true)).ToList();
        _logger.LogInformation("Collecting from {Count} sources", enabled.Count);

        using var semaphore = new SemaphoreSlim(_maxConcurrency);
        var tasks = enabled.Select(async config =>
        {
            await semaphore.WaitAsync();
            try { return await FetchSingleAsync(config); }
            finally { semaphore.Release(); }
        });

        var allResults = await Task.WhenAll(tasks);

        foreach (var (config, result) in enabled.Zip(allResults))
        {
            if (result.IsFailure)
            {
                var name = GetString(config, "name", "unknown");
                _logger.LogWarning("Source {Name} failed: {Error}", name, result.Error);
                Errors.Add((name, result.Error?.Message ?? "Unknown error"));
            }
            else
            {
                Results.AddRange(result.Value!);
            }
        }

        var before = Results.Count;
        Results = _deduplicator.Deduplicate(Results);
        _logger.LogInformation("Collected {Total} items ({Duplicates} duplicates removed)", Results.Count, before - Results.Count);

        return Results;
    }

    private async Task<Result<List<SourceItem>>> FetchSingleAsync(Dictionary<string, object> config)
    {
        var name = GetString(config, "name", "unknown");
        var type = GetString(config, "type", "");

        try
        {
            ISourceAdapter adapter = type switch
            {
                "rss" => new RssSourceAdapter(config, _settings),
                "arxiv" => new ArxivSourceAdapter(config, _settings),
                "github" => new GitHubSourceAdapter(config, _settings),
                "hackernews" => new HackerNewsSourceAdapter(config, _settings),
                "reddit" => new RedditSourceAdapter(config, _settings),
                "moltbook" => new MoltbookSourceAdapter(config, _settings),
                _ => throw new ArgumentException($"Unknown source type: {type}")
            };

            var items = await adapter.FetchAsync();
            var normalized = items.Select(i => _normalizer.Normalize(i)).ToList();
            _logger.LogDebug("Fetched {Count} items from {Name}", normalized.Count, name);
            return Result<List<SourceItem>>.Success(normalized);
        }
        catch (Exception ex)
        {
            return Result<List<SourceItem>>.Failure(ex);
        }
    }

    private static string GetString(Dictionary<string, object> config, string key, string defaultValue)
    {
        return config.TryGetValue(key, out var v) && v != null ? v.ToString() ?? defaultValue : defaultValue;
    }

    private static bool GetBool(Dictionary<string, object> config, string key, bool defaultValue)
    {
        return config.TryGetValue(key, out var v) && bool.TryParse(v?.ToString(), out var result) ? result : defaultValue;
    }
}

public class Result<T>
{
    public bool IsSuccess { get; private set; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; private set; }
    public Exception? Error { get; private set; }

    public static Result<T> Success(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Failure(Exception error) => new() { IsSuccess = false, Error = error };
}