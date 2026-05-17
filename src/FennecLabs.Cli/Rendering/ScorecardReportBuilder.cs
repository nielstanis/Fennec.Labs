using System.Text;

namespace FennecLabs.Cli.Rendering;

internal record ScorecardReport(
    string Project,
    string? Framework,
    DateTime GeneratedAt,
    ScorecardDependencyTree? DependencyTree,
    IReadOnlyList<ScorecardReportPackage> Packages);

internal record ScorecardDependencyTree(
    IReadOnlyList<ScorecardPackageRef> TopLevel,
    IReadOnlyList<ScorecardPackageRef> Transitive);

internal record ScorecardPackageRef(string Id, string? RequestedVersion, string? ResolvedVersion);

internal record ScorecardReportPackage(
    string PackageId,
    string PackageVersion,
    decimal? Score,
    IReadOnlyList<ScorecardReportCheck> Checks,
    string? Error,
    string? RepoName = null,
    string? ScorecardDate = null,
    string? ScorecardVersion = null);

internal record ScorecardReportCheck(string Name, int Score, string? Reason);

internal static class ScorecardReportBuilder
{
    internal static string BuildHtml(ScorecardReport report)
    {
        var withScore = report.Packages.Where(p => p.Score != null && p.Error == null).ToList();
        var noScore = report.Packages
            .Where(p => p.Score == null && string.IsNullOrEmpty(p.Error)).ToList();
        var withErrors = report.Packages.Where(p => !string.IsNullOrEmpty(p.Error)).ToList();
        var avgScore = withScore.Count > 0 ? withScore.Average(p => p.Score!.Value) : 0;

        var sb = new StringBuilder();
        sb.Append(HtmlHead());
        sb.Append($"""
<body>
    <div class="container">
        <h1>Security Scorecard Report</h1>
        <div class="info-section">
            <p><span class="info-label">Generated:</span> {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}</p>
            <p><span class="info-label">Project:</span> {EscapeHtml(report.Project)}</p>
            {(report.Framework != null ? $"""<p><span class="info-label">Framework:</span> {EscapeHtml(report.Framework)}</p>""" : "")}
        </div>
        <div class="summary-stats">
            <div class="stat-card"><h3>{report.Packages.Count}</h3><p>Total Packages</p></div>
            <div class="stat-card"><h3>{withScore.Count}</h3><p>With Scorecards</p></div>
            <div class="stat-card"><h3>{avgScore:F1}</h3><p>Average Score</p></div>
            <div class="stat-card"><h3>{withErrors.Count}</h3><p>Errors</p></div>
        </div>
""");

        if (report.DependencyTree != null)
            sb.Append(HtmlDependencyTree(report.DependencyTree, report.Packages));

        if (withScore.Count > 0)
        {
            sb.AppendLine("        <h2>Detailed Scorecard Results</h2>");
            foreach (var pkg in withScore.OrderByDescending(p => p.Score))
                sb.Append(HtmlPackageDetail(pkg));
        }

        if (noScore.Count > 0)
        {
            sb.AppendLine("        <h2>Packages Without Scorecards</h2>");
            foreach (var pkg in noScore)
                sb.Append(HtmlPackageNoScore(pkg));
        }

        if (withErrors.Count > 0)
        {
            sb.AppendLine("        <h2>Packages With Errors</h2>");
            foreach (var pkg in withErrors)
                sb.Append(HtmlPackageError(pkg));
        }

        sb.Append($"""
        <div class="timestamp">Report generated on {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}</div>
    </div>
</body>
</html>
""");
        return sb.ToString();
    }

