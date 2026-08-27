using Aqevryn.Agents;
using Aqevryn.Common;
using Aqevryn.Config;
using Aqevryn.Sources;
using Aqevryn.Publishing;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Orchestration;

public class Pipeline
{
    private readonly PipelineContext _ctx;
    private readonly AqevrynSettings _settings;
    private readonly ILogger<Pipeline> _logger;

    public Pipeline(PipelineContext ctx, AqevrynSettings settings, ILogger<Pipeline>? logger = null)
    {
        _ctx = ctx; _settings = settings;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Pipeline>.Instance;
    }

    public async Task<PipelineContext> RunAsync()
    {
        _logger.LogInformation("Pipeline started: {RunId}", _ctx.RunId);
        try
        {
            _ctx.Stage = "DISCOVERING"; await StageDiscoverAsync();
            _ctx.Stage = "ANALYZING"; await StageAnalyzeAsync();
            _ctx.Stage = "RESEARCHING"; await StageResearchAsync();
            _ctx.Stage = "WRITING"; await StageWriteAsync();
            _ctx.Stage = "REVIEWING"; await StageReviewAsync();
            _ctx.Stage = "PUBLISHING"; await StagePublishAsync();
            _ctx.Stage = "COMPLETED";
            _logger.LogInformation("Pipeline completed: {RunId}", _ctx.RunId);
        }
        catch (Exception ex)
        {
            _ctx.Stage = "FAILED"; _ctx.Error = ex.Message;
            _logger.LogError(ex, "Pipeline failed at stage {Stage}", _ctx.Stage);
        }
        return _ctx;
    }

    private async Task StageDiscoverAsync()
    {
        var sources = ConfigLoader.LoadSources("sources.yaml");
        if (sources.Count == 0)
        {
            sources = ConfigLoader.LoadSources("sources.example.yaml");
            if (sources.Count == 0) { _logger.LogWarning("No source config found"); return; }
        }

        var collector = new SourceCollector(sources, _settings);
        var items = await collector.CollectAllAsync();
        _ctx.Articles = items.Select(item => new Dictionary<string, object?>
        {
            ["id"] = item.ContentHash, ["title"] = item.Title, ["url"] = item.Url,
            ["source_type"] = item.SourceType, ["source_name"] = item.SourceName,
            ["content"] = item.Content ?? item.Summary ?? item.Title,
            ["summary"] = item.Summary ?? item.Title, ["tags"] = item.Tags ?? new(),
            ["category"] = item.Category ?? "technology", ["published_at"] = item.PublishedAt,
            ["author"] = item.Author,
        }).ToList();

        _logger.LogInformation("Collected {Count} articles", _ctx.Articles.Count);

        if (_ctx.Articles.Count > 0)
        {
            var discovery = new TopicDiscoveryAgent();
            var results = await discovery.DiscoverAsync(_ctx.Articles);
            _ctx.Topics = results.Select(r => new Dictionary<string, object?>
            {
                ["topic"] = r.Topic, ["summary"] = r.Summary, ["why_trending"] = r.WhyTrending,
                ["evidence"] = r.Evidence, ["related_topics"] = r.RelatedTopics, ["category"] = r.Category ?? "technology",
                ["id"] = r.Topic.GetHashCode(),
            }).ToList();
            _logger.LogInformation("Discovered {Count} topics", _ctx.Topics.Count);
        }
    }

