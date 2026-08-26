using Microsoft.EntityFrameworkCore;

namespace Aqevryn.Database;

public class AqevrynDbContext : DbContext
{
    public AqevrynDbContext(DbContextOptions<AqevrynDbContext> options) : base(options) { }

    public DbSet<SourceEntity> Sources => Set<SourceEntity>();
    public DbSet<ArticleEntity> Articles => Set<ArticleEntity>();
    public DbSet<TopicEntity> Topics => Set<TopicEntity>();
    public DbSet<TopicClusterEntity> TopicClusters => Set<TopicClusterEntity>();
    public DbSet<TrendScoreEntity> TrendScores => Set<TrendScoreEntity>();
    public DbSet<ResearchProjectEntity> ResearchProjects => Set<ResearchProjectEntity>();
    public DbSet<ResearchSourceEntity> ResearchSources => Set<ResearchSourceEntity>();
    public DbSet<ResearchFindingEntity> ResearchFindings => Set<ResearchFindingEntity>();
    public DbSet<CitationEntity> Citations => Set<CitationEntity>();
    public DbSet<GeneratedArticleEntity> GeneratedArticles => Set<GeneratedArticleEntity>();
    public DbSet<EditorialReviewEntity> EditorialReviews => Set<EditorialReviewEntity>();
    public DbSet<PublicationEntity> Publications => Set<PublicationEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ArticleEntity>(entity =>
        {
            entity.HasIndex(a => a.CanonicalUrl).IsUnique();
            entity.HasIndex(a => a.ContentHash);
            entity.HasOne(a => a.Source)
                .WithMany(s => s.Articles)
                .HasForeignKey(a => a.SourceId);
        });

        modelBuilder.Entity<TopicEntity>(entity =>
        {
            entity.HasIndex(t => t.Topic).IsUnique();
            entity.HasOne(t => t.Cluster)
                .WithMany(c => c.Topics)
                .HasForeignKey(t => t.ClusterId);
        });

        modelBuilder.Entity<ResearchProjectEntity>(entity =>
        {
            entity.HasOne(r => r.Topic)
                .WithMany(t => t.ResearchProjects)
                .HasForeignKey(r => r.TopicId);
        });

        modelBuilder.Entity<ResearchSourceEntity>(entity =>
        {
            entity.HasOne(s => s.Project)
                .WithMany(p => p.Sources)
                .HasForeignKey(s => s.ProjectId);
        });

        modelBuilder.Entity<ResearchFindingEntity>(entity =>
        {
            entity.HasOne(f => f.Project)
                .WithMany(p => p.Findings)
                .HasForeignKey(f => f.ProjectId);
        });

        modelBuilder.Entity<CitationEntity>(entity =>
        {
            entity.HasOne(c => c.Finding)
                .WithMany(f => f.Citations)
                .HasForeignKey(c => c.FindingId);
        });

        modelBuilder.Entity<GeneratedArticleEntity>(entity =>
        {
            entity.HasIndex(a => a.Slug).IsUnique();
            entity.HasOne(a => a.Project)
                .WithMany(p => p.GeneratedArticles)
                .HasForeignKey(a => a.ProjectId);
        });

        modelBuilder.Entity<EditorialReviewEntity>(entity =>
        {
            entity.HasOne(r => r.Article)
                .WithMany(a => a.EditorialReviews)
                .HasForeignKey(r => r.ArticleId);
        });

        modelBuilder.Entity<PublicationEntity>(entity =>
        {
            entity.HasOne(p => p.Article)
                .WithMany(a => a.Publications)
                .HasForeignKey(p => p.ArticleId);
        });
    }
}