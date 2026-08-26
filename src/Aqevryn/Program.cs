using System.Text.Json;
using Aqevryn.Config;
using Aqevryn.Orchestration;
using Aqevryn.Common;
using Serilog;

namespace Aqevryn;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File("logs/aqevryn-.log", rollingInterval: RollingInterval.Day)
            .CreateLogger();

        var settings = ConfigLoader.LoadSettings(".env");

        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            ShowHelp();
            return 0;
        }

        var dryRun = args.Contains("--dry-run");
        var verbose = args.Contains("-v") || args.Contains("--verbose");
        var commandArgs = args.Where(a => !a.StartsWith("--")).ToArray();

        if (dryRun) settings.DryRun = true;
        if (verbose) Log.Logger = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Console().CreateLogger();

        try
        {
            return commandArgs[0] switch
            {
                "run" => await RunPipeline(settings, dryRun),
                "discover" => await RunDiscover(settings),
                "analyze" => await RunAnalyze(settings),
                "research" => await RunResearch(settings, commandArgs.Length > 1 ? commandArgs[1] : null),
                "write" => await RunWrite(settings),
                "review" => await RunReview(settings),
                "publish" => await RunPublish(settings, dryRun),
                "health" => RunHealth(),
                "build-site" => await RunBuildSite(),
                "scheduler" => await RunScheduler(settings),
                "api" => await RunApi(),
                _ => ShowHelpAndReturn(1),
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Command failed");
            return 1;
        }
    }

    static async Task<int> RunPipeline(AqevrynSettings settings, bool dryRun)
    {
        Console.WriteLine("Aqevryn starting...");
        Console.WriteLine("[1/9] Collecting technology sources...");
        Console.WriteLine("[2/9] Extracting topics...");
        Console.WriteLine("[3/9] Clustering...");
        Console.WriteLine("[4/9] Trend analysis...");
        Console.WriteLine("[5/9] Researchability...");
        Console.WriteLine("[6/9] Market viability...");
        Console.WriteLine("[7/9] Researching...");
        Console.WriteLine("[8/9] Generating article...");
        Console.WriteLine("[9/9] Editorial review...");

        var ctx = new PipelineContext { DryRun = dryRun };
        var pipeline = new Pipeline(ctx, settings);
        var result = await pipeline.RunAsync();

        if (result.Stage == "COMPLETED")
        {
            Console.WriteLine("Pipeline completed successfully.");
            if (result.SelectedTopic != null)
                Console.WriteLine($"\nTopic: {result.SelectedTopic.Topic}\nFinal Score: {result.SelectedTopic.FinalScore}");
            if (result.PublishPrUrl != null)
                Console.WriteLine($"Pull Request: {result.PublishPrUrl}");
            return 0;
        }
        Console.WriteLine($"Pipeline failed at stage {result.Stage}: {result.Error}");
        return 1;
    }

    static async Task<int> RunDiscover(AqevrynSettings settings)
    {
        Console.WriteLine("Collecting technology sources...");
        var ctx = new PipelineContext { DryRun = true };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline.RunAsync();
        Console.WriteLine($"{ctx.Articles.Count} new articles found");
        Console.WriteLine($"{ctx.Topics.Count} candidate topics");
        return 0;
    }

    static async Task<int> RunAnalyze(AqevrynSettings settings)
    {
        Console.WriteLine("Analyzing topics...");
        var ctx = new PipelineContext { DryRun = true };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline._StageDiscover();
        await pipeline._StageAnalyze();
        if (ctx.SelectedTopic != null)
        {
            Console.WriteLine($"Top topic: {ctx.SelectedTopic.Topic}");
            Console.WriteLine($"Trend Score: {ctx.SelectedTopic.TrendScore}");
            Console.WriteLine($"Researchability: {ctx.SelectedTopic.ResearchabilityScore}");
            Console.WriteLine($"Market Viability: {ctx.SelectedTopic.MarketViabilityScore}");
            Console.WriteLine($"Final Score: {ctx.SelectedTopic.FinalScore}");
        }
        return 0;
    }

    static async Task<int> RunResearch(AqevrynSettings settings, string? topic)
    {
        Console.WriteLine($"Researching topic: {topic ?? "auto-selected"}");
        var ctx = new PipelineContext { DryRun = true, Topic = topic };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline.RunAsync();
        if (ctx.ResearchResult != null)
            Console.WriteLine($"{ctx.ResearchResult.SourcesAnalyzed.Count} sources analyzed, {ctx.ResearchResult.Findings.Count} findings");
        return 0;
    }

    static async Task<int> RunWrite(AqevrynSettings settings)
    {
        var ctx = new PipelineContext { DryRun = true };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline.RunAsync();
        if (ctx.GeneratedArticle != null)
            Console.WriteLine($"Article generated: {ctx.GeneratedArticle.Title}");
        return 0;
    }

    static async Task<int> RunReview(AqevrynSettings settings)
    {
        var ctx = new PipelineContext { DryRun = true };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline.RunAsync();
        if (ctx.EditorialReview != null)
        {
            Console.WriteLine($"Editorial Score: {ctx.EditorialReview.OverallScore}");
            Console.WriteLine($"Recommendation: {ctx.EditorialReview.PublishRecommendation}");
        }
        return 0;
    }

    static async Task<int> RunPublish(AqevrynSettings settings, bool dryRun)
    {
        Console.WriteLine("Publishing...");
        if (dryRun) { Console.WriteLine("Dry-run: skipping publication"); return 0; }
        var ctx = new PipelineContext { DryRun = dryRun };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline.RunAsync();
        if (ctx.PublishPrUrl != null) Console.WriteLine($"PR: {ctx.PublishPrUrl}");
        else Console.WriteLine("No article to publish");
        return 0;
    }

    static int RunHealth()
    {
        var health = new { status = "ok", version = "0.1.0", environment = Environment.GetEnvironmentVariable("APP_ENV") ?? "development" };
        Console.WriteLine(JsonSerializer.Serialize(health, new JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }

    static async Task<int> RunBuildSite()
    {
        Console.WriteLine("Building site...");
        var gen = new Publishing.SiteGenerator();
        gen.Generate();
        return 0;
    }

    static async Task<int> RunScheduler(AqevrynSettings settings)
    {
        Console.WriteLine("Scheduler started. Press Ctrl+C to stop.");
        var scheduler = new Scheduler.AqevrynScheduler(settings);
        await scheduler.StartAsync();
        return 0;
    }

    static async Task<int> RunApi()
    {
        Console.WriteLine("API server starting on port 8000...");
        var api = new Api.ApiServer();
        await api.StartAsync();
        return 0;
    }

    static void ShowHelp()
    {
        Console.WriteLine("Aqevryn — Autonomous Technology Research & Publishing Agent");
        Console.WriteLine();
        Console.WriteLine("Usage: aqevryn [command] [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  run          Run the full pipeline");
        Console.WriteLine("  discover     Collect sources and discover topics");
        Console.WriteLine("  analyze      Analyze trends, researchability, market");
        Console.WriteLine("  research     Conduct deep research on a topic");
        Console.WriteLine("  write        Generate a research article");
        Console.WriteLine("  review       Run editorial review");
        Console.WriteLine("  publish      Publish (create PR or auto-publish)");
        Console.WriteLine("  scheduler    Run the scheduler loop");
        Console.WriteLine("  api          Start the API server");
        Console.WriteLine("  health       Check application health");
        Console.WriteLine("  build-site   Build the static research website");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --dry-run    Run without publishing");
        Console.WriteLine("  -v           Verbose output");
        Console.WriteLine("  --help       Show help");
    }

    static int ShowHelpAndReturn(int code) { ShowHelp(); return code; }
}

// Extension methods to access private methods for CLI commands
public static class PipelineExtensions
{
    public static async Task _StageDiscover(this Pipeline pipeline)
    {
        var method = typeof(Pipeline).GetMethod("StageDiscoverAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
            await (Task)method.Invoke(pipeline, null)!;
    }

    public static async Task _StageAnalyze(this Pipeline pipeline)
    {
        var method = typeof(Pipeline).GetMethod("StageAnalyzeAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
            await (Task)method.Invoke(pipeline, null)!;
    }
}