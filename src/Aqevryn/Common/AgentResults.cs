using System.Text.Json.Serialization;

namespace Aqevryn.Common;

public class TopicDiscoveryResult
{
    public string Topic { get; set; } = "";
    public string Summary { get; set; } = "";
    public string WhyTrending { get; set; } = "";
    public List<string> Evidence { get; set; } = new();
    public List<string> RelatedTopics { get; set; } = new();
    public List<string> SourceIds { get; set; } = new();
    public string? Category { get; set; }
}

public class TopicClusterResult
{
    public string CanonicalTopic { get; set; } = "";
    public List<string> Aliases { get; set; } = new();
    public List<string> RelatedTopics { get; set; } = new();
    public List<string> TopicIds { get; set; } = new();
    public int ArticleCount { get; set; }
    public List<string> MemberTopics { get; set; } = new();
}

public class TrendScoreResult
{
    public string Topic { get; set; } = "";
    public double Score { get; set; }
    public int MentionCount { get; set; }
    public double MentionGrowth { get; set; }
    public int IndependentSources { get; set; }
    public double RecencyScore { get; set; }
    public Dictionary<string, double> Signals { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
}

public class ResearchabilityResult
{
    public string Topic { get; set; } = "";
    public double Score { get; set; }
    public bool HasPrimarySources { get; set; }
    public bool HasAcademicPapers { get; set; }
    public bool HasTechnicalDocs { get; set; }
    public bool HasImplementations { get; set; }
    public bool HasCompetingViewpoints { get; set; }
    public List<string> KnowledgeGaps { get; set; } = new();
    public string? ResearchQuestion { get; set; }
    public List<string> Evidence { get; set; } = new();
    public List<string> Weaknesses { get; set; } = new();
}

public class MarketViabilityResult
{
    public string Topic { get; set; } = "";
    public double Score { get; set; }
    public double IndustryAdoption { get; set; }
    public double EnterpriseActivity { get; set; }
    public double DeveloperActivity { get; set; }
    public int CommercialProducts { get; set; }
    public double OpenSourceActivity { get; set; }
    public List<string> CompaniesInvolved { get; set; } = new();
    public List<string> Evidence { get; set; } = new();
    public List<string> Limitations { get; set; } = new();
}

public class RankedTopic
{
    public string Topic { get; set; } = "";
    public double TrendScore { get; set; }
    public double ResearchabilityScore { get; set; }
    public double MarketViabilityScore { get; set; }
    public double NoveltyScore { get; set; }
    public double TechnicalSignificance { get; set; }
    public double FinalScore { get; set; }
    public string Decision { get; set; } = "PENDING";
    public string? RejectionReason { get; set; }
    public int ArticleCount { get; set; }
    public string? Category { get; set; }
}

public class ResearchPlan
{
    public string ResearchQuestion { get; set; } = "";
    public List<string> Objectives { get; set; } = new();
    public List<string> Subquestions { get; set; } = new();
    public List<string> RequiredEvidence { get; set; } = new();
    public List<string> PrimarySourcesRequired { get; set; } = new();
    public List<string> AcademicSourcesRequired { get; set; } = new();
    public List<string> TechnicalDocumentation { get; set; } = new();
    public List<string> SearchQueries { get; set; } = new();
    public List<string> PotentialCounterarguments { get; set; } = new();
    public List<string> ExpectedLimitations { get; set; } = new();
}

public class ResearchFinding
{
    public string Claim { get; set; } = "";
    public string FindingType { get; set; } = "research_finding";
    public double Confidence { get; set; } = 0.5;
    public string? SupportingExcerpt { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceTitle { get; set; }
    public List<string> Contradictions { get; set; } = new();
}

public class ResearchSource
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string SourceType { get; set; } = "";
    public double Reliability { get; set; } = 0.5;
    public double? RelevanceScore { get; set; }
    public List<string> KeyFindings { get; set; } = new();
}

public class ResearchResult
{
    public string ResearchQuestion { get; set; } = "";
    public List<ResearchSource> SourcesAnalyzed { get; set; } = new();
    public List<ResearchFinding> Findings { get; set; } = new();
    public List<string> KnowledgeGaps { get; set; } = new();
    public List<string> Conclusions { get; set; } = new();
    public List<string> MethodologyNotes { get; set; } = new();
}

public class GeneratedArticle
{
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Description { get; set; } = "";
    public string Introduction { get; set; } = "";
    public string WhyThisMatters { get; set; } = "";
    public string Background { get; set; } = "";
    public string ResearchQuestion { get; set; } = "";
    public string TechnicalAnalysis { get; set; } = "";
    public string Findings { get; set; } = "";
    public string MarketImplications { get; set; } = "";
    public string Limitations { get; set; } = "";
    public string FutureOutlook { get; set; } = "";
    public string Conclusion { get; set; } = "";
    public List<string> References { get; set; } = new();
    public string Methodology { get; set; } = "";
}

public class EditorialReviewResult
{
    public double OverallScore { get; set; }
    public double FactualAccuracy { get; set; }
    public double ResearchQuality { get; set; }
    public double Originality { get; set; }
    public double TechnicalQuality { get; set; }
    public double WritingQuality { get; set; }
    public double SeoQuality { get; set; }
    public List<string> CriticalIssues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string PublishRecommendation { get; set; } = "REJECT";
}

public class PipelineContext
{
    public string RunId { get; set; } = Guid.NewGuid().ToString();
    public bool DryRun { get; set; }
    public string? Topic { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public string Stage { get; set; } = "INITIALIZED";
    public string? Error { get; set; }

    public List<SourceItem> SourceItems { get; set; } = new();
    public List<Dictionary<string, object?>> Articles { get; set; } = new();
    public List<Dictionary<string, object?>> Topics { get; set; } = new();
    public List<Dictionary<string, object?>> Clusters { get; set; } = new();
    public Dictionary<string, double> TrendScores { get; set; } = new();
    public Dictionary<string, double> ResearchabilityScores { get; set; } = new();
    public Dictionary<string, double> MarketScores { get; set; } = new();
    public Dictionary<string, int> ArticleCounts { get; set; } = new();
    public List<RankedTopic> RankedTopics { get; set; } = new();
    public RankedTopic? SelectedTopic { get; set; }
    public ResearchPlan? ResearchPlan { get; set; }
    public ResearchResult? ResearchResult { get; set; }
    public GeneratedArticle? GeneratedArticle { get; set; }
    public EditorialReviewResult? EditorialReview { get; set; }
    public string? MarkdownArticle { get; set; }
    public string? MarkdownFilename { get; set; }
    public bool? PublishSuccess { get; set; }
    public string? PublishBranch { get; set; }
    public int? PublishPrNumber { get; set; }
    public string? PublishPrUrl { get; set; }
}