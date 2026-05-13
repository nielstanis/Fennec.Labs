using FennecLabs.DotNetCli;
using FennecLabs.Scorecard;

namespace FennecLabs.Cli.Commands;

internal class ScorecardCommandHandler
{
    private readonly ScorecardClient _scorecardClient;

    public ScorecardCommandHandler(ScorecardClient scorecardClient)
    {
        _scorecardClient = scorecardClient;
    }

    public async Task<int> ExecuteAsync(string? projectPath, bool generateReport)
    {
        Console.WriteLine($"Analyzing project: {projectPath ?? "all projects"}");
        Console.WriteLine();

        var packageList = projectPath != null
            ? await DotnetCliExecutor.GetPackageListAsync(projectPath)
            : await DotnetCliExecutor.GetPackageListAsync();

        if (packageList == null || packageList.Projects.Count == 0)
        {
            Console.WriteLine("No packages found in the project.");
            return 0;
        }

        var project = packageList.Projects[0];
        if (project.Frameworks.Count == 0)
        {
            Console.WriteLine("No frameworks found in the project.");
            return 0;
        }

        var framework = project.Frameworks[0];
        var topLevelPackages = framework.TopLevelPackages;
        var transitivePackages = framework.TransitivePackages;
        var allPackages = topLevelPackages.Concat(transitivePackages).ToList();

        if (allPackages.Count == 0)
        {
            Console.WriteLine("No packages found in the project.");
            return 0;
        }

        Console.WriteLine($"Found {topLevelPackages.Count} top-level and {transitivePackages.Count} transitive package(s):");
        Console.WriteLine();

        var results = new List<PackageScorecardResult>();

        foreach (var package in allPackages)
        {
            Console.Write($"Processing {package.Id} {package.ResolvedVersion}... ");

            try
            {
                var scorecardResult = await _scorecardClient.GetScorecardResultFromPackageAsync(
                    package.Id,
                    package.ResolvedVersion);

                if (scorecardResult != null)
                {
                    results.Add(new PackageScorecardResult
                    {
                        PackageId = package.Id,
                        PackageVersion = package.ResolvedVersion ?? "unknown",
                        Scorecard = scorecardResult
                    });
                    Console.WriteLine($"✓ Score: {scorecardResult.Score:F2}/10");
                }
                else
                {
                    results.Add(new PackageScorecardResult
                    {
                        PackageId = package.Id,
                        PackageVersion = package.ResolvedVersion ?? "unknown",
                        Scorecard = null
                    });
                    Console.WriteLine("No scorecard available");
                }
            }
            catch (Exception ex)
            {
                results.Add(new PackageScorecardResult
                {
                    PackageId = package.Id,
                    PackageVersion = package.ResolvedVersion ?? "unknown",
                    Scorecard = null,
                    Error = ex.Message
                });
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
        }

        PrintSummary(results);

        if (generateReport)
        {
            var reportPath = await GenerateHtmlReportAsync(packageList, results, projectPath);
            Console.WriteLine();
            Console.WriteLine($"HTML report generated: {reportPath}");
        }

        return 0;
    }

    private static void PrintSummary(List<PackageScorecardResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        Console.WriteLine();

        var packagesWithScorecards = results.Where(r => r.Scorecard != null).ToList();
        var packagesWithoutScorecards = results
            .Where(r => r.Scorecard == null && string.IsNullOrEmpty(r.Error))
            .ToList();
        var packagesWithErrors = results.Where(r => !string.IsNullOrEmpty(r.Error)).ToList();

        if (packagesWithScorecards.Count > 0)
        {
            Console.WriteLine($"Packages with scorecards ({packagesWithScorecards.Count}):");
            foreach (var result in packagesWithScorecards.OrderByDescending(r => r.Scorecard!.Score))
                Console.WriteLine($"  {result.PackageId} {result.PackageVersion}: {result.Scorecard!.Score:F2}/10");
            Console.WriteLine();
        }

        if (packagesWithoutScorecards.Count > 0)
        {
            Console.WriteLine($"Packages without scorecards ({packagesWithoutScorecards.Count}):");
            foreach (var result in packagesWithoutScorecards)
                Console.WriteLine($"  {result.PackageId} {result.PackageVersion}");
            Console.WriteLine();
        }

        if (packagesWithErrors.Count > 0)
        {
            Console.WriteLine($"Packages with errors ({packagesWithErrors.Count}):");
            foreach (var result in packagesWithErrors)
                Console.WriteLine($"  {result.PackageId} {result.PackageVersion}: {result.Error}");
            Console.WriteLine();
        }

        if (packagesWithScorecards.Count > 0)
        {
            Console.WriteLine("=== Detailed Scorecard Information ===");
            Console.WriteLine();

            foreach (var result in packagesWithScorecards)
            {
                var sc = result.Scorecard!;
                Console.WriteLine($"Package: {result.PackageId} {result.PackageVersion}");
                Console.WriteLine($"Repository: {sc.Repo.Name}");
                Console.WriteLine($"Score: {sc.Score:F2}/10");
                Console.WriteLine($"Date: {sc.Date}");
                Console.WriteLine($"Scorecard Version: {sc.Scorecard.Version}");

                if (sc.Checks.Count > 0)
                {
                    Console.WriteLine("Checks:");
                    foreach (var check in sc.Checks.OrderByDescending(c => c.Score))
                    {
                        var status = check.Score == -1 ? "N/A" : $"{check.Score}/10";
                        Console.WriteLine($"  {check.Name}: {status}");
                        if (!string.IsNullOrWhiteSpace(check.Reason))
                            Console.WriteLine($"    {check.Reason}");
                    }
                }

                Console.WriteLine();
            }
        }
    }

    private static async Task<string> GenerateHtmlReportAsync(
        PackageListResult? packageList,
        List<PackageScorecardResult> results,
        string? projectPath)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var reportFileName = $"scorecard-report-{timestamp}.html";
        var reportPath = Path.Combine(Directory.GetCurrentDirectory(), reportFileName);

        var html = BuildHtmlReport(packageList, results, projectPath);

        await File.WriteAllTextAsync(reportPath, html);
        return reportPath;
    }

