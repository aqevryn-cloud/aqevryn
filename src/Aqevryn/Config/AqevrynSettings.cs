namespace Aqevryn.Config;

public class AqevrynSettings
{
    // App
    public string AppEnv { get; set; } = "development";
    public string LogLevel { get; set; } = "INFO";
    public string DataDir { get; set; } = "./data";

    // LLM
    public string LlmProvider { get; set; } = "openai";
    public string LlmApiKey { get; set; } = "";
    public string LlmModel { get; set; } = "gpt-4o";
    public int LlmMaxTokens { get; set; } = 4096;
    public double LlmTemperature { get; set; } = 0.3;

    // Search
    public string SearchProvider { get; set; } = "tavily";
    public string SearchApiKey { get; set; } = "";
    public int SearchMaxResults { get; set; } = 10;

    // GitHub
    public string GitHubToken { get; set; } = "";
    public string GitHubOwner { get; set; } = "";
    public string GitHubRepository { get; set; } = "";
    public string GitHubDefaultBranch { get; set; } = "main";

    // Database
    public string DatabaseUrl { get; set; } = "Host=localhost;Database=aqevryn;Username=aqevryn;Password=aqevryn";

    // Pipeline
    public bool AutoPublish { get; set; } = false;
    public int MinPublicationScore { get; set; } = 90;
    public bool DryRun { get; set; } = false;

    // Scoring Weights
    public double WeightTrend { get; set; } = 0.25;
    public double WeightResearchability { get; set; } = 0.25;
    public double WeightMarket { get; set; } = 0.20;
    public double WeightNovelty { get; set; } = 0.15;
    public double WeightTechnicalSignificance { get; set; } = 0.15;

    // Scheduler
    public int CollectIntervalHours { get; set; } = 6;
    public int AnalyzeIntervalHours { get; set; } = 12;
    public int RankIntervalHours { get; set; } = 24;

    // Cost Controls
    public int MaxSourcesPerResearch { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public int RequestTimeoutSeconds { get; set; } = 60;

    // Redis
    public string RedisUrl { get; set; } = "";
}