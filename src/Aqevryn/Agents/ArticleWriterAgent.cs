using System.Text.Json;
using Aqevryn.Common;
using Microsoft.Extensions.Logging;

namespace Aqevryn.Agents;

/// <summary>
/// Professional article writer that uses LLM to produce comprehensive,
/// well-structured research papers with depth, context, and evidence.
/// Falls back to template-based generation when LLM is unavailable.
/// </summary>
public class ArticleWriterAgent
{
    private readonly ILogger<ArticleWriterAgent> _logger;
    private readonly LLMClient? _llm;

    public ArticleWriterAgent(LLMClient? llm = null, ILogger<ArticleWriterAgent>? logger = null)
    {
        _llm = llm; _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ArticleWriterAgent>.Instance;
    }

    public async Task<GeneratedArticle> WriteAsync(string topic, ResearchResult researchResult)
    {
        _logger.LogInformation("Writing article for {Topic} using LLM={LlmAvailable}",
            topic, _llm?.IsAvailable ?? false);

        var slug = MakeSlug(topic);
        var sources = researchResult.SourcesAnalyzed;
        var findings = researchResult.Findings;
        var conclusions = researchResult.Conclusions;
        var references = sources.Select((s, i) => $"[{i + 1}] {s.Title} — {s.Url} ({s.SourceType})").ToList();

        // Use LLM for comprehensive article generation if available
        if (_llm?.IsAvailable == true)
        {
            try
            {
                return await GenerateWithLLMAsync(topic, slug, researchResult, references);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LLM article generation failed for {Topic}, falling back to template", topic);
            }
        }

        // Fallback: enhanced template-based generation
        return GenerateTemplate(topic, slug, researchResult, references);
    }

    private async Task<GeneratedArticle> GenerateWithLLMAsync(string topic, string slug,
        ResearchResult research, List<string> references)
    {
        _logger.LogInformation("Generating comprehensive article with LLM for {Topic}", topic);

        // Build the research context
        var findingsText = string.Join("\n",
            research.Findings.Select(f => $"  - {f.Claim} ({f.FindingType}, confidence: {f.Confidence:P0})"));
        var conclusionsText = string.Join("\n", research.Conclusions.Select(c => $"  - {c}"));
        var sourcesText = string.Join("\n",
            research.SourcesAnalyzed.Take(20).Select(s => $"  - {s.Title} ({s.SourceType}, reliability: {s.Reliability:P0})"));
        var gapsText = research.KnowledgeGaps.Count > 0
            ? string.Join("\n", research.KnowledgeGaps.Select(g => $"  - {g}"))
            : "  - None identified";

        var systemPrompt = @"You are a senior technology research writer for Aqevryn Research.
Write a comprehensive, professional research paper that is:
- Technically accurate and evidence-based
- Well-structured with clear sections
- Insightful and thought-provoking
- Accessible to a technical audience
- 2000-4000 words in length
- Clearly distinguishes facts from interpretations

Writing style: Professional, analytical, evidence-based. Use clear headings, 
specific examples, and data-driven analysis. Avoid generic filler phrases like 
'in today's rapidly evolving landscape' or 'game-changer'.

Structure the paper with these sections, each with substantive content:
1. Introduction (context, importance, what this paper covers)
2. Why This Matters (significance and implications)
3. Background & Context (historical context, evolution, key concepts)
4. Research Question (clear statement of what we investigated)
5. Technical Analysis (deep dive into architecture, implementations, technologies)
6. Key Findings (evidence-based findings with supporting data)
7. Market & Industry Implications (adoption, ecosystem, competitive landscape)
8. Limitations & Challenges (known issues, gaps, concerns)
9. Future Outlook (trajectory, opportunities, predictions)
10. Conclusion (synthesize key takeaways)
11. References (numbered list)

Return a JSON object with keys: title, description, introduction, why_this_matters, 
background, research_question, technical_analysis, findings, market_implications, 
limitations, future_outlook, conclusion, methodology";

        var userPrompt = $@"Write a comprehensive research paper on: {topic}

RESEARCH QUESTION: {research.ResearchQuestion}

FINDINGS ({research.Findings.Count} total):
{findingsText}

CONCLUSIONS:
{conclusionsText}

SOURCES ANALYZED ({research.SourcesAnalyzed.Count} total):
{sourcesText}

KNOWLEDGE GAPS:
{gapsText}

METHODOLOGY NOTES:
{string.Join("\n", research.MethodologyNotes)}

Write a thorough, professional research paper. Each section should have substantial content (2-4 paragraphs).
The technical analysis should be 500+ words. The findings section should detail each key finding.
Include specific examples, data points, and references where appropriate.";

        var response = await _llm!.CompleteAsync(systemPrompt, userPrompt,
            expectJson: true, maxTokens: 16384, temperature: 0.3);

        var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response);

        if (data == null)
            throw new Exception("Failed to parse LLM response");

        var title = GetString(data, "title") ?? $"Research: {topic} — A Comprehensive Analysis";
        var description = GetString(data, "description") ?? $"An in-depth, evidence-based analysis of {topic}, examining technical architecture, market implications, and future directions.";

