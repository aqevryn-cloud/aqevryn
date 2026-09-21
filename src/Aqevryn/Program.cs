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
                "run" => await RunPipeline(settings, dryRun, verbose),
                "discover" => await RunDiscover(settings, verbose),
                "analyze" => await RunAnalyze(settings, verbose),
                "research" => await RunResearch(settings, verbose),
                "write" => await RunWrite(settings),
                "review" => await RunReview(settings),
                "publish" => await RunPublish(settings, dryRun),
                "health" => RunHealth(),
                "build-site" => await RunBuildSite(),
                "scheduler" => await RunScheduler(settings),
                "api" => await RunApi(),
                "web" => await RunWeb(),
                "test-github" => await RunTestGitHub(settings),
                "moltbook" => await RunMoltbook(settings, commandArgs),
                "dashboard" => RunDashboard(settings),
                "watch" => await RunWatch(settings),
                "logs" => RunLogs(),
                _ => ShowHelpAndReturn(1),
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Command failed");
            return 1;
        }
    }

    static async Task<int> RunPipeline(AqevrynSettings settings, bool dryRun, bool verbose)
    {
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║        Aqevryn Research Pipeline     ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.WriteLine();

        var ctx = new PipelineContext { DryRun = dryRun };
        var pipeline = new Pipeline(ctx, settings);

        // Stage 1: Discover
        Console.Write("• [1/9] Collecting technology sources... ");
        await pipeline._StageDiscover();
        Console.WriteLine($"{ctx.Articles.Count} new articles, {ctx.Topics.Count} topics");

        // Stage 2: Analyze
        Console.Write("• [2/9] Extracting topics... ");
        Console.Write("• [3/9] Clustering... ");
        Console.Write("• [4/9] Trend analysis... ");
        Console.Write("• [5/9] Researchability... ");
        Console.Write("• [6/9] Market viability... ");
        Console.WriteLine("done.");
        if (ctx.Articles.Count > 0)
        {
            await pipeline._StageAnalyze();
            if (ctx.SelectedTopic != null)
                Console.WriteLine($"  → Selected: \"{ctx.SelectedTopic.Topic}\" (Score: {ctx.SelectedTopic.FinalScore})");
            else
                Console.WriteLine("  → No topic met the selection criteria.");
        }
        else
        {
            Console.WriteLine("  → No articles collected — skipping analysis.");
        }

        // Stage 3: Research
        Console.Write("• [7/9] Researching... ");
        if (ctx.SelectedTopic != null)
        {
            await pipeline._StageResearch();
            Console.WriteLine($"{ctx.ResearchResult?.SourcesAnalyzed.Count ?? 0} sources, {ctx.ResearchResult?.Findings.Count ?? 0} findings");
        }
        else
        {
            Console.WriteLine("skipped (no topic).");
        }

        // Stage 4: Write
        Console.Write("• [8/9] Generating article... ");
        if (ctx.ResearchResult != null)
        {
            await pipeline._StageWrite();
            Console.WriteLine($"\"{ctx.GeneratedArticle?.Title ?? "untitled"}\"");
        }
        else
        {
            Console.WriteLine("skipped (no research).");
        }

        // Stage 5: Review
        Console.Write("• [9/9] Editorial review... ");
        if (ctx.GeneratedArticle != null)
        {
            await pipeline._StageReview();
            Console.WriteLine($"Score: {ctx.EditorialReview?.OverallScore ?? 0} — {ctx.EditorialReview?.PublishRecommendation ?? "N/A"}");
        }
        else
        {
            Console.WriteLine("skipped (no article).");
        }

        // Stage 6: Publish
        Console.Write("• Publishing... ");
        if (ctx.EditorialReview?.PublishRecommendation == "APPROVE" && ctx.GeneratedArticle != null)
        {
            await pipeline._StagePublish();
            if (ctx.PublishPrUrl != null)
                Console.WriteLine($"PR created: {ctx.PublishPrUrl}");
            else if (ctx.PublishSuccess == true)
                Console.WriteLine("Branch created, awaiting human approval.");
            else
                Console.WriteLine("Skipped (dry run or no GitHub token).");
        }
        else
        {
            Console.WriteLine("Article not approved for publication.");
        }

        Console.WriteLine();
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║        Pipeline Completed             ║");
        Console.WriteLine("╚══════════════════════════════════════╝");

        if (verbose && ctx.SelectedTopic != null)
        {
            Console.WriteLine();
            Console.WriteLine("Summary:");
            Console.WriteLine($"  Topic:          {ctx.SelectedTopic.Topic}");
            Console.WriteLine($"  Final Score:    {ctx.SelectedTopic.FinalScore}");
            Console.WriteLine($"  Trend Score:    {ctx.SelectedTopic.TrendScore}");
            Console.WriteLine($"  Researchability: {ctx.SelectedTopic.ResearchabilityScore}");
            Console.WriteLine($"  Market Viability: {ctx.SelectedTopic.MarketViabilityScore}");
            Console.WriteLine($"  Articles:       {ctx.Articles.Count}");
            Console.WriteLine($"  Editorial Score: {ctx.EditorialReview?.OverallScore ?? 0}");
        }

        return 0;
    }

    static async Task<int> RunDiscover(AqevrynSettings settings, bool verbose)
    {
        Console.WriteLine("Collecting technology sources...");
        var ctx = new PipelineContext { DryRun = true };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline._StageDiscover();
        Console.WriteLine($"  {ctx.Articles.Count} new articles found");
        Console.WriteLine($"  {ctx.Topics.Count} candidate topics");
        if (verbose && ctx.Topics.Count > 0)
        {
            foreach (var topic in ctx.Topics)
                Console.WriteLine($"    - {topic.GetValueOrDefault("topic")}");
        }
        return 0;
    }

    static async Task<int> RunAnalyze(AqevrynSettings settings, bool verbose)
    {
        Console.WriteLine("Analyzing topics...");
        var ctx = new PipelineContext { DryRun = true };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline._StageDiscover();
        await pipeline._StageAnalyze();
        if (ctx.SelectedTopic != null)
        {
            Console.WriteLine($"  Top topic: {ctx.SelectedTopic.Topic}");
            Console.WriteLine($"  Trend Score: {ctx.SelectedTopic.TrendScore}");
            Console.WriteLine($"  Researchability: {ctx.SelectedTopic.ResearchabilityScore}");
            Console.WriteLine($"  Market Viability: {ctx.SelectedTopic.MarketViabilityScore}");
            Console.WriteLine($"  Final Score: {ctx.SelectedTopic.FinalScore}");
        }
        else
        {
            Console.WriteLine("  No topic selected.");
        }
        return 0;
    }

    static async Task<int> RunResearch(AqevrynSettings settings, bool verbose)
    {
        Console.WriteLine("Researching...");
        var ctx = new PipelineContext { DryRun = true };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline._StageDiscover();
        await pipeline._StageAnalyze();
        await pipeline._StageResearch();
        if (ctx.ResearchResult != null)
            Console.WriteLine($"  {ctx.ResearchResult.SourcesAnalyzed.Count} sources analyzed, {ctx.ResearchResult.Findings.Count} findings");
        else
            Console.WriteLine("  No research performed.");
        return 0;
    }

    static async Task<int> RunWrite(AqevrynSettings settings)
    {
        var ctx = new PipelineContext { DryRun = true };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline._StageDiscover();
        await pipeline._StageAnalyze();
        await pipeline._StageResearch();
        await pipeline._StageWrite();
        if (ctx.GeneratedArticle != null)
            Console.WriteLine($"Article generated: {ctx.GeneratedArticle.Title}");
        else
            Console.WriteLine("No article generated.");
        return 0;
    }

    static async Task<int> RunReview(AqevrynSettings settings)
    {
        var ctx = new PipelineContext { DryRun = true };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline._StageDiscover();
        await pipeline._StageAnalyze();
        await pipeline._StageResearch();
        await pipeline._StageWrite();
        await pipeline._StageReview();
        if (ctx.EditorialReview != null)
        {
            Console.WriteLine($"Editorial Score: {ctx.EditorialReview.OverallScore}");
            Console.WriteLine($"Recommendation: {ctx.EditorialReview.PublishRecommendation}");
        }
        else
        {
            Console.WriteLine("No review performed.");
        }
        return 0;
    }

    static async Task<int> RunPublish(AqevrynSettings settings, bool dryRun)
    {
        Console.WriteLine("Publishing...");
        if (dryRun) { Console.WriteLine("Dry-run: skipping publication"); return 0; }
        var ctx = new PipelineContext { DryRun = dryRun };
        var pipeline = new Pipeline(ctx, settings);
        await pipeline._StageDiscover();
        await pipeline._StageAnalyze();
        await pipeline._StageResearch();
        await pipeline._StageWrite();
        await pipeline._StageReview();
        await pipeline._StagePublish();
        if (ctx.PublishPrUrl != null) Console.WriteLine($"PR: {ctx.PublishPrUrl}");
        else if (ctx.PublishSuccess == true) Console.WriteLine("Branch created.");
        else Console.WriteLine("No article to publish.");
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

    static async Task<int> RunWeb()
    {
        Console.WriteLine("Web dashboard starting on http://localhost:9888 ...");
        await Api.WebDashboard.RunAsync(9888);
        return 0;
    }

    static async Task<int> RunTestGitHub(AqevrynSettings settings)
    {
        Console.WriteLine("Testing GitHub connection...");
        Console.WriteLine($"  Owner: {settings.GitHubOwner}");
        Console.WriteLine($"  Repo:  {settings.GitHubRepository}");
        Console.WriteLine($"  Token: {settings.GitHubToken[..Math.Min(10, settings.GitHubToken.Length)]}...");
        Console.WriteLine();

        var mgr = new Publishing.GitHubRepositoryManager(
            settings.GitHubToken,
            settings.GitHubOwner,
            settings.GitHubRepository,
            settings.GitHubDefaultBranch
        );

        Console.WriteLine("Checking if repository exists...");
        var exists = await mgr.RepositoryExistsAsync();
        Console.WriteLine($"  Repository exists: {exists}");

        if (!exists)
        {
            Console.WriteLine("Creating repository...");
            var created = await mgr.EnsureRepositoryAsync();
            Console.WriteLine($"  Repository created: {created}");

            if (created)
            {
                Console.WriteLine("Scaffolding directories...");
                await mgr.ScaffoldRepositoryAsync();
                Console.WriteLine("  Done.");
            }
            else
            {
                Console.WriteLine("  FAILED! Check your token has 'repo' scope.");
                return 1;
            }
        }

        Console.WriteLine($"\n  View at: https://github.com/{settings.GitHubOwner}/{settings.GitHubRepository}");
        return 0;
    }

    static async Task<int> RunMoltbook(AqevrynSettings settings, string[] commandArgs)
    {
        var client = new Common.MoltbookClient();

        var sub = commandArgs.Length > 1 ? commandArgs[1].ToLower() : null;
        if (sub == null || sub == "register")
        {
            Console.WriteLine("Registering Aqevryn on Moltbook...");
            var result = await client.RegisterAsync("Aqevryn");
            if (result.Success)
            {
                Console.WriteLine($"  ✅ Registered successfully!");
                Console.WriteLine($"  Agent: {result.AgentName}");
                Console.WriteLine($"  API Key: {result.ApiKey}");
                Console.WriteLine($"  Claim URL: {result.ClaimUrl}");
                Console.WriteLine($"  Verification Code: {result.VerificationCode}");
                Console.WriteLine();
                Console.WriteLine($"  ⚠️  SAVE YOUR API KEY! It is shown only once.");
                Console.WriteLine($"  📧 Send the claim URL to your human to verify ownership.");
            }
            else
            {
                Console.WriteLine($"  ❌ Registration failed: {result.Error}");
            }
            return result.Success ? 0 : 1;
        }

        if (!client.IsRegistered)
        {
            Console.WriteLine("  ❌ Not registered on Moltbook.");
            return 1;
        }

        return sub switch
        {
            "status" => await RunMoltbookStatus(client),
            "post" => await RunMoltbookPost(client),
            "feed" => await RunMoltbookFeed(client),
            "home" => await RunMoltbookHome(client),
            "profile" => await RunMoltbookProfile(client),
            "list" => await RunMoltbookList(client),
            "delete" => await RunMoltbookDelete(client, commandArgs),
            "engage" => await RunMoltbookEngage(settings, client),
            _ => ShowMoltbookHelp(),
        };
    }

    static async Task<int> RunMoltbookStatus(Common.MoltbookClient client)
    {
        var status = await client.CheckStatusAsync();
        Console.WriteLine($"Moltbook Status: {status}");
        return 0;
    }

    static async Task<int> RunMoltbookPost(Common.MoltbookClient client)
    {
        Console.WriteLine("Posting to Moltbook...");
        var result = await client.PostResearchAsync(
            "Research Update",
            "Latest research from Aqevryn",
            "https://github.com/aqevryn-cloud/aqevryn",
            "Aqevryn has completed new technology research."
        );
        if (result.Success)
        {
            Console.WriteLine($"  ✅ Posted! Post ID: {result.PostId}");
            if (result.VerificationRequired)
            {
                Console.WriteLine("  ⚠️  Verification required. Solve the challenge to publish.");
                Console.WriteLine($"  Challenge: {result.ChallengeText}");
            }
        }
        else
        {
            Console.WriteLine($"  ❌ Failed: {result.Error}");
        }
        return result.Success ? 0 : 1;
    }

    static async Task<int> RunMoltbookFeed(Common.MoltbookClient client)
    {
        var feed = await client.GetFeedAsync("hot", 10);
        Console.WriteLine("Moltbook Feed:");
        Console.WriteLine(feed);
        return 0;
    }

    static async Task<int> RunMoltbookHome(Common.MoltbookClient client)
    {
        var home = await client.GetHomeAsync();
        Console.WriteLine("Moltbook Home:");
        Console.WriteLine(home);
        return 0;
    }

    static async Task<int> RunMoltbookProfile(Common.MoltbookClient client)
    {
        var profile = await client.GetProfileAsync();
        Console.WriteLine("Moltbook Profile:");
        Console.WriteLine(profile);
        return 0;
    }

    static async Task<int> RunMoltbookList(Common.MoltbookClient client)
    {
        var posts = await client.GetMyPostsAsync(30);
        Console.WriteLine("My Posts:");
        // Parse and show only Aqevryn's posts
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(posts);
            if (doc.RootElement.TryGetProperty("posts", out var p) && p.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var post in p.EnumerateArray())
                {
                    var aid = post.TryGetProperty("author", out var a)
                        ? (a.TryGetProperty("name", out var n) ? n.GetString() : "") : "";
                    if (aid?.ToLower() == "aqevryn")
                    {
                        var id = post.TryGetProperty("id", out var i) ? i.GetString() : "";
                        var title = post.TryGetProperty("title", out var t) ? t.GetString() : "";
                        Console.WriteLine($"  ID: {id}  Title: {title}");
                    }
                }
            }
        }
        catch { Console.WriteLine("  (error parsing posts)"); }
        return 0;
    }

    static async Task<int> RunMoltbookDelete(Common.MoltbookClient client, string[] args)
    {
        var postId = args.Length > 2 ? args[2] : null;
        if (string.IsNullOrEmpty(postId))
        {
            Console.WriteLine("Usage: aqevryn moltbook delete <post-id>");
            Console.WriteLine("Run 'aqevryn moltbook list' to find your post IDs.");
            return 1;
        }
        Console.WriteLine($"Deleting post {postId}...");
        var result = await client.DeletePostAsync(postId);
        if (result.Success)
            Console.WriteLine($"  ✅ Deleted");
        else
            Console.WriteLine($"  ❌ Failed: {result.Error}");
        return result.Success ? 0 : 1;
    }

    static async Task<int> RunMoltbookEngage(AqevrynSettings settings, Common.MoltbookClient client)
    {
        var engager = new Common.MoltbookEngager(client, settings);
        Console.WriteLine("🤖 Aqevryn browsing Moltbook for interesting discussions...");
        await engager.RunEngagementCycleAsync();
        Console.WriteLine("✅ Engagement cycle complete.");
        return 0;
    }

    static int ShowMoltbookHelp()
    {
        Console.WriteLine("Moltbook — Social Network for AI Agents");
        Console.WriteLine();
        Console.WriteLine("Subcommands:");
        Console.WriteLine("  register     Register Aqevryn on Moltbook");
        Console.WriteLine("  status       Check claim status");
        Console.WriteLine("  post         Post research to Moltbook");
        Console.WriteLine("  feed         View the Moltbook feed");
        Console.WriteLine("  home         View your home dashboard");
        Console.WriteLine("  profile      View your profile");
        Console.WriteLine("  list         List your posts");
        Console.WriteLine("  delete       Delete a post");
        Console.WriteLine("  engage       Browse and comment on trending discussions");
        return 0;
    }

    static int RunDashboard(AqevrynSettings settings)
    {
        var summary = ActivityRegistry.GetSummary();
        
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║       Aqevryn — Dashboard            ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Pipeline:");
        Console.WriteLine($"  Total Runs:     {summary.TotalRuns}");
        Console.WriteLine($"  Runs Today:     {summary.RunsToday}");
        Console.WriteLine($"  Last Run:       {summary.LastRunTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "never"}");
        Console.WriteLine($"  Last Status:    {summary.LastRunStatus ?? "-"} ({summary.LastRunDuration?.TotalSeconds:F0}s)");
        Console.WriteLine($"  Last Output:    {summary.LastRunOutput ?? "-"}");
        Console.WriteLine();
        Console.WriteLine("Today's Activity:");
        Console.WriteLine($"  Articles Collected: {summary.ArticlesCollectedToday}");
        Console.WriteLine($"  Topics Discovered:  {summary.TopicsDiscoveredToday}");
        Console.WriteLine($"  Articles Published: {summary.ArticlesPublishedToday}");
        Console.WriteLine();
        Console.WriteLine("GitHub:");
        Console.WriteLine($"  Owner: {settings.GitHubOwner}");
        Console.WriteLine($"  Repo:  {settings.GitHubRepository}");
        Console.WriteLine($"  View:  https://github.com/{settings.GitHubOwner}/{settings.GitHubRepository}");
        Console.WriteLine();
        
        if (summary.RecentErrors.Count > 0)
        {
            Console.WriteLine("Recent Errors:");
            foreach (var err in summary.RecentErrors.Take(5))
                Console.WriteLine($"  ! {err}");
            Console.WriteLine();
        }
        
        Console.WriteLine("Recent Activity:");
        var recent = ActivityRegistry.GetRecent(10);
        foreach (var log in recent)
        {
            var icon = log.Status == "COMPLETED" ? "✓" : log.Status == "FAILED" ? "✗" : "○";
            var time = log.StartedAt.ToString("HH:mm:ss");
            var dur = log.Duration?.TotalSeconds;
            var durStr = dur.HasValue ? $" ({dur:F1}s)" : "";
            Console.WriteLine($"  {icon} [{time}] {log.AgentName} ({log.Stage}){durStr}");
            if (log.OutputSummary != null)
                Console.WriteLine($"      → {log.OutputSummary}");
        }
        
        return 0;
    }

    static async Task<int> RunWatch(AqevrynSettings settings)
    {
        Console.WriteLine("Watching pipeline activity. Press Ctrl+C to stop.");
        Console.WriteLine();
        
        var lastCount = 0;
        while (true)
        {
            var recent = ActivityRegistry.GetRecent(20);
            if (recent.Count != lastCount)
            {
                Console.Clear();
                RunDashboard(settings);
                lastCount = recent.Count;
            }
            await Task.Delay(2000);
        }
    }

    static int RunLogs()
    {
        var logs = ActivityRegistry.GetRecent(30);
        
        Console.WriteLine("╔══════════════════════════════════════╗");
        Console.WriteLine("║       Aqevryn — Activity Log         ║");
        Console.WriteLine("╚══════════════════════════════════════╝");
        Console.WriteLine();
        
        if (logs.Count == 0)
        {
            Console.WriteLine("No activity recorded yet. Run 'aqevryn run' first.");
            return 0;
        }
        
        foreach (var log in logs)
        {
            var icon = log.Status == "COMPLETED" ? "✅" : log.Status == "FAILED" ? "❌" : "🔄";
            var time = log.StartedAt.ToString("yyyy-MM-dd HH:mm:ss");
            var dur = log.Duration?.TotalSeconds;
            var durStr = dur.HasValue ? $" [{dur:F1}s]" : "";
            
            Console.WriteLine($"{icon} [{time}] {log.AgentName}.{log.Stage}{durStr}");
            if (log.InputSummary != null) Console.WriteLine($"   In:  {log.InputSummary}");
            if (log.OutputSummary != null) Console.WriteLine($"   Out: {log.OutputSummary}");
            if (log.Error != null) Console.WriteLine($"   Err: {log.Error}");
            Console.WriteLine();
        }
        
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
        Console.WriteLine("  web          Start the web dashboard (port 9888)");
        Console.WriteLine("  health       Check application health");
        Console.WriteLine("  build-site   Build the static research website");
        Console.WriteLine("  test-github  Test GitHub connection and create repository if needed");
        Console.WriteLine("  moltbook     Interact with Moltbook (social network for AI agents)");
        Console.WriteLine("               Subcommands: register, status, post, feed, home, profile");
        Console.WriteLine("  dashboard    Show pipeline activity dashboard");
        Console.WriteLine("  watch        Live-update dashboard (auto-refresh)");
        Console.WriteLine("  logs         Show recent agent activity log");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --dry-run    Run without publishing");
        Console.WriteLine("  -v           Verbose output");
        Console.WriteLine("  --help       Show help");
    }

    static int ShowHelpAndReturn(int code) { ShowHelp(); return code; }
}

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

    public static async Task _StageResearch(this Pipeline pipeline)
    {
        var method = typeof(Pipeline).GetMethod("StageResearchAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
            await (Task)method.Invoke(pipeline, null)!;
    }

    public static async Task _StageWrite(this Pipeline pipeline)
    {
        var method = typeof(Pipeline).GetMethod("StageWriteAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
            await (Task)method.Invoke(pipeline, null)!;
    }

    public static async Task _StageReview(this Pipeline pipeline)
    {
        var method = typeof(Pipeline).GetMethod("StageReviewAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
            await (Task)method.Invoke(pipeline, null)!;
    }

    public static async Task _StagePublish(this Pipeline pipeline)
    {
        var method = typeof(Pipeline).GetMethod("StagePublishAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (method != null)
            await (Task)method.Invoke(pipeline, null)!;
    }
}