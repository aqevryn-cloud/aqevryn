namespace Aqevryn.Common;

/// <summary>
/// Domain-aware evidence profiles per soul.md §9, §18.
/// Different domains require different evidence hierarchies.
/// </summary>
public static class DomainProfiles
{
    public static DomainProfile GetProfile(string domain) =>
        Profiles.TryGetValue(domain.ToLowerInvariant(), out var p) ? p : Profiles["default"];

    public static readonly Dictionary<string, DomainProfile> Profiles = new()
    {
        ["default"] = new DomainProfile
        {
            DisplayName = "General",
            EvidenceHierarchy = new[] { "research_paper", "documentation", "engineering_blog", "standards", "government", "reputable_publication", "industry_report", "community" },
            PreferredSourceTypes = new[] { "arxiv", "rss" },
            Description = "Default evidence hierarchy for general research",
        },
        ["technology"] = new DomainProfile
        {
            DisplayName = "Technology",
            EvidenceHierarchy = new[] { "research_paper", "documentation", "engineering_blog", "standards", "reputable_publication", "industry_report", "community" },
            PreferredSourceTypes = new[] { "arxiv", "github", "rss", "hackernews" },
            Description = "Prioritizes primary research, official docs, and engineering publications",
        },
        ["science"] = new DomainProfile
        {
            DisplayName = "Science",
            EvidenceHierarchy = new[] { "research_paper", "research_paper", "research_paper", "government", "reputable_publication", "industry_report", "community" },
            PreferredSourceTypes = new[] { "arxiv", "rss" },
            Description = "Prioritizes peer-reviewed research and government sources",
        },
        ["medicine"] = new DomainProfile
        {
            DisplayName = "Medicine",
            EvidenceHierarchy = new[] { "research_paper", "research_paper", "government", "standards", "reputable_publication", "industry_report", "community" },
            PreferredSourceTypes = new[] { "arxiv", "rss" },
            Description = "Prioritizes clinical trials, systematic reviews, and health authority guidance",
        },
        ["economics"] = new DomainProfile
        {
            DisplayName = "Economics",
            EvidenceHierarchy = new[] { "government", "research_paper", "industry_report", "reputable_publication", "documentation", "standards", "community" },
            PreferredSourceTypes = new[] { "rss" },
            Description = "Prioritizes official statistics, institutional research, and academic papers",
        },
        ["finance"] = new DomainProfile
        {
            DisplayName = "Finance",
            EvidenceHierarchy = new[] { "government", "industry_report", "reputable_publication", "research_paper", "documentation", "standards", "community" },
            PreferredSourceTypes = new[] { "rss" },
            Description = "Prioritizes regulatory filings, institutional research, and market data",
        },
        ["history"] = new DomainProfile
        {
            DisplayName = "History",
            EvidenceHierarchy = new[] { "research_paper", "reputable_publication", "documentation", "government", "standards", "industry_report", "community" },
            PreferredSourceTypes = new[] { "rss" },
            Description = "Prioritizes primary sources, archival records, and scholarly research",
        },
        ["geopolitics"] = new DomainProfile
        {
            DisplayName = "Geopolitics",
            EvidenceHierarchy = new[] { "government", "reputable_publication", "research_paper", "industry_report", "standards", "documentation", "community" },
            PreferredSourceTypes = new[] { "rss" },
            Description = "Prioritizes official government sources, think tanks, and policy analysis",
        },
        ["climate"] = new DomainProfile
        {
            DisplayName = "Climate",
            EvidenceHierarchy = new[] { "research_paper", "government", "standards", "reputable_publication", "industry_report", "documentation", "community" },
            PreferredSourceTypes = new[] { "rss" },
            Description = "Prioritizes climate science research, IPCC, and environmental agencies",
        },
        ["law"] = new DomainProfile
        {
            DisplayName = "Law",
            EvidenceHierarchy = new[] { "government", "standards", "research_paper", "reputable_publication", "documentation", "industry_report", "community" },
            PreferredSourceTypes = new[] { "rss" },
            Description = "Prioritizes legal rulings, legislation, and scholarly legal analysis",
        },
        ["business"] = new DomainProfile
        {
            DisplayName = "Business",
            EvidenceHierarchy = new[] { "industry_report", "reputable_publication", "research_paper", "documentation", "government", "standards", "community" },
            PreferredSourceTypes = new[] { "rss" },
            Description = "Prioritizes market analysis, case studies, and business research",
        },
    };

    /// <summary>Get the reliability weight for a source type in a given domain.</summary>
    public static double GetSourceReliability(string domain, string sourceType)
    {
        var profile = GetProfile(domain);
        var hierarchy = profile.EvidenceHierarchy;
        for (int i = 0; i < hierarchy.Length; i++)
            if (hierarchy[i] == sourceType)
                return 1.0 - (i * 0.1); // Position-based scoring
        return 0.3; // Very low for unlisted types
    }
}

public class DomainProfile
{
    public string DisplayName { get; set; } = "";
    public string[] EvidenceHierarchy { get; set; } = Array.Empty<string>();
    public string[] PreferredSourceTypes { get; set; } = Array.Empty<string>();
    public string Description { get; set; } = "";
}