        _logger.LogInformation("LLM generated article: {Title} ({Len} chars)", title, response.Length);

        return new GeneratedArticle
        {
            Title = title,
            Slug = slug,
            Description = description,
            Introduction = GetString(data, "introduction") ?? "",
            WhyThisMatters = GetString(data, "why_this_matters") ?? "",
            Background = GetString(data, "background") ?? "",
            ResearchQuestion = research.ResearchQuestion,
            TechnicalAnalysis = GetString(data, "technical_analysis") ?? "",
            Findings = GetString(data, "findings") ?? "",
            MarketImplications = GetString(data, "market_implications") ?? "",
            Limitations = GetString(data, "limitations") ?? "",
            FutureOutlook = GetString(data, "future_outlook") ?? "",
            Conclusion = GetString(data, "conclusion") ?? "",
            Methodology = GetString(data, "methodology") ?? $"This research was conducted by analyzing {research.SourcesAnalyzed.Count} sources across research papers, engineering blogs, and industry publications. The analysis was performed using Aqevryn's research pipeline with LLM-enhanced analysis.",
            References = references,
        };
    }

    private GeneratedArticle GenerateTemplate(string topic, string slug,
        ResearchResult research, List<string> references)
    {
        var findings = research.Findings;
        var sources = research.SourcesAnalyzed;
        var conclusions = research.Conclusions;

        var findingLines = string.Join("\n", findings.Take(10).Select(f =>
            $"- **{f.Claim}** (*{f.FindingType}*, confidence: {f.Confidence:P0})" +
            (f.SupportingExcerpt != null ? $"\n  > {f.SupportingExcerpt}" : "")));

        var sourcesByType = sources.GroupBy(s => s.SourceType)
            .Select(g => $"{g.Count()} {g.Key} sources")
            .ToList();
        var sourceSummary = string.Join(", ", sourcesByType);

        return new GeneratedArticle
        {
            Title = $"Research: {topic} — An Evidence-Based Analysis",
            Slug = slug,
            Description = $"An in-depth investigation into {topic}, analyzing {sources.Count} sources across {sourceSummary}.",
            Introduction = $"{topic} has emerged as a significant technology trend with implications across multiple industries. This article presents a comprehensive, evidence-based analysis drawing on {sources.Count} sources from {sourceSummary}. We examine the current state of the technology, key findings from our research, and implications for the future.",
            WhyThisMatters = $"{topic} represents a convergence of several important technology trends. Understanding its development, capabilities, and limitations is crucial for technologists, decision-makers, and researchers alike.",
            Background = $"{topic} has developed rapidly in recent years. This research traces its evolution, examines the key technological foundations, and provides context for understanding its current state and future trajectory.",
            ResearchQuestion = research.ResearchQuestion,
            TechnicalAnalysis = $"Our analysis of {sources.Count} sources reveals several key themes in the technical development of {topic}. The technology builds on established principles while introducing novel approaches to address key challenges. Multiple implementations exist, each with different trade-offs in terms of performance, scalability, and reliability.\n\nKey technical considerations include: architecture decisions, performance characteristics, integration patterns, and operational requirements. The evidence suggests that while the technology shows promise, several technical challenges remain to be addressed.",
            Findings = findingLines,
            MarketImplications = $"The market for {topic} is showing significant growth. Industry adoption is being driven by concrete benefits in productivity, efficiency, and capability. Major technology companies are investing heavily, and the open-source ecosystem is expanding rapidly.",
            Limitations = "This analysis is based on publicly available sources at the time of research. The technology landscape evolves rapidly, and some findings may date. Specific limitations of the current research include: reliance on publicly available information, potential publication bias in favor of positive results, and the rapidly evolving nature of the field.",
            FutureOutlook = $"The trajectory of {topic} suggests continued development and adoption. Key areas to watch include: improvements in reliability and robustness, expansion of the ecosystem, enterprise adoption patterns, and the emergence of standardized benchmarks and evaluation methodologies.",
            Conclusion = conclusions.Count > 0
                ? string.Join("\n\n", conclusions.Select(c => c))
                : $"{topic} represents a significant technology development with far-reaching implications. Evidence from {sources.Count} sources across multiple categories suggests meaningful progress and continued growth. However, several challenges and open questions remain, warranting continued research and careful evaluation.",
            References = references,
            Methodology = $"This research was conducted by analyzing {sources.Count} sources across {sourceSummary}. Sources were classified by type with reliability scores. Findings were extracted and categorized by type (verified fact, research finding, interpretation, inference, prediction, opinion) with confidence scores. The analysis was performed using Aqevryn's research pipeline with deterministic analysis.",
        };
    }

    private static string MakeSlug(string topic)
    {
        var slug = topic.ToLower().Replace(" ", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9-]", "").Trim('-');
        return slug.Length > 80 ? slug[..80] : slug;
    }

    private static string? GetString(Dictionary<string, JsonElement> data, string key)
    {
        return data.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
    }
}