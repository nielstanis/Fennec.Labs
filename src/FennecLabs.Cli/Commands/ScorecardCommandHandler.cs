using System.Text.Json;
using FennecLabs.Cli.Rendering;
using FennecLabs.Contracts;
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

        (Framework framework, List<PackageReference> packages)? resolved;
        try
        {
            resolved = await ResolvePackagesAsync(projectPath, outputMode);
        }
        catch (InvalidOperationException ex)
        {
            if (outputMode == OutputMode.Json)
                Console.Error.WriteLine(ex.Message);
            else
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

        if (resolved == null)
            return 0;

        var (framework, allPackages) = resolved.Value;
        var results = await FetchScorecardsAsync(allPackages, outputMode);

        var generatedAt = DateTime.Now;
        var envelope = ScorecardGraphNormalizer.Normalize(
            framework.FrameworkName,
            results.Select(r => new PackageScorecardLookup
            {
                PackageId = r.PackageId,
                PackageVersion = r.PackageVersion,
                Result = r.Scorecard,
                ErrorMessage = r.Error,
            }).ToList(),
            projectPath ?? ".",
            Environment.CurrentDirectory,
            ProducerVersion.Current,
            producedAt: generatedAt);
        var json = JsonSerializer.Serialize(envelope, ContractJsonOptions.Default);

        var projectName = projectPath != null
            ? Path.GetFileNameWithoutExtension(projectPath)
            : "project";
        var scoreDir = OutputCache.ScorecardDir(output, projectName,
            generatedAt.ToString("yyyy-MM-dd_HH-mm-ss"));
        await OutputCache.WriteAsync(Path.Combine(scoreDir, "result.json"), json);

        if (outputMode == OutputMode.Json)
        {
            Console.WriteLine(json);
            return 0;
        }

        AnsiConsole.WriteLine();
        ScorecardRenderer.Render(results);
        await WriteReportsAsync(projectPath, framework, results, generatedAt, reportFormat, scoreDir);
        return 0;
    }

    private static async Task<(Framework framework, List<PackageReference> packages)?> ResolvePackagesAsync(
        string? projectPath, OutputMode outputMode)
    {
        var packageList = projectPath != null
            ? await DotnetCliExecutor.GetPackageListAsync(projectPath)
            : await DotnetCliExecutor.GetPackageListAsync();

        if (packageList == null || packageList.Projects.Count == 0)
        {
            EmitEmpty(outputMode);
            return null;
        }

        var project = packageList.Projects[0];
        if (project.Frameworks.Count == 0)
        {
            EmitEmpty(outputMode);
            return null;
        }

        var framework = project.Frameworks[0];
        var allPackages = framework.TopLevelPackages.Concat(framework.TransitivePackages).ToList();

        if (allPackages.Count == 0)
        {
            EmitEmpty(outputMode);
            return null;
        }

        if (outputMode == OutputMode.Human)
        {
            AnsiConsole.MarkupLine(
                $"[dim]Found {framework.TopLevelPackages.Count} top-level and " +
                $"{framework.TransitivePackages.Count} transitive package(s)[/]");
            AnsiConsole.WriteLine();
        }

        return (framework, allPackages);
    }

    private async Task<List<PackageScorecardResult>> FetchScorecardsAsync(
        List<PackageReference> packages, OutputMode outputMode)
    {
        var results = new List<PackageScorecardResult>();
        foreach (var package in packages)
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
        return results;
    }

    private static async Task WriteReportsAsync(
        string? projectPath, Framework framework,
        List<PackageScorecardResult> results, DateTime generatedAt,
        string? reportFormat, string scoreDir)
    {
        if (string.IsNullOrEmpty(reportFormat))
            return;

        var report = BuildReport(projectPath, framework, results, generatedAt);
        foreach (var fmt in reportFormat.Split(',', StringSplitOptions.RemoveEmptyEntries))
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