    private async Task StageAnalyzeAsync()
    {
        if (_ctx.Topics.Count == 0) return;

        var clustering = new TopicClusteringAgent();
        _ctx.Clusters = (await clustering.ClusterAsync(_ctx.Topics)).Select(c => new Dictionary<string, object?>
        {
            ["canonical_topic"] = c.CanonicalTopic, ["member_topics"] = c.MemberTopics,
            ["article_count"] = c.ArticleCount, ["aliases"] = c.Aliases,
        }).ToList();

        var trend = new TrendAnalysisAgent();
        var researchability = new ResearchabilityAgent();
        var market = new MarketViabilityAgent();

        foreach (var cluster in _ctx.Clusters)
        {
            var topicName = cluster.GetValueOrDefault("canonical_topic")?.ToString() ?? "";
            if (string.IsNullOrEmpty(topicName)) continue;

            // Try to match cluster articles by member topics
            var clusterArticles = _ctx.Articles.Where(a =>
            {
                var text = $"{a.GetValueOrDefault("title")} {a.GetValueOrDefault("summary")}";
                if (cluster.GetValueOrDefault("member_topics") is List<object> members)
                {
                    foreach (var member in members)
                    {
                        var memberStr = member?.ToString() ?? "";
                        // Match on the member topic (e.g., "Artificial Intelligence" matches "AI")
                        if (!string.IsNullOrEmpty(memberStr) &&
                            text.Contains(memberStr, StringComparison.OrdinalIgnoreCase))
                            return true;
                        // Also match on individual words from the topic
                        foreach (var word in memberStr.Split(' '))
                        {
                            if (word.Length > 2 && text.Contains(word, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                    }
                }
                return false;
            }).ToList();

            // If no articles matched the cluster, use all articles as fallback
            if (clusterArticles.Count == 0)
                clusterArticles = _ctx.Articles;

            var tr = await trend.AnalyzeAsync(topicName, clusterArticles);
            _ctx.TrendScores[topicName] = tr.Score;

            var rr = await researchability.EvaluateAsync(topicName, clusterArticles);
            _ctx.ResearchabilityScores[topicName] = rr.Score;

            var mr = await market.EvaluateAsync(topicName, clusterArticles);
            _ctx.MarketScores[topicName] = mr.Score;

            _ctx.ArticleCounts[topicName] = clusterArticles.Count;
        }

        // The Rank method expects a "topic" key, but clusters use "canonical_topic"
        // Remap clusters to have the "topic" key expected by the ranker
        var rankerTopics = _ctx.Clusters.Select(c => new Dictionary<string, object?>
        {
            ["topic"] = c.GetValueOrDefault("canonical_topic"),
            ["category"] = c.GetValueOrDefault("canonical_topic"),
        }).ToList();

        var ranker = new TopicRanker();
        _ctx.RankedTopics = ranker.Rank(rankerTopics, _ctx.TrendScores, _ctx.ResearchabilityScores, _ctx.MarketScores, _ctx.ArticleCounts);
        var selected = ranker.SelectTopN(_ctx.RankedTopics, 1);
        _ctx.SelectedTopic = selected.FirstOrDefault();

        if (_ctx.SelectedTopic != null)
            _logger.LogInformation("Selected topic: {Topic} (score: {Score})", _ctx.SelectedTopic.Topic, _ctx.SelectedTopic.FinalScore);
    }

    private async Task StageResearchAsync()
    {
        if (_ctx.SelectedTopic == null) return;
        var topicName = _ctx.SelectedTopic.Topic;

        var topicArticles = _ctx.Articles.Where(a =>
            $"{a.GetValueOrDefault("title")} {a.GetValueOrDefault("summary")}".Contains(topicName, StringComparison.OrdinalIgnoreCase)).ToList();

        var planner = new ResearchPlannerAgent();
        _ctx.ResearchPlan = await planner.CreatePlanAsync(topicName,
            _ctx.ResearchabilityScores.GetValueOrDefault(topicName),
            _ctx.TrendScores.GetValueOrDefault(topicName),
            _ctx.MarketScores.GetValueOrDefault(topicName), topicArticles);

        var researcher = new ResearchAgent();
        _ctx.ResearchResult = await researcher.ResearchAsync(_ctx.ResearchPlan.ResearchQuestion, topicName, topicArticles);

        _logger.LogInformation("Research complete: {Count} sources, {Findings} findings",
            _ctx.ResearchResult.SourcesAnalyzed.Count, _ctx.ResearchResult.Findings.Count);
    }

    private async Task StageWriteAsync()
    {
        if (_ctx.ResearchResult == null) return;
        var writer = new ArticleWriterAgent();
        _ctx.GeneratedArticle = await writer.WriteAsync(_ctx.SelectedTopic?.Topic ?? "Research Topic", _ctx.ResearchResult);
        _logger.LogInformation("Article written: {Title}", _ctx.GeneratedArticle.Title);
    }

    private async Task StageReviewAsync()
    {
        if (_ctx.GeneratedArticle == null) return;
        var reviewer = new EditorialReviewAgent();
        _ctx.EditorialReview = await reviewer.ReviewAsync(_ctx.GeneratedArticle, _settings.MinPublicationScore);
        _logger.LogInformation("Review: {Score} - {Recommendation}", _ctx.EditorialReview.OverallScore, _ctx.EditorialReview.PublishRecommendation);
    }

    private async Task StagePublishAsync()
    {
        if (_ctx.GeneratedArticle == null || _ctx.EditorialReview == null) return;

        if (_ctx.EditorialReview.PublishRecommendation != "APPROVE" || _ctx.EditorialReview.OverallScore < _settings.MinPublicationScore)
        {
            _logger.LogInformation("Article not approved for publication");
            return;
        }

        var topicName = _ctx.SelectedTopic?.Topic ?? "Research";
        var mdGen = new MarkdownGenerator();
        var scores = new Dictionary<string, double>
        {
            ["trend"] = _ctx.TrendScores.GetValueOrDefault(topicName),
            ["editorial"] = _ctx.EditorialReview.OverallScore,
        };

        var markdown = mdGen.Generate(_ctx.GeneratedArticle, scores);
        var filename = mdGen.GetFilename(_ctx.GeneratedArticle.Slug);
        _ctx.MarkdownArticle = markdown;
        _ctx.MarkdownFilename = filename;

        if (!_settings.DryRun && !string.IsNullOrEmpty(_settings.GitHubToken))
        {
            var publisher = new GitHubPublisher(_settings.GitHubToken, _settings.GitHubOwner,
                _settings.GitHubRepository, _settings.GitHubDefaultBranch, _settings.AutoPublish);
            var result = await publisher.PublishAsync(markdown, filename, topicName, scores, _settings.DryRun);
            _ctx.PublishSuccess = result.Success;
            _ctx.PublishBranch = result.Branch;
            _ctx.PublishPrNumber = result.PrNumber;
            _ctx.PublishPrUrl = result.PrUrl;
            _logger.LogInformation("Publish result: {Success}, PR: {PrUrl}", result.Success, result.PrUrl);
        }
        else
        {
            _logger.LogInformation("Dry run or no GitHub token — skipping publish");
        }
    }
}