using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Aqevryn.Config;

public class ConfigLoader
{
    public static AqevrynSettings LoadSettings(string? envPath = null)
    {
        var settings = new AqevrynSettings();

        // Load from environment variables
        if (Environment.GetEnvironmentVariable("APP_ENV") is { } env)
            settings.AppEnv = env;
        if (Environment.GetEnvironmentVariable("LLM_PROVIDER") is { } llmProv)
            settings.LlmProvider = llmProv;
        if (Environment.GetEnvironmentVariable("LLM_API_KEY") is { } llmKey)
            settings.LlmApiKey = llmKey;
        if (Environment.GetEnvironmentVariable("LLM_MODEL") is { } llmModel)
            settings.LlmModel = llmModel;
        if (Environment.GetEnvironmentVariable("GITHUB_TOKEN") is { } ghToken)
            settings.GitHubToken = ghToken;
        if (Environment.GetEnvironmentVariable("GITHUB_OWNER") is { } ghOwner)
            settings.GitHubOwner = ghOwner;
        if (Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") is { } ghRepo)
            settings.GitHubRepository = ghRepo;
        if (Environment.GetEnvironmentVariable("DATABASE_URL") is { } dbUrl)
            settings.DatabaseUrl = dbUrl;
        if (Environment.GetEnvironmentVariable("AUTO_PUBLISH") is { } autoPub)
            settings.AutoPublish = bool.Parse(autoPub);
        if (Environment.GetEnvironmentVariable("MIN_PUBLICATION_SCORE") is { } minScore)
            settings.MinPublicationScore = int.Parse(minScore);
        if (Environment.GetEnvironmentVariable("LOG_LEVEL") is { } logLevel)
            settings.LogLevel = logLevel;

        // Load from .env file if present
        if (envPath != null && File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("#") || string.IsNullOrEmpty(trimmed))
                    continue;
                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex < 0) continue;
                var key = trimmed[..eqIndex].Trim();
                var value = trimmed[(eqIndex + 1)..].Trim();
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        return settings;
    }

    public static List<Dictionary<string, object>> LoadSources(string path)
    {
        if (!File.Exists(path))
            return new();

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        var result = deserializer.Deserialize<Dictionary<string, object>>(yaml);
        if (result == null || !result.ContainsKey("sources"))
            return new();

        var sources = new List<Dictionary<string, object>>();
        if (result["sources"] is List<object> sourceList)
        {
            foreach (var item in sourceList)
            {
                if (item is Dictionary<object, object> sourceDict)
                {
                    sources.Add(sourceDict.ToDictionary(
                        k => k.Key?.ToString() ?? "",
                        v => v.Value ?? ""
                    ));
                }
            }
        }
        return sources;
    }
}