    internal static string BuildMarkdown(ScorecardReport report)
    {
        var withScore = report.Packages.Where(p => p.Score != null && p.Error == null).ToList();
        var withErrors = report.Packages.Where(p => !string.IsNullOrEmpty(p.Error)).ToList();
        var avgScore = withScore.Count > 0 ? withScore.Average(p => p.Score!.Value) : 0;

        var sb = new StringBuilder();
        sb.AppendLine("# Security Scorecard Report");
        sb.AppendLine();

        var meta = new List<string> { $"**Project:** {report.Project}" };
        if (report.Framework != null) meta.Add($"**Framework:** {report.Framework}");
        meta.Add($"**Generated:** {report.GeneratedAt:yyyy-MM-dd}");
        sb.AppendLine(string.Join(" | ", meta));
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Total | With Scorecard | Avg Score | Errors |");
        sb.AppendLine("|-------|---------------|-----------|--------|");
        sb.AppendLine(
            $"| {report.Packages.Count} | {withScore.Count} | {avgScore:F1}/10 | {withErrors.Count} |");
        sb.AppendLine();

        if (report.DependencyTree != null)
            sb.Append(MarkdownDependencyTree(report.DependencyTree, report.Packages));

        if (withScore.Count > 0)
        {
            sb.AppendLine("## Detailed Results");
            sb.AppendLine();
            foreach (var pkg in withScore.OrderByDescending(p => p.Score))
                sb.Append(MarkdownPackageDetail(pkg));
        }

        return sb.ToString();
    }

    // ── HTML helpers ────────────────────────────────────────────────────────────

    private static string HtmlHead() => """
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Security Scorecard Report</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
               line-height: 1.6; color: #333; background: #f5f5f5; padding: 20px; }
        .container { max-width: 1200px; margin: 0 auto; background: white; border-radius: 8px;
                     box-shadow: 0 2px 10px rgba(0,0,0,0.1); padding: 30px; }
        h1 { color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px;
             margin-bottom: 30px; }
        h2 { color: #34495e; margin-top: 30px; margin-bottom: 15px; padding-bottom: 8px;
             border-bottom: 2px solid #ecf0f1; }
        .info-section { background: #f8f9fa; padding: 15px; border-radius: 5px; margin-bottom: 20px; }
        .info-section p { margin: 5px 0; }
        .info-label { font-weight: bold; color: #555; }
        .package-item { background: #fff; border: 1px solid #ddd; border-radius: 5px;
                        padding: 12px; margin: 8px 0; }
        .package-header { display: flex; justify-content: space-between;
                          align-items: center; margin-bottom: 8px; }
        .package-name { font-weight: bold; color: #2c3e50; font-size: 1.1em; }
        .package-version { color: #7f8c8d; font-size: 0.9em; }
        .score-badge { display: inline-block; padding: 4px 12px; border-radius: 12px;
                       font-weight: bold; font-size: 0.9em; }
        .score-excellent { background: #2ecc71; color: white; }
        .score-good { background: #27ae60; color: white; }
        .score-fair { background: #f39c12; color: white; }
        .score-poor { background: #e74c3c; color: white; }
        .score-na { background: #95a5a6; color: white; }
        .score-none { background: #bdc3c7; color: #2c3e50; }
        .transitive-package { margin-left: 30px; border-left: 3px solid #ecf0f1;
                              padding-left: 15px; }
        .checks-list { margin-top: 10px; }
        .check-item { display: flex; justify-content: space-between; padding: 6px 0;
                      border-bottom: 1px solid #ecf0f1; }
        .check-item:last-child { border-bottom: none; }
        .check-name { color: #555; }
        .check-reason { color: #7f8c8d; font-size: 0.85em; margin-top: 4px; font-style: italic; }
        .summary-stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
                         gap: 15px; margin: 20px 0; }
        .stat-card { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white;
                     padding: 20px; border-radius: 8px; text-align: center; }
        .stat-card h3 { font-size: 2em; margin-bottom: 5px; }
        .stat-card p { font-size: 0.9em; opacity: 0.9; }
        .error-message { color: #e74c3c; background: #fee; padding: 10px; border-radius: 5px;
                         border-left: 4px solid #e74c3c; }
        .timestamp { text-align: right; color: #7f8c8d; font-size: 0.85em; margin-top: 30px;
                     padding-top: 20px; border-top: 1px solid #ecf0f1; }
    </style>
</head>
""";

