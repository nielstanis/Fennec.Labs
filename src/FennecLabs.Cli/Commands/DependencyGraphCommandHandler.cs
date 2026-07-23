using System.Text.Json;
using FennecLabs.Contracts;
using FennecLabs.DotNetCli;
using Spectre.Console;

namespace FennecLabs.Cli.Commands;

internal class DependencyGraphCommandHandler
{
    private readonly Func<string?, Task<PackageListResult?>> _packageListResolver;

    public DependencyGraphCommandHandler()
        : this(ResolvePackageListAsync)
    {
    }

    internal DependencyGraphCommandHandler(Func<string?, Task<PackageListResult?>> packageListResolver)
    {
        _packageListResolver = packageListResolver
            ?? throw new ArgumentNullException(nameof(packageListResolver));
    }

    public async Task<int> ExecuteAsync(string? projectPath, OutputMode outputMode, string output)
    {
        if (outputMode == OutputMode.Human)
        {
            AnsiConsole.MarkupLine(
                $"[dim]Analyzing {Markup.Escape(projectPath ?? "all projects")}…[/]");
            AnsiConsole.WriteLine();
        }

        PackageListResult? packageList;
        try
        {
            packageList = await _packageListResolver(projectPath);
        }
        catch (InvalidOperationException ex)
        {
            if (outputMode == OutputMode.Json)
                Console.Error.WriteLine(ex.Message);
            else
                AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return 1;
        }

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
        var resolvedProjectPath = projectPath ?? project.Path;
        var envelope = DependencyGraphNormalizer.Normalize(
            framework,
            resolvedProjectPath,
            Environment.CurrentDirectory,
            ProducerVersion.Current);

        if (outputMode == OutputMode.Human)
        {
            AnsiConsole.MarkupLine(
                $"[dim]Found {envelope.Payload.Nodes.Count} package(s) for {Markup.Escape(framework.FrameworkName)}[/]");
            AnsiConsole.WriteLine();
        }

        var json = JsonSerializer.Serialize(envelope, ContractJsonOptions.Default);

        var projectName = Path.GetFileNameWithoutExtension(resolvedProjectPath);
        var timestamp = envelope.ProducedAt.ToString("yyyy-MM-dd_HH-mm-ss");
        var dependenciesDir = OutputCache.DependenciesDir(output, projectName, timestamp);
        await OutputCache.WriteAsync(Path.Combine(dependenciesDir, "result.json"), json);

        if (outputMode == OutputMode.Json)
        {
            Console.WriteLine(json);
            return 0;
        }

        AnsiConsole.WriteLine();
        foreach (var node in envelope.Payload.Nodes)
        {
            var kind = node.IsTopLevel ? "top-level" : "transitive";
            AnsiConsole.MarkupLine(
                $"  [dim]{Markup.Escape(kind)}[/] {Markup.Escape(node.Id)} {Markup.Escape(node.ResolvedVersion)}");
        }
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Artifact written to:[/] {Markup.Escape(Path.Combine(dependenciesDir, "result.json"))}");

        return 0;
    }

    private static void EmitEmpty(OutputMode outputMode)
    {
        if (outputMode == OutputMode.Json)
            Console.WriteLine(JsonSerializer.Serialize(new { nodes = Array.Empty<object>() }, Json.Options));
        else
            AnsiConsole.MarkupLine("[yellow]No packages found in the project.[/]");
    }

    private static Task<PackageListResult?> ResolvePackageListAsync(string? projectPath) =>
        projectPath != null
            ? DotnetCliExecutor.GetPackageListAsync(projectPath)
            : DotnetCliExecutor.GetPackageListAsync();
}
