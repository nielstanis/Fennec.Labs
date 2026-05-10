using System.CommandLine;
using System.Diagnostics;
using System.IO.Compression;
using FennecLabs.AssemblyDiff;
using FennecLabs.DotNetCli;
using FennecLabs.Instrumentation;
using FennecLabs.Instrumentation.Output;
using FennecLabs.NuGet;
using FennecLabs.Scorecard;
using Mono.Cecil;
using NuGet.Versioning;

namespace FennecLabs;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Fennec Labs CLI");

        var filenameOption = new Option<string>(
            "--filename",
            "-f"
        )
        {
            Description = "Path to the assembly file to instrument"
        };

        var nugetOption = new Option<string>(
            "--nuget",
            "-n"
        )
        {
            Description = "NuGet package ID to download and instrument"
        };

        var versionOption = new Option<string>(
            "--version",
            "-v"
        )
        {
            Description = "Version of the NuGet package (optional, uses latest if not specified)"
        };

        var instrumentCommand = new Command("instrument", "Instrument assembly files or NuGet packages");
        instrumentCommand.Options.Add(filenameOption);
        instrumentCommand.Options.Add(nugetOption);
        instrumentCommand.Options.Add(versionOption);
        instrumentCommand.SetAction(async (ParseResult parseResult) =>
        {
            var filename = parseResult.GetValue(filenameOption);
            var nuget = parseResult.GetValue(nugetOption);
            var version = parseResult.GetValue(versionOption);

            if (!string.IsNullOrWhiteSpace(nuget))
            {
                await InstrumentNuGetPackageAsync(nuget, version);
            }
            else if (!string.IsNullOrWhiteSpace(filename))
            {
                await InstrumentAssemblyAsync(filename);
            }
            else
            {
                Console.Error.WriteLine("Either --filename or --nuget must be specified.");
            }
        });

        var projectPathOption = new Option<string>(
            "--project",
            "-p"
        )
        {
            Description = "Path to the .csproj file"
        };

        var reportOption = new Option<bool>(
            "--report",
            "-r"
        )
        {
            Description = "Generate an HTML report with scorecard results and dependency tree"
        };

        var scorecardCommand = new Command("scorecard", "Get security scorecards for packages in a project");
        scorecardCommand.Options.Add(projectPathOption);
        scorecardCommand.Options.Add(reportOption);
        scorecardCommand.SetAction(async (ParseResult parseResult) =>
        {
            var projectPath = parseResult.GetValue(projectPathOption);
            var generateReport = parseResult.GetValue(reportOption);
            await GetScorecardsForProjectAsync(projectPath, generateReport);
        });

        var compareNugetOption = new Option<string>(
            "--nuget",
            "-n"
        )
        {
            Description = "NuGet package ID to compare"
        };

        var compareVersionOption = new Option<string>(
            "--version",
            "-v"
        )
        {
            Description = "Version of the NuGet package to compare (optional, uses latest if not specified)"
        };

        var compareCommand = new Command("compare", "Compare assemblies between two versions of a NuGet package");
        compareCommand.Options.Add(compareNugetOption);
        compareCommand.Options.Add(compareVersionOption);
        compareCommand.SetAction(async (ParseResult parseResult) =>
        {
            var nuget = parseResult.GetValue(compareNugetOption);
            var version = parseResult.GetValue(compareVersionOption);

            if (string.IsNullOrWhiteSpace(nuget))
            {
                Console.Error.WriteLine("--nuget is required.");
                return;
            }

            await CompareNuGetPackageAsync(nuget, version);
        });

        var reproduceFilenameOption = new Option<string>(
            "--filename",
            "-f"
        )
        {
            Description = "Path to the .nupkg file to compare"
        };

        var reproduceNugetOption = new Option<string>(
            "--nuget",
            "-n"
        )
        {
            Description = "NuGet package ID to compare against"
        };

        var reproduceVersionOption = new Option<string>(
            "--version",
            "-v"
        )
        {
            Description = "Version of the NuGet package to compare against (optional, uses latest if not specified)"
        };

        var reproduceCommand = new Command("reproduce", "Compare a local .nupkg file with a NuGet package from the feed");
        reproduceCommand.Options.Add(reproduceFilenameOption);
        reproduceCommand.Options.Add(reproduceNugetOption);
        reproduceCommand.Options.Add(reproduceVersionOption);
        reproduceCommand.SetAction(async (ParseResult parseResult) =>
        {
            var filename = parseResult.GetValue(reproduceFilenameOption);
            var nuget = parseResult.GetValue(reproduceNugetOption);
            var version = parseResult.GetValue(reproduceVersionOption);

            if (string.IsNullOrWhiteSpace(filename))
            {
                Console.Error.WriteLine("--filename is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(nuget))
            {
                Console.Error.WriteLine("--nuget is required.");
                return;
            }

            await ReproduceComparisonAsync(filename, nuget, version);
        });

        rootCommand.Subcommands.Add(instrumentCommand);
        rootCommand.Subcommands.Add(scorecardCommand);
        rootCommand.Subcommands.Add(compareCommand);
        rootCommand.Subcommands.Add(reproduceCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }

    static async Task GetScorecardsForProjectAsync(string? projectPath = null, bool generateReport = false)
    {

        Console.WriteLine($"Analyzing project: {projectPath ?? "all projects"}");
        Console.WriteLine();

        // Get package list from the project
        var packageList = projectPath != null ? await DotnetCliExecutor.GetPackageListAsync(projectPath) : await DotnetCliExecutor.GetPackageListAsync();
        
        if (packageList == null || packageList.Projects.Count == 0)
        {
            Console.WriteLine("No packages found in the project.");
            return;
        }

        var project = packageList.Projects[0];
        if (project.Frameworks.Count == 0)
        {
            Console.WriteLine("No frameworks found in the project.");
            return;
        }

        var framework = project.Frameworks[0];
        var topLevelPackages = framework.TopLevelPackages;

        if (topLevelPackages.Count == 0)
        {
            Console.WriteLine("No top-level packages found in the project.");
            return;
        }

        Console.WriteLine($"Found {topLevelPackages.Count} top-level package(s):");
        Console.WriteLine();

        // Initialize services
        //var nugetService = new NuGetService();
        var scorecardClient = new ScorecardClient();

        // Process each package
        var results = new List<PackageScorecardResult>();

        foreach (var package in topLevelPackages)
        {
            Console.Write($"Processing {package.Id} {package.ResolvedVersion}... ");
            
            try
            {
                var scorecardResult = await scorecardClient.GetScorecardResultFromPackageAsync(
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
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        Console.WriteLine();

        var packagesWithScorecards = results.Where(r => r.Scorecard != null).ToList();
        var packagesWithoutScorecards = results.Where(r => r.Scorecard == null && string.IsNullOrEmpty(r.Error)).ToList();
        var packagesWithErrors = results.Where(r => !string.IsNullOrEmpty(r.Error)).ToList();

        if (packagesWithScorecards.Count > 0)
        {
            Console.WriteLine($"Packages with scorecards ({packagesWithScorecards.Count}):");
            foreach (var result in packagesWithScorecards.OrderByDescending(r => r.Scorecard!.Score))
            {
                Console.WriteLine($"  {result.PackageId} {result.PackageVersion}: {result.Scorecard!.Score:F2}/10");
            }
            Console.WriteLine();
        }

        if (packagesWithoutScorecards.Count > 0)
        {
            Console.WriteLine($"Packages without scorecards ({packagesWithoutScorecards.Count}):");
            foreach (var result in packagesWithoutScorecards)
            {
                Console.WriteLine($"  {result.PackageId} {result.PackageVersion}");
            }
            Console.WriteLine();
        }

        if (packagesWithErrors.Count > 0)
        {
            Console.WriteLine($"Packages with errors ({packagesWithErrors.Count}):");
            foreach (var result in packagesWithErrors)
            {
                Console.WriteLine($"  {result.PackageId} {result.PackageVersion}: {result.Error}");
            }
            Console.WriteLine();
        }

        // Show detailed scorecard information
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
                        {
                            Console.WriteLine($"    {check.Reason}");
                        }
                    }
                }

                Console.WriteLine();
            }
        }

        // Generate HTML report if requested
        if (generateReport)
        {
            var reportPath = await GenerateHtmlReportAsync(packageList, results, projectPath);
            Console.WriteLine();
            Console.WriteLine($"HTML report generated: {reportPath}");
        }
    }

    static async Task<string> GenerateHtmlReportAsync(
        PackageListResult? packageList,
        List<PackageScorecardResult> results,
        string? projectPath)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var reportFileName = $"scorecard-report-{timestamp}.html";
        var reportPath = Path.Combine(Directory.GetCurrentDirectory(), reportFileName);

        var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Security Scorecard Report</title>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
            line-height: 1.6;
            color: #333;
            background: #f5f5f5;
            padding: 20px;
        }}
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 8px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
            padding: 30px;
        }}
        h1 {{
            color: #2c3e50;
            border-bottom: 3px solid #3498db;
            padding-bottom: 10px;
            margin-bottom: 30px;
        }}
        h2 {{
            color: #34495e;
            margin-top: 30px;
            margin-bottom: 15px;
            padding-bottom: 8px;
            border-bottom: 2px solid #ecf0f1;
        }}
        .info-section {{
            background: #f8f9fa;
            padding: 15px;
            border-radius: 5px;
            margin-bottom: 20px;
        }}
        .info-section p {{
            margin: 5px 0;
        }}
        .info-label {{
            font-weight: bold;
            color: #555;
        }}
        .dependency-tree {{
            margin: 20px 0;
        }}
        .package-item {{
            background: #fff;
            border: 1px solid #ddd;
            border-radius: 5px;
            padding: 12px;
            margin: 8px 0;
            transition: box-shadow 0.2s;
        }}
        .package-item:hover {{
            box-shadow: 0 2px 8px rgba(0,0,0,0.1);
        }}
        .package-header {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 8px;
        }}
        .package-name {{
            font-weight: bold;
            color: #2c3e50;
            font-size: 1.1em;
        }}
        .package-version {{
            color: #7f8c8d;
            font-size: 0.9em;
        }}
        .score-badge {{
            display: inline-block;
            padding: 4px 12px;
            border-radius: 12px;
            font-weight: bold;
            font-size: 0.9em;
        }}
        .score-excellent {{
            background: #2ecc71;
            color: white;
        }}
        .score-good {{
            background: #27ae60;
            color: white;
        }}
        .score-fair {{
            background: #f39c12;
            color: white;
        }}
        .score-poor {{
            background: #e74c3c;
            color: white;
        }}
        .score-na {{
            background: #95a5a6;
            color: white;
        }}
        .score-none {{
            background: #bdc3c7;
            color: #2c3e50;
        }}
        .transitive-package {{
            margin-left: 30px;
            border-left: 3px solid #ecf0f1;
            padding-left: 15px;
        }}
        .checks-list {{
            margin-top: 10px;
        }}
        .check-item {{
            display: flex;
            justify-content: space-between;
            padding: 6px 0;
            border-bottom: 1px solid #ecf0f1;
        }}
        .check-item:last-child {{
            border-bottom: none;
        }}
        .check-name {{
            color: #555;
        }}
        .check-reason {{
            color: #7f8c8d;
            font-size: 0.85em;
            margin-top: 4px;
            font-style: italic;
        }}
        .summary-stats {{
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin: 20px 0;
        }}
        .stat-card {{
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            border-radius: 8px;
            text-align: center;
        }}
        .stat-card h3 {{
            font-size: 2em;
            margin-bottom: 5px;
        }}
        .stat-card p {{
            font-size: 0.9em;
            opacity: 0.9;
        }}
        .error-message {{
            color: #e74c3c;
            background: #fee;
            padding: 10px;
            border-radius: 5px;
            border-left: 4px solid #e74c3c;
        }}
        .no-data {{
            color: #7f8c8d;
            font-style: italic;
            text-align: center;
            padding: 20px;
        }}
        .timestamp {{
            text-align: right;
            color: #7f8c8d;
            font-size: 0.85em;
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid #ecf0f1;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <h1>Security Scorecard Report</h1>
        
        <div class=""info-section"">
            <p><span class=""info-label"">Generated:</span> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</p>
            <p><span class=""info-label"">Project:</span> {projectPath ?? "Current Directory"}</p>
        </div>";

        // Summary statistics
        var packagesWithScorecards = results.Where(r => r.Scorecard != null).ToList();
        var packagesWithoutScorecards = results.Where(r => r.Scorecard == null && string.IsNullOrEmpty(r.Error)).ToList();
        var packagesWithErrors = results.Where(r => !string.IsNullOrEmpty(r.Error)).ToList();
        var avgScore = packagesWithScorecards.Count > 0 
            ? packagesWithScorecards.Average(r => r.Scorecard!.Score) 
            : 0;

        html += $@"
        <div class=""summary-stats"">
            <div class=""stat-card"">
                <h3>{results.Count}</h3>
                <p>Total Packages</p>
            </div>
            <div class=""stat-card"">
                <h3>{packagesWithScorecards.Count}</h3>
                <p>With Scorecards</p>
            </div>
            <div class=""stat-card"">
                <h3>{avgScore:F1}</h3>
                <p>Average Score</p>
            </div>
            <div class=""stat-card"">
                <h3>{packagesWithErrors.Count}</h3>
                <p>Errors</p>
            </div>
        </div>";

        // Dependency Tree
        if (packageList != null && packageList.Projects.Count > 0)
        {
            html += @"
        <h2>Dependency Tree</h2>
        <div class=""dependency-tree"">";

            var project = packageList.Projects[0];
            if (project.Frameworks.Count > 0)
            {
                var framework = project.Frameworks[0];
                
                // Top-level packages
                html += @"
            <h3 style=""margin-top: 15px; color: #555;"">Top-Level Packages</h3>";
                foreach (var package in framework.TopLevelPackages)
                {
                    var result = results.FirstOrDefault(r => r.PackageId == package.Id);
                    html += GeneratePackageHtml(package, result, false);
                }

                // Transitive packages
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

        // Detailed Scorecard Results
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

        // Packages without scorecards
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

        // Packages with errors
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

        await File.WriteAllTextAsync(reportPath, html);
        return reportPath;
    }

    static string GeneratePackageHtml(PackageReference package, PackageScorecardResult? result, bool isTransitive)
    {
        var cssClass = isTransitive ? "package-item transitive-package" : "package-item";
        var scoreHtml = "";
        
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

    static string GetScoreClass(decimal score)
    {
        if (score >= 8) return "score-excellent";
        if (score >= 6) return "score-good";
        if (score >= 4) return "score-fair";
        return "score-poor";
    }

    static string GetCheckScoreClass(int score)
    {
        if (score == -1) return "score-na";
        if (score >= 8) return "score-excellent";
        if (score >= 6) return "score-good";
        if (score >= 4) return "score-fair";
        return "score-poor";
    }

    static string EscapeHtml(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    static async Task InstrumentAssemblyAsync(string filename)
    {
        if (!File.Exists(filename))
        {
            Console.Error.WriteLine($"Assembly file not found: {filename}");
            return;
        }

        Console.WriteLine($"Instrumenting assembly: {filename}");
        
        var analyzer = new AssemblyAnalyzer(filename);
        var result = analyzer.Analyse();

        if (result.HasError)
        {
            Console.Error.WriteLine($"Error analyzing assembly: {result.ExceptionOccurred?.Message}");
            return;
        }

        var writer = WriterFactory.CreateWriter("FXT", "fenneclabs");
        await writer.WriteOutputAsync(result);
        
        Console.WriteLine($"Instrumentation complete. Output written to fenneclabs folder.");
    }

    static async Task InstrumentNuGetPackageAsync(string packageId, string? version)
    {
        Console.WriteLine($"Downloading NuGet package: {packageId} {version ?? "latest"}");
        
        var nugetService = new NuGetService();
        
        try
        {
            // Download the package
            var packagePath = await nugetService.DownloadPackageAsync(packageId, version);
            Console.WriteLine($"Package downloaded to: {packagePath}");

            // Get package contents to find DLL files
            var contents = await nugetService.GetPackageContentsAsync(packageId, version);
            var dllFiles = contents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                    && !f.Path.Contains("_._")) // Exclude placeholder files
                .ToList();

            if (dllFiles.Count == 0)
            {
                Console.WriteLine("No DLL files found in the package.");
                return;
            }

            Console.WriteLine($"Found {dllFiles.Count} DLL file(s) in the package:");
            foreach (var dll in dllFiles)
            {
                Console.WriteLine($"  - {dll.Path}");
            }
            Console.WriteLine();

            // Instrument each DLL
            var writer = WriterFactory.CreateWriter("FXT", ".");
            int successCount = 0;
            int errorCount = 0;

            foreach (var dll in dllFiles)
            {
                Console.Write($"Instrumenting {dll.Path}... ");
                try
                {
                    var analyzer = new AssemblyAnalyzer(dll.FullPath);
                    var result = analyzer.Analyse();

                    if (result.HasError)
                    {
                        Console.WriteLine($"Error: {result.ExceptionOccurred?.Message}");
                        errorCount++;
                    }
                    else
                    {
                        // Pass the relative path from the package to preserve folder structure
                        await writer.WriteOutputAsync(result, dll.Path);
                        Console.WriteLine("✓");
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    errorCount++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Instrumentation complete: {successCount} succeeded, {errorCount} failed.");
            Console.WriteLine($"Output written to fenneclabs folder.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error downloading or processing package: {ex.Message}");
        }
    }

    static async Task CompareNuGetPackageAsync(string packageId, string? version)
    {
        Console.WriteLine($"Comparing NuGet package: {packageId}");
        
        var nugetService = new NuGetService();
        
        try
        {
            // Get all versions of the package
            var allVersions = await nugetService.GetPackageVersionsAsync(packageId, includePrerelease: false);
            var sortedVersions = allVersions.OrderByDescending(v => v).ToList();

            if (sortedVersions.Count < 2)
            {
                Console.WriteLine($"Package '{packageId}' has less than 2 versions. Cannot compare.");
                return;
            }

            NuGetVersion? currentVersion = null;
            NuGetVersion? previousVersion = null;

            if (!string.IsNullOrWhiteSpace(version))
            {
                if (!NuGetVersion.TryParse(version, out var parsedVersion))
                {
                    Console.Error.WriteLine($"Invalid version format: {version}");
                    return;
                }
                currentVersion = parsedVersion;

                // Find the index of the current version
                var currentIndex = sortedVersions.FindIndex(v => v == currentVersion!);
                if (currentIndex == -1)
                {
                    Console.Error.WriteLine($"Version '{version}' not found for package '{packageId}'");
                    return;
                }

                if (currentIndex == sortedVersions.Count - 1)
                {
                    Console.Error.WriteLine($"Version '{version}' is the oldest version. Cannot compare with previous version.");
                    return;
                }

                previousVersion = sortedVersions[currentIndex + 1];
            }
            else
            {
                // Use latest and previous latest
                currentVersion = sortedVersions[0];
                previousVersion = sortedVersions[1];
            }

            if (currentVersion == null || previousVersion == null)
            {
                Console.Error.WriteLine("Failed to determine versions for comparison.");
                return;
            }

            Console.WriteLine($"Comparing version {currentVersion} with previous version {previousVersion}");
            Console.WriteLine();

            // Download both versions
            Console.WriteLine($"Downloading version {currentVersion}...");
            var currentPackagePath = await nugetService.DownloadPackageAsync(packageId, currentVersion.ToNormalizedString());
            Console.WriteLine($"Downloaded to: {currentPackagePath}");

            Console.WriteLine($"Downloading version {previousVersion}...");
            var previousPackagePath = await nugetService.DownloadPackageAsync(packageId, previousVersion.ToNormalizedString());
            Console.WriteLine($"Downloaded to: {previousPackagePath}");
            Console.WriteLine();

            // Get contents of both packages
            var currentContents = await nugetService.GetPackageContentsAsync(packageId, currentVersion.ToNormalizedString());
            var previousContents = await nugetService.GetPackageContentsAsync(packageId, previousVersion.ToNormalizedString());

            // Find DLL files in both packages
            var currentDlls = currentContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            var previousDlls = previousContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            // Find matching DLLs by relative path
            var matchingDlls = currentDlls.Keys.Intersect(previousDlls.Keys).ToList();
            var onlyInCurrent = currentDlls.Keys.Except(previousDlls.Keys).ToList();
            var onlyInPrevious = previousDlls.Keys.Except(currentDlls.Keys).ToList();

            Console.WriteLine($"Found {matchingDlls.Count} matching DLL file(s) to compare");
            if (onlyInCurrent.Count > 0)
            {
                Console.WriteLine($"  {onlyInCurrent.Count} DLL(s) only in version {currentVersion}");
            }
            if (onlyInPrevious.Count > 0)
            {
                Console.WriteLine($"  {onlyInPrevious.Count} DLL(s) only in version {previousVersion}");
            }
            Console.WriteLine();

            if (matchingDlls.Count == 0)
            {
                Console.WriteLine("No matching DLL files found to compare.");
                return;
            }

            // Compare each matching DLL
            int identicalCount = 0;
            int differentCount = 0;
            int errorCount = 0;

            foreach (var dllPath in matchingDlls)
            {
                Console.WriteLine($"Comparing {dllPath}...");
                try
                {
                    var currentDll = currentDlls[dllPath];
                    var previousDll = previousDlls[dllPath];

                    using var assembly1 = AssemblyDefinition.ReadAssembly(previousDll.FullPath);
                    using var assembly2 = AssemblyDefinition.ReadAssembly(currentDll.FullPath);

                    var comparer = new AssemblyComparer(assembly1, assembly2);
                    var result = comparer.Compare();

                    if (result.AreEqual)
                    {
                        Console.WriteLine($"  ✓ Identical");
                        identicalCount++;
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ Differences found ({result.Differences.Count} difference(s))");
                        differentCount++;

                        // Show summary
                        if (result.TypesOnlyInAssembly1.Count > 0)
                        {
                            Console.WriteLine($"    - {result.TypesOnlyInAssembly1.Count} type(s) only in {previousVersion}");
                        }
                        if (result.TypesOnlyInAssembly2.Count > 0)
                        {
                            Console.WriteLine($"    - {result.TypesOnlyInAssembly2.Count} type(s) only in {currentVersion}");
                        }
                        if (result.MethodBodyDifferences.Count > 0)
                        {
                            Console.WriteLine($"    - {result.MethodBodyDifferences.Count} method(s) with body differences");
                        }

                        // Show detailed report
                        Console.WriteLine();
                        Console.WriteLine("  Detailed Report:");
                        var report = result.GenerateReport();
                        foreach (var line in report.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                Console.WriteLine($"    {line}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ Error: {ex.Message}");
                    errorCount++;
                }
                Console.WriteLine();
            }

            // Summary
            Console.WriteLine("=== Comparison Summary ===");
            Console.WriteLine($"Package: {packageId}");
            Console.WriteLine($"Version {currentVersion} vs {previousVersion}");
            Console.WriteLine($"  Identical: {identicalCount}");
            Console.WriteLine($"  Different: {differentCount}");
            Console.WriteLine($"  Errors: {errorCount}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error comparing package: {ex.Message}");
        }
    }

    static async Task ReproduceComparisonAsync(string nupkgFilePath, string packageId, string? version)
    {
        if (!File.Exists(nupkgFilePath))
        {
            Console.Error.WriteLine($"File not found: {nupkgFilePath}");
            return;
        }

        if (!nupkgFilePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"File must be a .nupkg file: {nupkgFilePath}");
            return;
        }

        Console.WriteLine($"Comparing local package file: {nupkgFilePath}");
        Console.WriteLine($"With NuGet package: {packageId} {version ?? "latest"}");
        Console.WriteLine();

        var nugetService = new NuGetService();
        string? tempExtractPath = null;

        try
        {
            // Extract the local .nupkg file
            tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempExtractPath);

            Console.WriteLine($"Extracting local package to: {tempExtractPath}");
            await ExtractNupkgFileAsync(nupkgFilePath, tempExtractPath);
            Console.WriteLine("Extraction complete.");
            Console.WriteLine();

            // Get DLL files from the extracted .nupkg
            var localContents = GetPackageContentsFromDirectory(tempExtractPath);
            var localDlls = localContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            // Download the NuGet package from the feed
            Console.WriteLine($"Downloading NuGet package: {packageId} {version ?? "latest"}");
            var feedPackagePath = await nugetService.DownloadPackageAsync(packageId, version);
            Console.WriteLine($"Downloaded to: {feedPackagePath}");
            Console.WriteLine();

            // Get DLL files from the downloaded package
            var feedContents = await nugetService.GetPackageContentsAsync(packageId, version);
            var feedDlls = feedContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            // Find matching DLLs by relative path
            var matchingDlls = localDlls.Keys.Intersect(feedDlls.Keys).ToList();
            var onlyInLocal = localDlls.Keys.Except(feedDlls.Keys).ToList();
            var onlyInFeed = feedDlls.Keys.Except(localDlls.Keys).ToList();

            Console.WriteLine($"Found {matchingDlls.Count} matching DLL file(s) to compare");
            if (onlyInLocal.Count > 0)
            {
                Console.WriteLine($"  {onlyInLocal.Count} DLL(s) only in local package");
            }
            if (onlyInFeed.Count > 0)
            {
                Console.WriteLine($"  {onlyInFeed.Count} DLL(s) only in feed package");
            }
            Console.WriteLine();

            if (matchingDlls.Count == 0)
            {
                Console.WriteLine("No matching DLL files found to compare.");
                return;
            }

            // Compare each matching DLL
            int identicalCount = 0;
            int differentCount = 0;
            int errorCount = 0;

            foreach (var dllPath in matchingDlls)
            {
                Console.WriteLine($"Comparing {dllPath}...");
                try
                {
                    var localDll = localDlls[dllPath];
                    var feedDll = feedDlls[dllPath];

                    using var assembly1 = AssemblyDefinition.ReadAssembly(localDll.FullPath);
                    using var assembly2 = AssemblyDefinition.ReadAssembly(feedDll.FullPath);

                    var comparer = new AssemblyComparer(assembly1, assembly2);
                    var result = comparer.Compare();

                    if (result.AreEqual)
                    {
                        Console.WriteLine($"  ✓ Identical");
                        identicalCount++;
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ Differences found ({result.Differences.Count} difference(s))");
                        differentCount++;

                        // Show summary
                        if (result.TypesOnlyInAssembly1.Count > 0)
                        {
                            Console.WriteLine($"    - {result.TypesOnlyInAssembly1.Count} type(s) only in local package");
                        }
                        if (result.TypesOnlyInAssembly2.Count > 0)
                        {
                            Console.WriteLine($"    - {result.TypesOnlyInAssembly2.Count} type(s) only in feed package");
                        }
                        if (result.MethodBodyDifferences.Count > 0)
                        {
                            Console.WriteLine($"    - {result.MethodBodyDifferences.Count} method(s) with body differences");
                        }

                        // Show detailed report
                        Console.WriteLine();
                        Console.WriteLine("  Detailed Report:");
                        var report = result.GenerateReport();
                        foreach (var line in report.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                Console.WriteLine($"    {line}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ Error: {ex.Message}");
                    errorCount++;
                }
                Console.WriteLine();
            }

            // Summary
            Console.WriteLine("=== Comparison Summary ===");
            Console.WriteLine($"Local Package: {nupkgFilePath}");
            Console.WriteLine($"Feed Package: {packageId} {version ?? "latest"}");
            Console.WriteLine($"  Identical: {identicalCount}");
            Console.WriteLine($"  Different: {differentCount}");
            Console.WriteLine($"  Errors: {errorCount}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error comparing packages: {ex.Message}");
        }
        finally
        {
            // Clean up temporary extraction directory
            if (tempExtractPath != null && Directory.Exists(tempExtractPath))
            {
                try
                {
                    Directory.Delete(tempExtractPath, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    static async Task ExtractNupkgFileAsync(string nupkgFilePath, string extractPath)
    {
        using var fileStream = File.OpenRead(nupkgFilePath);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var entryPath = Path.Combine(extractPath, entry.FullName);
            var entryDirectory = Path.GetDirectoryName(entryPath);

            if (!string.IsNullOrEmpty(entryDirectory))
            {
                Directory.CreateDirectory(entryDirectory);
            }

            using var entryStream = entry.Open();
            using var fileOutStream = File.Create(entryPath);
            await entryStream.CopyToAsync(fileOutStream);
        }
    }

    static List<PackageFileInfo> GetPackageContentsFromDirectory(string packagePath)
    {
        var files = Directory.GetFiles(packagePath, "*", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.FullName)
            .ToList();

        return files.Select(f =>
        {
            var relativePath = Path.GetRelativePath(packagePath, f.FullName);
            return new PackageFileInfo
            {
                Path = relativePath,
                FullPath = f.FullName,
                Size = f.Length
            };
        }).ToList();
    }

    private class PackageScorecardResult
    {
        public string PackageId { get; set; } = string.Empty;
        public string PackageVersion { get; set; } = string.Empty;
        public ScorecardResult? Scorecard { get; set; }
        public string? Error { get; set; }
    }
}
