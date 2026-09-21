using Aqevryn.Config;
using Aqevryn.Orchestration;
using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Scheduler;

public class AqevrynScheduler
{
    private readonly AqevrynSettings _settings;
    private readonly ILogger<AqevrynScheduler> _logger;
    private bool _running;
    private DateTime? _lastCollect;
    private DateTime? _lastAnalyze;
    private DateTime? _lastResearch;
    private DateTime? _lastMoltybook;

    public AqevrynScheduler(AqevrynSettings settings, ILogger<AqevrynScheduler>? logger = null)
    {
        _settings = settings;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<AqevrynScheduler>.Instance;
    }

    public async Task StartAsync()
    {
        // Start the web dashboard in the background
        _ = Task.Run(async () =>
        {
            try
            {
                await Api.WebDashboard.RunAsync(9888);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Web dashboard failed to start");
            }
        });

        // Small delay to let web server start
        await Task.Delay(500);

        _running = true;
        _logger.LogInformation("Scheduler started (collect: {Co}h, analyze: {An}h, research: {Re}h)",
            _settings.CollectIntervalHours, _settings.AnalyzeIntervalHours, _settings.RankIntervalHours);

        Console.CancelKeyPress += (_, e) => { _running = false; e.Cancel = true; };

        while (_running)
        {
            var now = DateTime.UtcNow;

            if (ShouldRun("collect", now, ref _lastCollect, _settings.CollectIntervalHours))
                await RunCollectAsync();

            if (ShouldRun("analyze", now, ref _lastAnalyze, _settings.AnalyzeIntervalHours))
                await RunAnalyzeAsync();

            if (ShouldRun("research", now, ref _lastResearch, _settings.RankIntervalHours))
                await RunFullPipelineAsync();

            // Moltbook engagement every 30 minutes
            if (ShouldRun("moltbook", now, ref _lastMoltybook, 1))
                await RunMoltbookEngagementAsync();

            await Task.Delay(TimeSpan.FromSeconds(60));
        }
    }

    private static bool ShouldRun(string name, DateTime now, ref DateTime? last, int hours)
    {
        if (last == null) { last = now; return true; }
        return (now - last.Value).TotalHours >= hours;
    }

    private async Task RunCollectAsync()
    {
        _logger.LogInformation("Scheduled: collecting sources");
        try
        {
            var ctx = new PipelineContext { DryRun = true };
            var pipeline = new Pipeline(ctx, _settings);
            await pipeline.RunAsync();
            _logger.LogInformation("Collected {A} articles, {T} topics", ctx.Articles.Count, ctx.Topics.Count);
        }
        catch (Exception ex) { _logger.LogError(ex, "Collect failed"); }
    }

    private async Task RunAnalyzeAsync()
    {
        _logger.LogInformation("Scheduled: analyzing trends");
        try
        {
            var ctx = new PipelineContext { DryRun = true };
            var pipeline = new Pipeline(ctx, _settings);
            await pipeline.RunAsync();
            _logger.LogInformation("Ranked {R} topics", ctx.RankedTopics.Count);
        }
        catch (Exception ex) { _logger.LogError(ex, "Analyze failed"); }
    }

    private async Task RunFullPipelineAsync()
    {
        _logger.LogInformation("Scheduled: full research pipeline");
        try
        {
            var ctx = new PipelineContext { DryRun = _settings.DryRun };
            var pipeline = new Pipeline(ctx, _settings);
            await pipeline.RunAsync();
            if (ctx.PublishPrUrl != null)
                _logger.LogInformation("Published PR: {Url}", ctx.PublishPrUrl);
        }
        catch (Exception ex) { _logger.LogError(ex, "Pipeline failed"); }
    }

    private async Task RunMoltbookEngagementAsync()
    {
        _logger.LogInformation("Moltbook engagement cycle");
        try
        {
            var client = new Common.MoltbookClient();
            if (client.IsRegistered)
            {
                var engager = new Common.MoltbookEngager(client, _settings);
                await engager.RunEngagementCycleAsync();
                _logger.LogInformation("Moltbook engagement complete");
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Moltbook engagement failed"); }
    }
}