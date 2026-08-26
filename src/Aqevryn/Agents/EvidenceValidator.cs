using Aqevryn.Common;
using System.Text.RegularExpressions;

namespace Aqevryn.Agents;

public class EvidenceValidator
{
    public Task<Dictionary<string, object>> ValidateFindingAsync(Dictionary<string, object?> finding)
    {
        var issues = new List<string>();
        var claim = finding.GetValueOrDefault("claim")?.ToString() ?? "";
        var sourceUrl = finding.GetValueOrDefault("source_url")?.ToString() ?? "";

        if (string.IsNullOrEmpty(sourceUrl)) issues.Add("Missing source URL");
        if (sourceUrl.Length > 0 && !sourceUrl.StartsWith("http")) issues.Add("Source URL is not a valid HTTP URL");
        if (string.IsNullOrEmpty(claim)) issues.Add("Empty claim");
        if (claim.Length < 10) issues.Add("Claim is too short");

        var isValid = issues.Count == 0;
        return Task.FromResult(new Dictionary<string, object>
        {
            ["is_valid"] = isValid, ["issues"] = issues, ["claim"] = claim, ["source_url"] = sourceUrl
        });
    }

    public List<string> CheckFabrication(List<Dictionary<string, object?>> findings)
    {
        var warnings = new List<string>();
        for (int i = 0; i < findings.Count; i++)
        {
            var claim = findings[i].GetValueOrDefault("claim")?.ToString() ?? "";
            var sourceUrl = findings[i].GetValueOrDefault("source_url")?.ToString() ?? "";

            if (Regex.IsMatch(claim, @"\d+%|\d+\.\d+%|\d+ million|\d+ billion") && string.IsNullOrEmpty(sourceUrl))
                warnings.Add($"Finding #{i + 1} contains statistics but no source URL: '{claim[..Math.Min(80, claim.Length)]}'");
            if (Regex.IsMatch(claim, @"\$\d+") && string.IsNullOrEmpty(sourceUrl))
                warnings.Add($"Finding #{i + 1} mentions monetary figures without source: '{claim[..Math.Min(80, claim.Length)]}'");
        }
        return warnings;
    }
}