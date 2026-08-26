using Aqevryn.Config;
using Aqevryn.Common;

namespace Aqevryn.Sources;

public abstract class BaseSourceAdapter : ISourceAdapter
{
    protected readonly Dictionary<string, object> Config;
    public string Name { get; }
    public bool Enabled { get; }
    public string? Category { get; }

    protected BaseSourceAdapter(Dictionary<string, object> config, AqevrynSettings settings)
    {
        Config = config;
        Name = GetString(config, "name", "Unnamed Source");
        Enabled = GetBool(config, "enabled", true);
        Category = GetStringOrNull(config, "category");
    }

    protected static string GetString(Dictionary<string, object> config, string key, string defaultValue)
    {
        return config.TryGetValue(key, out var v) && v != null ? v.ToString() ?? defaultValue : defaultValue;
    }

    protected static string? GetStringOrNull(Dictionary<string, object> config, string key)
    {
        return config.TryGetValue(key, out var v) && v != null && !string.IsNullOrEmpty(v.ToString())
            ? v.ToString()
            : null;
    }

    protected static int GetInt(Dictionary<string, object> config, string key, int defaultValue)
    {
        return config.TryGetValue(key, out var v) && int.TryParse(v?.ToString(), out var result)
            ? result : defaultValue;
    }

    protected static bool GetBool(Dictionary<string, object> config, string key, bool defaultValue)
    {
        return config.TryGetValue(key, out var v) && bool.TryParse(v?.ToString(), out var result)
            ? result : defaultValue;
    }

    public abstract Task<List<SourceItem>> FetchAsync();
    public abstract Task<bool> ValidateAsync();
}