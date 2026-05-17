using System.Text.Json;
using FennecLabs.Cli.Rendering;
using FennecLabs.DotNetCli;
using FennecLabs.Scorecard;
using Spectre.Console;

namespace FennecLabs.Cli.Commands;

internal class ScorecardCommandHandler
{
    private readonly ScorecardClient _scorecardClient;

    public ScorecardCommandHandler(ScorecardClient scorecardClient)
    {
        _scorecardClient = scorecardClient;
    }

    public async Task<int> ExecuteAsync(
        string? projectPath, string? reportFormat, OutputMode outputMode, string output)
    {
        if (outputMode == OutputMode.Human)
        {
            AnsiConsole.MarkupLine(
                $"[dim]Analyzing {Markup.Escape(projectPath ?? "all projects")}…[/]");
            AnsiConsole.WriteLine();
        }

        var packageList = projectPath != null
            ? await DotnetCliExecutor.GetPackageListAsync(projectPath)
            : await DotnetCliExecutor.GetPackageListAsync();

        if (packageList == null || packageList.Projects.Count == 0)
        {
            EmitEmpty(outputMode);
            return 0;
        }

        var project = packageList.Projects[0];
        if (project.Frameworks.Count == 0)
        {
            EmitEmpty(outputMode);
            return 0;
        }

        var framework = project.Frameworks[0];
        var allPackages = framework.TopLevelPackages.Concat(framework.TransitivePackages).ToList();

        if (allPackages.Count == 0)
        {
            EmitEmpty(outputMode);
            return 0;
        }

        if (outputMode == OutputMode.Human)
        {
            AnsiConsole.MarkupLine(
                $"[dim]Found {framework.TopLevelPackages.Count} top-level and " +
                $"{framework.TransitivePackages.Count} transitive package(s)[/]");
            AnsiConsole.WriteLine();
        }

        var results = new List<PackageScorecardResult>();

        foreach (var package in allPackages)
        {
            if (outputMode == OutputMode.Human)
                AnsiConsole.Markup(
                    $"  [dim]Fetching {Markup.Escape(package.Id)} " +
                    $"{Markup.Escape(package.ResolvedVersion ?? "")}…[/] ");
            try
            {
                var sc = await _scorecardClient.GetScorecardResultFromPackageAsync(
                    package.Id, package.ResolvedVersion);
                results.Add(new PackageScorecardResult
                {
                    PackageId = package.Id,
                    PackageVersion = package.ResolvedVersion ?? "unknown",
                    Scorecard = sc,
                });
                if (outputMode == OutputMode.Human)
                {
                    if (sc != null)
                        AnsiConsole.MarkupLine($"[{ColorTheme.ForScore(sc.Score)}]{sc.Score:F1}/10[/]");
                    else
                        AnsiConsole.MarkupLine("[grey]no scorecard[/]");
                }
            }
            catch (Exception ex)
            {
                results.Add(new PackageScorecardResult
                {
                    PackageId = package.Id,
                    PackageVersion = package.ResolvedVersion ?? "unknown",
                    Error = ex.Message,
                });
                if (outputMode == OutputMode.Human)
                    AnsiConsole.MarkupLine("[red]error[/]");
                else
                    Console.Error.WriteLine($"Error fetching {package.Id}: {ex.Message}");
            }
        }

        var generatedAt = DateTime.Now;
        var scorecardOutput = new
        {
            project = projectPath ?? ".",
            framework = framework.FrameworkName,
            generatedAt,
            dependencyTree = new
            {
                topLevel = framework.TopLevelPackages.Select(p => new
                {
                    id = p.Id,
                    requestedVersion = p.RequestedVersion,
                    resolvedVersion = p.ResolvedVersion,
                }),
                transitive = framework.TransitivePackages.Select(p => new
                {
                    id = p.Id,
                    requestedVersion = p.RequestedVersion,
                    resolvedVersion = p.ResolvedVersion,
                }),
            },
            packages = results.Select(r => new
            {
                packageId = r.PackageId,
                packageVersion = r.PackageVersion,
                score = r.Scorecard?.Score,
                repoName = r.Scorecard?.Repo.Name,
                scorecardDate = r.Scorecard?.Date,
                scorecardVersion = r.Scorecard?.Scorecard.Version,
                checks = r.Scorecard?.Checks.Select(c => new
                {
                    name = c.Name,
                    score = c.Score,
                    reason = c.Reason,
                }),
                error = r.Error,
            }),
        };
        var json = JsonSerializer.Serialize(scorecardOutput, Json.Options);

        var projectName = projectPath != null
            ? Path.GetFileNameWithoutExtension(projectPath)
            : "project";
        var timestamp = generatedAt.ToString("yyyy-MM-dd_HH-mm-ss");
        var scoreDir = OutputCache.ScorecardDir(output, projectName, timestamp);
        await OutputCache.WriteAsync(Path.Combine(scoreDir, "result.json"), json);

        if (outputMode == OutputMode.Json)
        {
            Console.WriteLine(json);
            return 0;
        }

        AnsiConsole.WriteLine();
        ScorecardRenderer.Render(results);

        if (!string.IsNullOrEmpty(reportFormat))
        {
            var report = BuildReport(projectPath, framework, results, generatedAt);
            var formats = reportFormat.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var fmt in formats)
            {
                var normalized = fmt.Trim().ToLowerInvariant();
                if (normalized == "html")
                {
                    var path = Path.Combine(scoreDir, "report.html");
                    await File.WriteAllTextAsync(path, ScorecardReportBuilder.BuildHtml(report));
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]HTML report:[/] {Markup.Escape(path)}");
                }
                else if (normalized == "md")
                {
                    var path = Path.Combine(scoreDir, "report.md");
                    await File.WriteAllTextAsync(path, ScorecardReportBuilder.BuildMarkdown(report));
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[dim]Markdown report:[/] {Markup.Escape(path)}");
                }
                else
                {
                    Console.Error.WriteLine($"Unknown report format '{fmt}'. Valid values: html, md");
                }
            }
        }

        return 0;
    }

    private static ScorecardReport BuildReport(
        string? projectPath,
        Framework framework,
        List<PackageScorecardResult> results,
        DateTime generatedAt) =>
        new(
            Project: projectPath ?? ".",
            Framework: framework.FrameworkName,
            GeneratedAt: generatedAt,
            DependencyTree: new ScorecardDependencyTree(
                TopLevel: framework.TopLevelPackages
                    .Select(p => new ScorecardPackageRef(p.Id, p.RequestedVersion, p.ResolvedVersion))
                    .ToList(),
                Transitive: framework.TransitivePackages
                    .Select(p => new ScorecardPackageRef(p.Id, p.RequestedVersion, p.ResolvedVersion))
                    .ToList()),
            Packages: results.Select(r => new ScorecardReportPackage(
                PackageId: r.PackageId,
                PackageVersion: r.PackageVersion,
                Score: r.Scorecard?.Score,
                Checks: r.Scorecard?.Checks
                    .Select(c => new ScorecardReportCheck(c.Name, c.Score, c.Reason))
                    .ToList() ?? [],
                Error: r.Error,
                RepoName: r.Scorecard?.Repo.Name,
                ScorecardDate: r.Scorecard?.Date,
                ScorecardVersion: r.Scorecard?.Scorecard.Version))
                .ToList());

    private static void EmitEmpty(OutputMode outputMode)
    {
        if (outputMode == OutputMode.Json)
            Console.WriteLine(JsonSerializer.Serialize(new { packages = Array.Empty<object>() }, Json.Options));
        else
            AnsiConsole.MarkupLine("[yellow]No packages found in the project.[/]");
    }
}
