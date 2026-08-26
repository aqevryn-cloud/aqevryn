using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Aqevryn.Database;

public class SourceEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string SourceType { get; set; } = "";
    public string Url { get; set; } = "";
    public string? Category { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ArticleEntity> Articles { get; set; } = new();
}

public class ArticleEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceId { get; set; } = "";
    [ForeignKey(nameof(SourceId))] public SourceEntity? Source { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string? CanonicalUrl { get; set; }
    public string? Author { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? Summary { get; set; }
    public string? Content { get; set; }
    public string? Tags { get; set; } // JSON array
    public string? Category { get; set; }
    public string ContentHash { get; set; } = "";
    public DateTime RetrievedAt { get; set; } = DateTime.UtcNow;
    public bool Processed { get; set; } = false;
}

public class TopicEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Topic { get; set; } = "";
    public string? Summary { get; set; }
    public string? WhyTrending { get; set; }
    public string? Evidence { get; set; } // JSON
    public string? RelatedTopics { get; set; } // JSON
    public string? SourceIds { get; set; } // JSON
    public string? Category { get; set; }
    public string? ClusterId { get; set; }
    [ForeignKey(nameof(ClusterId))] public TopicClusterEntity? Cluster { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<TrendScoreEntity> TrendScores { get; set; } = new();
    public List<ResearchProjectEntity> ResearchProjects { get; set; } = new();
}

public class TopicClusterEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CanonicalTopic { get; set; } = "";
    public string? Aliases { get; set; } // JSON
    public string? RelatedTopics { get; set; } // JSON
    public string? Embedding { get; set; } // JSON
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<TopicEntity> Topics { get; set; } = new();
}

public class TrendScoreEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TopicId { get; set; } = "";
    [ForeignKey(nameof(TopicId))] public TopicEntity? Topic { get; set; }
    public double Score { get; set; }
    public int MentionCount { get; set; }
    public double? MentionGrowth { get; set; }
    public int IndependentSources { get; set; }
    public double? RecencyScore { get; set; }
    public double? GitHubActivity { get; set; }
    public double? PaperActivity { get; set; }
    public string? Signals { get; set; } // JSON
    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;
}

public class ResearchProjectEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string TopicId { get; set; } = "";
    [ForeignKey(nameof(TopicId))] public TopicEntity? Topic { get; set; }
    public string ResearchQuestion { get; set; } = "";
    public string? ResearchPlan { get; set; } // JSON
    public string? Objectives { get; set; } // JSON
    public string? Subquestions { get; set; } // JSON
    public string Status { get; set; } = "PLANNING";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public List<ResearchSourceEntity> Sources { get; set; } = new();
    public List<ResearchFindingEntity> Findings { get; set; } = new();
    public List<GeneratedArticleEntity> GeneratedArticles { get; set; } = new();
}

public class ResearchSourceEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = "";
    [ForeignKey(nameof(ProjectId))] public ResearchProjectEntity? Project { get; set; }
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string SourceType { get; set; } = "";
    public double Reliability { get; set; } = 0.5;
    public double? RelevanceScore { get; set; }
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    public string? Metadata { get; set; } // JSON
}

public class ResearchFindingEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = "";
    [ForeignKey(nameof(ProjectId))] public ResearchProjectEntity? Project { get; set; }
    public string Claim { get; set; } = "";
    public string FindingType { get; set; } = "research_finding";
    public double Confidence { get; set; } = 0.5;
    public string? SupportingExcerpt { get; set; }
    public string? SourceId { get; set; }
    public string? Contradictions { get; set; } // JSON
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<CitationEntity> Citations { get; set; } = new();
}

public class CitationEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FindingId { get; set; } = "";
    [ForeignKey(nameof(FindingId))] public ResearchFindingEntity? Finding { get; set; }
    public string SourceId { get; set; } = "";
    public string Claim { get; set; } = "";
    public string? SupportingExcerpt { get; set; }
    public string? Url { get; set; }
    public double Confidence { get; set; } = 0.5;
    public bool Validated { get; set; } = false;
    public DateTime? ValidatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class GeneratedArticleEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProjectId { get; set; } = "";
    [ForeignKey(nameof(ProjectId))] public ResearchProjectEntity? Project { get; set; }
    public string Title { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Content { get; set; } = "";
    public string? Markdown { get; set; }
    public string? FrontMatter { get; set; } // JSON
    public string Status { get; set; } = "DRAFT";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<EditorialReviewEntity> EditorialReviews { get; set; } = new();
    public List<PublicationEntity> Publications { get; set; } = new();
}

public class EditorialReviewEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ArticleId { get; set; } = "";
    [ForeignKey(nameof(ArticleId))] public GeneratedArticleEntity? Article { get; set; }
    public double OverallScore { get; set; }
    public double FactualAccuracy { get; set; }
    public double ResearchQuality { get; set; }
    public double Originality { get; set; }
    public double TechnicalQuality { get; set; }
    public double WritingQuality { get; set; }
    public double SeoQuality { get; set; }
    public string? CriticalIssues { get; set; } // JSON
    public string? Warnings { get; set; } // JSON
    public string PublishRecommendation { get; set; } = "PENDING";
    public string Reviewer { get; set; } = "editorial_agent";
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;
}

public class PublicationEntity
{
    [Key] public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ArticleId { get; set; } = "";
    [ForeignKey(nameof(ArticleId))] public GeneratedArticleEntity? Article { get; set; }
    public string Branch { get; set; } = "";
    public string? CommitSha { get; set; }
    public int? PrNumber { get; set; }
    public string? PrUrl { get; set; }
    public string PrStatus { get; set; } = "OPEN";
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}