    private static string HtmlDependencyTree(
        ScorecardDependencyTree tree, IReadOnlyList<ScorecardReportPackage> packages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""        <h2>Dependency Tree</h2>""");
        sb.AppendLine("""        <div class="dependency-tree">""");
        sb.AppendLine("""            <h3 style="margin-top: 15px; color: #555;">Top-Level Packages</h3>""");
        foreach (var p in tree.TopLevel)
            sb.Append(HtmlPackageTreeItem(p, packages.FirstOrDefault(r => r.PackageId == p.Id), false));
        if (tree.Transitive.Count > 0)
        {
            sb.AppendLine("""            <h3 style="margin-top: 20px; color: #555;">Transitive Packages</h3>""");
            foreach (var p in tree.Transitive)
                sb.Append(HtmlPackageTreeItem(p, packages.FirstOrDefault(r => r.PackageId == p.Id), true));
        }
        sb.AppendLine("        </div>");
        return sb.ToString();
    }

    private static string HtmlPackageTreeItem(
        ScorecardPackageRef pkg, ScorecardReportPackage? result, bool isTransitive)
    {
        var cssClass = isTransitive ? "package-item transitive-package" : "package-item";
        var version = EscapeHtml(pkg.ResolvedVersion ?? pkg.RequestedVersion ?? "unknown");
        string scoreHtml;
        if (result?.Score != null)
            scoreHtml = $"""<span class="score-badge {GetScoreClass(result.Score.Value)}">{result.Score.Value:F2}/10</span>""";
        else if (result != null && !string.IsNullOrEmpty(result.Error))
            scoreHtml = """<span class="score-badge score-na">Error</span>""";
        else
            scoreHtml = """<span class="score-badge score-none">No Scorecard</span>""";

        return $"""
            <div class="{cssClass}">
                <div class="package-header">
                    <div>
                        <span class="package-name">{EscapeHtml(pkg.Id)}</span>
                        <span class="package-version">{version}</span>
                    </div>
                    {scoreHtml}
                </div>
            </div>
""";
    }

    private static string HtmlPackageDetail(ScorecardReportPackage pkg)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"""
        <div class="package-item">
            <div class="package-header">
                <div>
                    <span class="package-name">{EscapeHtml(pkg.PackageId)}</span>
                    <span class="package-version">{EscapeHtml(pkg.PackageVersion)}</span>
                </div>
                <span class="score-badge {GetScoreClass(pkg.Score!.Value)}">{pkg.Score.Value:F2}/10</span>
            </div>
        """);
        if (!string.IsNullOrEmpty(pkg.RepoName))
            sb.AppendLine($"""            <p style="margin: 8px 0; color: #555;"><strong>Repository:</strong> {EscapeHtml(pkg.RepoName)}</p>""");
        if (pkg.ScorecardDate != null || pkg.ScorecardVersion != null)
            sb.AppendLine($"""            <p style="margin: 4px 0; color: #7f8c8d; font-size: 0.9em;"><strong>Date:</strong> {EscapeHtml(pkg.ScorecardDate)} | <strong>Version:</strong> {EscapeHtml(pkg.ScorecardVersion)}</p>""");
        if (pkg.Checks.Count > 0)
        {
            sb.AppendLine("""            <div class="checks-list">""");
            foreach (var check in pkg.Checks.OrderByDescending(c => c.Score))
            {
                var checkScore = check.Score == -1 ? "N/A" : $"{check.Score}/10";
                var reasonHtml = !string.IsNullOrWhiteSpace(check.Reason)
                    ? $"""<div class="check-reason">{EscapeHtml(check.Reason)}</div>"""
                    : "";
                sb.AppendLine($"""
                <div class="check-item">
                    <div>
                        <span class="check-name">{EscapeHtml(check.Name)}</span>
                        {reasonHtml}
                    </div>
                    <span class="score-badge {GetCheckScoreClass(check.Score)}">{checkScore}</span>
                </div>
            """);
            }
            sb.AppendLine("            </div>");
        }
        sb.AppendLine("        </div>");
        return sb.ToString();
    }

    private static string HtmlPackageNoScore(ScorecardReportPackage pkg) => $"""
        <div class="package-item">
            <div class="package-header">
                <div>
                    <span class="package-name">{EscapeHtml(pkg.PackageId)}</span>
                    <span class="package-version">{EscapeHtml(pkg.PackageVersion)}</span>
                </div>
                <span class="score-badge score-none">No Scorecard</span>
            </div>
        </div>
""";

    private static string HtmlPackageError(ScorecardReportPackage pkg) => $"""
        <div class="package-item">
            <div class="package-header">
                <div>
                    <span class="package-name">{EscapeHtml(pkg.PackageId)}</span>
                    <span class="package-version">{EscapeHtml(pkg.PackageVersion)}</span>
                </div>
            </div>
            <div class="error-message">{EscapeHtml(pkg.Error)}</div>
        </div>
""";

    // ── Markdown helpers ─────────────────────────────────────────────────────────

    private static string MarkdownDependencyTree(
        ScorecardDependencyTree tree, IReadOnlyList<ScorecardReportPackage> packages)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Dependency Tree");
        sb.AppendLine();
        sb.AppendLine("### Top-Level Packages");
        sb.AppendLine();
        sb.AppendLine("| Package | Version | Score |");
        sb.AppendLine("|---------|---------|-------|");
        foreach (var p in tree.TopLevel)
        {
            var result = packages.FirstOrDefault(r => r.PackageId == p.Id);
            sb.AppendLine(
                $"| {p.Id} | {p.ResolvedVersion ?? p.RequestedVersion ?? "unknown"} " +
                $"| {ScoreEmoji(result)} |");
        }
        sb.AppendLine();
        if (tree.Transitive.Count > 0)
        {
            sb.AppendLine("### Transitive Packages");
            sb.AppendLine();
            sb.AppendLine("| Package | Version | Score |");
            sb.AppendLine("|---------|---------|-------|");
            foreach (var p in tree.Transitive)
            {
                var result = packages.FirstOrDefault(r => r.PackageId == p.Id);
                sb.AppendLine(
                    $"| {p.Id} | {p.ResolvedVersion ?? p.RequestedVersion ?? "unknown"} " +
                    $"| {ScoreEmoji(result)} |");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string MarkdownPackageDetail(ScorecardReportPackage pkg)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"### {pkg.PackageId} {pkg.PackageVersion} — {pkg.Score!.Value:F2}/10");
        sb.AppendLine();
        if (pkg.Checks.Count > 0)
        {
            sb.AppendLine("| Check | Score | Reason |");
            sb.AppendLine("|-------|-------|--------|");
            foreach (var check in pkg.Checks.OrderByDescending(c => c.Score))
            {
                var score = check.Score == -1 ? "N/A" : $"{check.Score}/10";
                var reason = (check.Reason ?? "").Replace("|", "\\|").Replace("\n", " ");
                sb.AppendLine($"| {check.Name} | {score} | {reason} |");
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    // ── Shared helpers ────────────────────────────────────────────────────────────

    private static string ScoreEmoji(ScorecardReportPackage? pkg)
    {
        if (pkg == null || (pkg.Score == null && string.IsNullOrEmpty(pkg.Error))) return "—";
        if (!string.IsNullOrEmpty(pkg.Error)) return "❌";
        return pkg.Score!.Value >= 7 ? $"✅ {pkg.Score.Value:F2}"
             : pkg.Score.Value >= 4 ? $"⚠️ {pkg.Score.Value:F2}"
             : $"❌ {pkg.Score.Value:F2}";
    }

    private static string GetScoreClass(decimal score) =>
        score >= 8 ? "score-excellent" :
        score >= 6 ? "score-good" :
        score >= 4 ? "score-fair" :
        "score-poor";

    private static string GetCheckScoreClass(int score) =>
        score == -1 ? "score-na" :
        score >= 8 ? "score-excellent" :
        score >= 6 ? "score-good" :
        score >= 4 ? "score-fair" :
        "score-poor";

    private static string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