    private static string BuildHtmlReport(
        PackageListResult? packageList,
        List<PackageScorecardResult> results,
        string? projectPath)
    {
        var packagesWithScorecards = results.Where(r => r.Scorecard != null).ToList();
        var packagesWithoutScorecards = results
            .Where(r => r.Scorecard == null && string.IsNullOrEmpty(r.Error))
            .ToList();
        var packagesWithErrors = results.Where(r => !string.IsNullOrEmpty(r.Error)).ToList();
        var avgScore = packagesWithScorecards.Count > 0
            ? packagesWithScorecards.Average(r => r.Scorecard!.Score)
            : 0;

        var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Security Scorecard Report</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; background: #f5f5f5; padding: 20px; }}
        .container {{ max-width: 1200px; margin: 0 auto; background: white; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); padding: 30px; }}
        h1 {{ color: #2c3e50; border-bottom: 3px solid #3498db; padding-bottom: 10px; margin-bottom: 30px; }}
        h2 {{ color: #34495e; margin-top: 30px; margin-bottom: 15px; padding-bottom: 8px; border-bottom: 2px solid #ecf0f1; }}
        .info-section {{ background: #f8f9fa; padding: 15px; border-radius: 5px; margin-bottom: 20px; }}
        .info-section p {{ margin: 5px 0; }}
        .info-label {{ font-weight: bold; color: #555; }}
        .package-item {{ background: #fff; border: 1px solid #ddd; border-radius: 5px; padding: 12px; margin: 8px 0; }}
        .package-header {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px; }}
        .package-name {{ font-weight: bold; color: #2c3e50; font-size: 1.1em; }}
        .package-version {{ color: #7f8c8d; font-size: 0.9em; }}
        .score-badge {{ display: inline-block; padding: 4px 12px; border-radius: 12px; font-weight: bold; font-size: 0.9em; }}
        .score-excellent {{ background: #2ecc71; color: white; }}
        .score-good {{ background: #27ae60; color: white; }}
        .score-fair {{ background: #f39c12; color: white; }}
        .score-poor {{ background: #e74c3c; color: white; }}
        .score-na {{ background: #95a5a6; color: white; }}
        .score-none {{ background: #bdc3c7; color: #2c3e50; }}
        .transitive-package {{ margin-left: 30px; border-left: 3px solid #ecf0f1; padding-left: 15px; }}
        .checks-list {{ margin-top: 10px; }}
        .check-item {{ display: flex; justify-content: space-between; padding: 6px 0; border-bottom: 1px solid #ecf0f1; }}
        .check-item:last-child {{ border-bottom: none; }}
        .check-name {{ color: #555; }}
        .check-reason {{ color: #7f8c8d; font-size: 0.85em; margin-top: 4px; font-style: italic; }}
        .summary-stats {{ display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 15px; margin: 20px 0; }}
        .stat-card {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; border-radius: 8px; text-align: center; }}
        .stat-card h3 {{ font-size: 2em; margin-bottom: 5px; }}
        .stat-card p {{ font-size: 0.9em; opacity: 0.9; }}
        .error-message {{ color: #e74c3c; background: #fee; padding: 10px; border-radius: 5px; border-left: 4px solid #e74c3c; }}
        .timestamp {{ text-align: right; color: #7f8c8d; font-size: 0.85em; margin-top: 30px; padding-top: 20px; border-top: 1px solid #ecf0f1; }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>Security Scorecard Report</h1>
        <div class=""info-section"">
            <p><span class=""info-label"">Generated:</span> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
            <p><span class=""info-label"">Project:</span> {projectPath ?? "Current Directory"}</p>
        </div>
        <div class=""summary-stats"">
            <div class=""stat-card""><h3>{results.Count}</h3><p>Total Packages</p></div>
            <div class=""stat-card""><h3>{packagesWithScorecards.Count}</h3><p>With Scorecards</p></div>
            <div class=""stat-card""><h3>{avgScore:F1}</h3><p>Average Score</p></div>
            <div class=""stat-card""><h3>{packagesWithErrors.Count}</h3><p>Errors</p></div>
        </div>";

        if (packageList != null && packageList.Projects.Count > 0)
        {
            html += @"
        <h2>Dependency Tree</h2>
        <div class=""dependency-tree"">";

            var project = packageList.Projects[0];
            if (project.Frameworks.Count > 0)
            {
                var framework = project.Frameworks[0];

                html += @"
            <h3 style=""margin-top: 15px; color: #555;"">Top-Level Packages</h3>";
                foreach (var package in framework.TopLevelPackages)
                {
                    var result = results.FirstOrDefault(r => r.PackageId == package.Id);
                    html += GeneratePackageHtml(package, result, false);
                }

                if (framework.TransitivePackages.Count > 0)
                {
                    html += @"
            <h3 style=""margin-top: 20px; color: #555;"">Transitive Packages</h3>";
                    foreach (var package in framework.TransitivePackages)
                    {
                        var result = results.FirstOrDefault(r => r.PackageId == package.Id);
                        html += GeneratePackageHtml(package, result, true);
                    }
                }
            }

            html += @"
        </div>";
        }

        if (packagesWithScorecards.Count > 0)
        {
            html += @"
        <h2>Detailed Scorecard Results</h2>";

            foreach (var result in packagesWithScorecards.OrderByDescending(r => r.Scorecard!.Score))
            {
                var sc = result.Scorecard!;
                html += $@"
        <div class=""package-item"">
            <div class=""package-header"">
                <div>
                    <span class=""package-name"">{EscapeHtml(result.PackageId)}</span>
                    <span class=""package-version"">{EscapeHtml(result.PackageVersion)}</span>
                </div>
                <span class=""score-badge {GetScoreClass(sc.Score)}"">{sc.Score:F2}/10</span>
            </div>
            <p style=""margin: 8px 0; color: #555;""><strong>Repository:</strong> {EscapeHtml(sc.Repo.Name)}</p>
            <p style=""margin: 4px 0; color: #7f8c8d; font-size: 0.9em;""><strong>Date:</strong> {EscapeHtml(sc.Date)} | <strong>Scorecard Version:</strong> {EscapeHtml(sc.Scorecard.Version)}</p>";

                if (sc.Checks.Count > 0)
                {
                    html += @"
            <div class=""checks-list"">";
                    foreach (var check in sc.Checks.OrderByDescending(c => c.Score))
                    {
                        var checkScore = check.Score == -1 ? "N/A" : $"{check.Score}/10";
                        html += $@"
                <div class=""check-item"">
                    <div>
                        <span class=""check-name"">{EscapeHtml(check.Name)}</span>
                        {(!string.IsNullOrWhiteSpace(check.Reason) ? $@"<div class=""check-reason"">{EscapeHtml(check.Reason)}</div>" : "")}
                    </div>
                    <span class=""score-badge {GetCheckScoreClass(check.Score)}"">{checkScore}</span>
                </div>";
                    }
                    html += @"
            </div>";
                }

                html += @"
        </div>";
            }
        }

        if (packagesWithoutScorecards.Count > 0)
        {
            html += @"
        <h2>Packages Without Scorecards</h2>";
            foreach (var result in packagesWithoutScorecards)
            {
                html += $@"
        <div class=""package-item"">
            <div class=""package-header"">
                <div>
                    <span class=""package-name"">{EscapeHtml(result.PackageId)}</span>
                    <span class=""package-version"">{EscapeHtml(result.PackageVersion)}</span>
                </div>
                <span class=""score-badge score-none"">No Scorecard</span>
            </div>
        </div>";
            }
        }

        if (packagesWithErrors.Count > 0)
        {
            html += @"
        <h2>Packages With Errors</h2>";
            foreach (var result in packagesWithErrors)
            {
                html += $@"
        <div class=""package-item"">
            <div class=""package-header"">
                <div>
                    <span class=""package-name"">{EscapeHtml(result.PackageId)}</span>
                    <span class=""package-version"">{EscapeHtml(result.PackageVersion)}</span>
                </div>
            </div>
            <div class=""error-message"">{EscapeHtml(result.Error ?? "Unknown error")}</div>
        </div>";
            }
        }

        html += $@"
        <div class=""timestamp"">
            Report generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}
        </div>
    </div>
</body>
</html>";

        return html;
    }

    private static string GeneratePackageHtml(PackageReference package, PackageScorecardResult? result, bool isTransitive)
    {
        var cssClass = isTransitive ? "package-item transitive-package" : "package-item";
        string scoreHtml;

        if (result?.Scorecard != null)
        {
            var score = result.Scorecard.Score;
            scoreHtml = $@"<span class=""score-badge {GetScoreClass(score)}"">{score:F2}/10</span>";
        }
        else if (result != null && !string.IsNullOrEmpty(result.Error))
        {
            scoreHtml = @"<span class=""score-badge score-na"">Error</span>";
        }
        else
        {
            scoreHtml = @"<span class=""score-badge score-none"">No Scorecard</span>";
        }

        return $@"
            <div class=""{cssClass}"">
                <div class=""package-header"">
                    <div>
                        <span class=""package-name"">{EscapeHtml(package.Id)}</span>
                        <span class=""package-version"">{EscapeHtml(package.ResolvedVersion ?? package.RequestedVersion ?? "unknown")}</span>
                    </div>
                    {scoreHtml}
                </div>
            </div>";
    }

    private static string GetScoreClass(decimal score)
    {
        if (score >= 8) return "score-excellent";
        if (score >= 6) return "score-good";
        if (score >= 4) return "score-fair";
        return "score-poor";
    }

    private static string GetCheckScoreClass(int score)
    {
        if (score == -1) return "score-na";
        if (score >= 8) return "score-excellent";
        if (score >= 6) return "score-good";
        if (score >= 4) return "score-fair";
        return "score-poor";
    }

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

    internal class PackageScorecardResult
    {
        public string PackageId { get; set; } = string.Empty;
        public string PackageVersion { get; set; } = string.Empty;
        public ScorecardResult? Scorecard { get; set; }
        public string? Error { get; set; }
    }
}
