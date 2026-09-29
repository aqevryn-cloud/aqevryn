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
    private readonly LLMClient? _llm;

    public Pipeline(PipelineContext ctx, AqevrynSettings settings, ILogger<Pipeline>? logger = null)
    {
        _ctx = ctx; _settings = settings;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<Pipeline>.Instance;
        _llm = CreateLLM();
    }

    private LLMClient? CreateLLM()
    {
        if (_settings.LlmProvider == "mock" || string.IsNullOrEmpty(_settings.LlmApiKey))
            return null;
        try
        {
            var llm = new LLMClient(_settings);
            _logger.LogInformation("LLM client created: provider={Provider}, model={Model}",
                _settings.LlmProvider, _settings.LlmModel);
            return llm;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create LLM client");
            return null;
        }
    }

    public async Task<PipelineContext> RunAsync()
    {
        _logger.LogInformation("Pipeline started: {RunId}", _ctx.RunId);
        ActivityRegistry.LogStart("pipeline", "pipeline", $"RunId: {_ctx.RunId}, DryRun: {_ctx.DryRun}");
        try
        {
            _ctx.Stage = "DISCOVERING"; await StageDiscoverAsync();
            _ctx.Stage = "ANALYZING"; await StageAnalyzeAsync();
            _ctx.Stage = "RESEARCHING"; await StageResearchAsync();
            _ctx.Stage = "WRITING"; await StageWriteAsync();
            _ctx.Stage = "REVIEWING"; await StageReviewAsync();
            _ctx.Stage = "PUBLISHING"; await StagePublishAsync();
            _ctx.Stage = "COMPLETED";
            var elapsed = DateTime.UtcNow - _ctx.StartTime;
            _logger.LogInformation("Pipeline completed: {RunId}", _ctx.RunId);
            ActivityRegistry.LogComplete("pipeline", "pipeline",
                $"Completed in {elapsed.TotalSeconds:F0}s. Topic: {_ctx.SelectedTopic?.Topic ?? "none"}, " +
                $"Articles: {_ctx.Articles.Count}, PR: {_ctx.PublishPrUrl ?? "none"}",
                $"Stage: {_ctx.Stage}, Duration: {elapsed.TotalSeconds:F1}s");
        }
        catch (Exception ex)
        {
            _ctx.Stage = "FAILED"; _ctx.Error = ex.Message;
            _logger.LogError(ex, "Pipeline failed at stage {Stage}", _ctx.Stage);
            ActivityRegistry.LogError("pipeline", "pipeline", $"{_ctx.Stage}: {ex.Message}");
        }
        return _ctx;
    }

    private async Task StageDiscoverAsync()
    {
        ActivityRegistry.LogStart("source_collector", "discover");
        var sources = ConfigLoader.LoadSources("sources.yaml");
        if (sources.Count == 0)
        {
            sources = ConfigLoader.LoadSources("sources.example.yaml");
            if (sources.Count == 0) { _logger.LogWarning("No source config found"); ActivityRegistry.LogComplete("source_collector", "discover", "0 articles (no sources)"); return; }
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
        ActivityRegistry.LogComplete("source_collector", "discover",
            $"{_ctx.Articles.Count} articles from {sources.Count} sources");

        if (_ctx.Articles.Count > 0)
        {
            ActivityRegistry.LogStart("topic_discovery", "topic_discovery", $"{_ctx.Articles.Count} articles");
            var discovery = new TopicDiscoveryAgent(_llm);
            var results = await discovery.DiscoverAsync(_ctx.Articles);
            _ctx.Topics = results.Select(r => new Dictionary<string, object?>
            {
                ["topic"] = r.Topic, ["summary"] = r.Summary, ["why_trending"] = r.WhyTrending,
                ["evidence"] = r.Evidence, ["related_topics"] = r.RelatedTopics, ["category"] = r.Category ?? "technology",
                ["id"] = r.Topic.GetHashCode(),
            }).ToList();
            _logger.LogInformation("Discovered {Count} topics", _ctx.Topics.Count);
            ActivityRegistry.LogComplete("topic_discovery", "topic_discovery",
                $"{_ctx.Topics.Count} topics discovered",
                $"Topics: {string.Join(", ", _ctx.Topics.Select(t => t.GetValueOrDefault("topic")))}");
        }

        // Save articles and topics to JSON for web dashboard
        SaveDataForWeb();
    }

    private void SaveDataForWeb()
    {
        try
        {
            var dataDir = Path.Combine(AppContext.BaseDirectory, "webdata");
            Directory.CreateDirectory(dataDir);

            var articles = _ctx.Articles.Select(a => new Dictionary<string, object?>
            {
                ["title"] = a.GetValueOrDefault("title")?.ToString()?[..Math.Min(150, a.GetValueOrDefault("title")?.ToString()?.Length ?? 0)],
                ["url"] = a.GetValueOrDefault("url"),
                ["source_name"] = a.GetValueOrDefault("source_name"),
                ["source_type"] = a.GetValueOrDefault("source_type"),
                ["category"] = a.GetValueOrDefault("category"),
                ["summary"] = a.GetValueOrDefault("summary")?.ToString()?[..Math.Min(300, a.GetValueOrDefault("summary")?.ToString()?.Length ?? 0)],
            }).ToList();

            var topics = _ctx.Topics.Select(t => new Dictionary<string, object?>
            {
                ["topic"] = t.GetValueOrDefault("topic"),
                ["summary"] = t.GetValueOrDefault("summary"),
                ["category"] = t.GetValueOrDefault("category"),
                ["evidence_count"] = (t.GetValueOrDefault("evidence") as List<object>)?.Count ?? 0,
            }).ToList();

            var payload = new { articles, topics, collected_at = DateTime.UtcNow.ToString("o") };
            var json = System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(dataDir, "pipeline_data.json"), json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save web data");
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

        // Filter out already-researched topics so we don't repeat them
        var availableTopics = rankerTopics.Where(t =>
        {
            var name = t.GetValueOrDefault("topic")?.ToString() ?? "";
            var alreadyDone = CompletedResearchRegistry.IsAlreadyResearched(name);
            if (alreadyDone)
                _logger.LogInformation("Skipping already-researched topic: {Topic}", name);
            return !alreadyDone;
        }).ToList();

        // If all topics have been researched, don't repeat without permission
        if (availableTopics.Count == 0)
        {
            _logger.LogInformation("All topics have been researched. No new topics available. Pipeline will wait for fresh sources.");
            _ctx.SelectedTopic = null;
            return;
        }

        _ctx.RankedTopics = ranker.Rank(availableTopics, _ctx.TrendScores, _ctx.ResearchabilityScores, _ctx.MarketScores, _ctx.ArticleCounts);
        var selected = ranker.SelectTopN(_ctx.RankedTopics, 1);
        _ctx.SelectedTopic = selected.FirstOrDefault();

        if (_ctx.SelectedTopic != null)
            _logger.LogInformation("Selected topic: {Topic} (score: {Score})", _ctx.SelectedTopic.Topic, _ctx.SelectedTopic.FinalScore);
    }

    private async Task StageResearchAsync()
    {
        if (_ctx.SelectedTopic == null) return;
        var topicName = _ctx.SelectedTopic.Topic;
        ActivityRegistry.LogStart("research_planner", "research", $"Topic: {topicName}");

        // Find the cluster that corresponds to this topic to get member topics
        var matchedCluster = _ctx.Clusters.FirstOrDefault(c =>
            c.GetValueOrDefault("canonical_topic")?.ToString() == topicName);

        // Build a broader search: use member topic names and their keywords
        var searchTerms = new List<string> { topicName };
        if (matchedCluster?.GetValueOrDefault("member_topics") is List<object> members)
        {
            foreach (var m in members)
            {
                var s = m?.ToString() ?? "";
                if (!string.IsNullOrEmpty(s)) searchTerms.Add(s);
            }
        }
        // Also add the aliases if available
        if (matchedCluster?.GetValueOrDefault("aliases") is List<object> aliases)
        {
            foreach (var a in aliases)
            {
                var s = a?.ToString() ?? "";
                if (!string.IsNullOrEmpty(s)) searchTerms.Add(s);
            }
        }

        // Match articles against all search terms (broad match)
        var topicArticles = _ctx.Articles.Where(a =>
        {
            var text = $"{a.GetValueOrDefault("title")} {a.GetValueOrDefault("summary")}";
            foreach (var term in searchTerms)
            {
                // Check each word in the search term
                foreach (var word in term.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (word.Length > 2 && text.Contains(word, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                if (text.Contains(term, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }).ToList();

        // Fallback: if no articles matched, use all articles
        if (topicArticles.Count == 0)
        {
            _logger.LogInformation("No articles matched topic '{Topic}', using all {Count} articles as fallback",
                topicName, _ctx.Articles.Count);
            topicArticles = _ctx.Articles;
        }

        _logger.LogInformation("Researching {Topic} with {Count} matched articles", topicName, topicArticles.Count);

        // Generate a research question using the question discovery engine (soul.md §21, §38)
        var questionDiscovery = new Agents.ResearchQuestionDiscovery(_settings);
        var researchQuestion = await questionDiscovery.GenerateQuestionAsync(topicName, topicArticles);
        _logger.LogInformation("Generated research question: {Q}", researchQuestion);

        var planner = new ResearchPlannerAgent(_llm);
        _ctx.ResearchPlan = await planner.CreatePlanAsync(topicName,
            _ctx.ResearchabilityScores.GetValueOrDefault(topicName),
            _ctx.TrendScores.GetValueOrDefault(topicName),
            _ctx.MarketScores.GetValueOrDefault(topicName), topicArticles);
        _ctx.ResearchPlan.ResearchQuestion = researchQuestion; // Override with the LLM-generated question
        ActivityRegistry.LogComplete("research_planner", "research", $"Question: {_ctx.ResearchPlan.ResearchQuestion}");

        ActivityRegistry.LogStart("deep_researcher", "research", $"Researching: {topicName}");
        var researcher = new ResearchAgent(_llm);
        _ctx.ResearchResult = await researcher.ResearchAsync(_ctx.ResearchPlan.ResearchQuestion, topicName, topicArticles);

        _logger.LogInformation("Research complete: {Count} sources, {Findings} findings",
            _ctx.ResearchResult.SourcesAnalyzed.Count, _ctx.ResearchResult.Findings.Count);
        ActivityRegistry.LogComplete("deep_researcher", "research",
            $"{_ctx.ResearchResult.SourcesAnalyzed.Count} sources, {_ctx.ResearchResult.Findings.Count} findings");

        // Record findings, gaps, and questions to the Knowledge Graph (soul.md §7, §23)
        foreach (var f in _ctx.ResearchResult.Findings)
        {
            KnowledgeGraph.RecordFinding(
                projectTopic: topicName,
                claim: f.Claim,
                findingType: f.FindingType,
                confidence: f.Confidence,
                sourceUrl: f.SourceUrl,
                supportingEvidence: f.SupportingExcerpt
            );
        }
        foreach (var gap in _ctx.ResearchResult.KnowledgeGaps)
            KnowledgeGraph.RecordGap(topicName, gap);

        // Falsifiability check — actively search for disconfirming evidence (soul.md §14)
        try
        {
            var falsifiability = new FalsifiabilityChecker(_settings);
            var lastConclusion = _ctx.ResearchResult.Conclusions.LastOrDefault() ?? "";
            if (!string.IsNullOrEmpty(lastConclusion))
            {
                var report = await falsifiability.CheckAsync(topicName, lastConclusion, _ctx.ResearchResult.Findings);
                foreach (var de in report.DisconfirmingEvidence)
                    KnowledgeGraph.RecordContradiction(topicName, lastConclusion, de.Claim, "conclusion", de.Source, de.Strength);
                _logger.LogInformation("Falsifiability check: {Count} potential counterarguments", report.DisconfirmingEvidence.Count);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Falsifiability check failed"); }
    }

    private async Task StageWriteAsync()
    {
        if (_ctx.ResearchResult == null) return;
        ActivityRegistry.LogStart("article_writer", "write", $"Topic: {_ctx.SelectedTopic?.Topic}");
        var writer = new ArticleWriterAgent(_llm);
        _ctx.GeneratedArticle = await writer.WriteAsync(_ctx.SelectedTopic?.Topic ?? "Research Topic", _ctx.ResearchResult);
        _logger.LogInformation("Article written: {Title}", _ctx.GeneratedArticle.Title);
        ActivityRegistry.LogComplete("article_writer", "write", $"Title: {_ctx.GeneratedArticle.Title}");
    }

    private async Task StageReviewAsync()
    {
        if (_ctx.GeneratedArticle == null) return;
        ActivityRegistry.LogStart("editorial_review", "review", $"Article: {_ctx.GeneratedArticle.Title}");
        var reviewer = new EditorialReviewAgent();
        _ctx.EditorialReview = await reviewer.ReviewAsync(_ctx.GeneratedArticle, _settings.MinPublicationScore);
        _logger.LogInformation("Review: {Score} - {Recommendation}", _ctx.EditorialReview.OverallScore, _ctx.EditorialReview.PublishRecommendation);
        ActivityRegistry.LogComplete("editorial_review", "review",
            $"Score: {_ctx.EditorialReview.OverallScore}, Recommendation: {_ctx.EditorialReview.PublishRecommendation}");
    }

    private async Task StagePublishAsync()
    {
        if (_ctx.GeneratedArticle == null || _ctx.EditorialReview == null) return;

        if (_ctx.EditorialReview.PublishRecommendation != "APPROVE" || _ctx.EditorialReview.OverallScore < _settings.MinPublicationScore)
        {
            _logger.LogInformation("Article not approved for publication");
            ActivityRegistry.LogComplete("publisher", "publish", "Not approved");
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
            ActivityRegistry.LogStart("publisher", "publish", $"Branch: research/{topicName.ToLower().Replace(" ", "-")}");
            var publisher = new GitHubPublisher(_settings.GitHubToken, _settings.GitHubOwner,
                _settings.GitHubRepository, _settings.GitHubDefaultBranch, _settings.AutoPublish);
            var result = await publisher.PublishAsync(markdown, filename, topicName, scores, _settings.DryRun);
            _ctx.PublishSuccess = result.Success;
            _ctx.PublishBranch = result.Branch;
            _ctx.PublishPrNumber = result.PrNumber;
            _ctx.PublishPrUrl = result.PrUrl;
            _logger.LogInformation("Publish result: {Success}, PR: {PrUrl}", result.Success, result.PrUrl);
            ActivityRegistry.LogComplete("publisher", "publish",
                $"Branch: {result.Branch}, PR: {result.PrUrl ?? "none"}, Success: {result.Success}");

            // Send email notification if PR was created
            if (result.Success && !string.IsNullOrEmpty(result.PrUrl))
            {
                var notifier = new EmailNotifier();
                if (notifier.IsConfigured)
                {
                    await notifier.SendPrNotificationAsync(topicName, _ctx.PublishPrUrl ?? result?.PrUrl ?? "", _ctx.EditorialReview.OverallScore, _ctx.GeneratedArticle?.Title ?? "");
                }

                // Post to Moltbook if registered
                try
                {
                    var moltbook = new Common.MoltbookClient();
                    if (moltbook.IsRegistered)
                    {
                        var summary = _ctx.ResearchPlan?.ResearchQuestion ?? "";
                        var findingsText = _ctx.ResearchResult?.Findings != null
                            ? string.Join("\n", _ctx.ResearchResult.Findings.Take(5).Select(f => f.Claim))
                            : "";
                        var conclusionText = _ctx.ResearchResult?.Conclusions != null
                            ? string.Join("\n", _ctx.ResearchResult.Conclusions.Take(3))
                            : "";
                        var mr = await moltbook.PostResearchAsync(
                            topicName,
                            _ctx.GeneratedArticle?.Title ?? "",
                            _ctx.PublishPrUrl ?? result?.PrUrl ?? "",
                            summary,
                            findings: findingsText,
                            conclusion: conclusionText,
                            editorialScore: _ctx.EditorialReview?.OverallScore ?? 0,
                            articleCount: _ctx.Articles.Count,
                            findingCount: _ctx.ResearchResult?.Findings.Count ?? 0
                        );
                        if (mr.Success)
                            _logger.LogInformation("Posted to Moltbook: {PostId}", mr.PostId);
                        else
                            _logger.LogWarning("Failed to post to Moltbook: {Error}", mr.Error);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Moltbook post failed");
                }
            }
        }
        else
        {
            _logger.LogInformation("Dry run or no GitHub token — skipping publish");
            ActivityRegistry.LogComplete("publisher", "publish", "Skipped (dry run or no token)");
        }

        // Record completed research to avoid repeating the same topic
        CompletedResearchRegistry.Record(
            topic: topicName,
            researchQuestion: _ctx.ResearchPlan?.ResearchQuestion ?? "",
            finalScore: _ctx.SelectedTopic?.FinalScore ?? 0,
            editorialScore: _ctx.EditorialReview.OverallScore,
            prUrl: _ctx.PublishPrUrl,
            articleTitle: _ctx.GeneratedArticle?.Title,
            articleCount: _ctx.Articles.Count,
            findingCount: _ctx.ResearchResult?.Findings.Count ?? 0
        );
    }
}