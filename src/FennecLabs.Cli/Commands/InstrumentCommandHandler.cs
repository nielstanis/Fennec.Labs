using System.Text.Json;
using FennecLabs.Instrumentation;
using FennecLabs.Instrumentation.Output;
using FennecLabs.Instrumentation.Result;
using FennecLabs.NuGet;
using Spectre.Console;

namespace FennecLabs.Cli.Commands;

internal class InstrumentCommandHandler
{
    private readonly NuGetService _nugetService;

    public InstrumentCommandHandler(NuGetService nugetService)
    {
        _nugetService = nugetService;
    }

    public async Task<int> ExecuteAsync(
        string? filename,
        string? nuget,
        string? version,
        string output,
        string fileFormat,
        OutputMode outputMode)
    {
        if (!string.IsNullOrWhiteSpace(nuget))
            return await InstrumentNuGetPackageAsync(nuget, version, output, fileFormat, outputMode);
        if (!string.IsNullOrWhiteSpace(filename))
            return await InstrumentAssemblyAsync(filename, output, fileFormat, outputMode);

        Console.Error.WriteLine("Either --filename or --nuget must be specified.");
        return 1;
    }

    private static async Task<int> InstrumentAssemblyAsync(
        string filename, string output, string fileFormat, OutputMode outputMode)
    {
        if (!File.Exists(filename))
        {
            Console.Error.WriteLine($"Assembly file not found: {filename}");
            return 1;
        }

        if (outputMode == OutputMode.Human)
            AnsiConsole.MarkupLine($"[dim]Instrumenting {Markup.Escape(filename)}…[/]");

        var analyzer = new AssemblyAnalyzer(filename);
        var result = analyzer.Analyze();

        if (result.HasError)
        {
            Console.Error.WriteLine($"Error analyzing assembly: {result.ExceptionOccurred?.Message}");
            return 1;
        }

        if (outputMode == OutputMode.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(FlattenInvocations(result), Json.Options));
            return 0;
        }

        var writer = WriterFactory.CreateWriter(ParseFileFormat(fileFormat), output);
        await writer.WriteOutputAsync(result);
        AnsiConsole.MarkupLine($"[dim]Output written to[/] {Markup.Escape(output)}/");
        return 0;
    }

    private async Task<int> InstrumentNuGetPackageAsync(
        string packageId, string? version, string output, string fileFormat, OutputMode outputMode)
    {
        try
        {
            string packagePath = string.Empty;
            if (outputMode == OutputMode.Human)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("grey"))
                    .StartAsync($"Downloading {packageId} {version ?? "latest"}…", async _ =>
                    {
                        packagePath = await _nugetService.DownloadPackageAsync(packageId, version);
                    });
            }
            else
            {
                packagePath = await _nugetService.DownloadPackageAsync(packageId, version);
            }

            var contents = await _nugetService.GetPackageContentsAsync(packageId, version);
            var dllFiles = contents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !f.Path.Contains("_._"))
                .ToList();

            if (dllFiles.Count == 0)
            {
                Console.Error.WriteLine("No DLL files found in the package.");
                return 0;
            }

            if (outputMode == OutputMode.Json)
            {
                var dllOutputs = new List<object>();
                foreach (var dll in dllFiles)
                {
                    var analyzer = new AssemblyAnalyzer(dll.FullPath);
                    var result = analyzer.Analyze();
                    if (result.HasError)
                    {
                        Console.Error.WriteLine($"Error analyzing {dll.Path}: {result.ExceptionOccurred?.Message}");
                        continue;
                    }
                    dllOutputs.Add(new
                    {
                        dllPath = dll.Path,
                        invocations = FlattenInvocations(result),
                    });
                }
                Console.WriteLine(JsonSerializer.Serialize(dllOutputs, Json.Options));
                return 0;
            }

            if (outputMode == OutputMode.Human)
            {
                AnsiConsole.MarkupLine(
                    $"[dim]Found {dllFiles.Count} DLL file(s)[/]");
            }

            var resolvedVersion = Path.GetFileName(packagePath);
            var packageOutput = Path.Combine(output, packageId, resolvedVersion);
            var writer = WriterFactory.CreateWriter(ParseFileFormat(fileFormat), packageOutput);
            int successCount = 0;
            int errorCount = 0;

            foreach (var dll in dllFiles)
            {
                if (outputMode == OutputMode.Human)
                    AnsiConsole.Markup($"  [dim]{Markup.Escape(dll.Path)}[/]… ");
                try
                {
                    var analyzer = new AssemblyAnalyzer(dll.FullPath);
                    var result = analyzer.Analyze();

                    if (result.HasError)
                    {
                        Console.Error.WriteLine($"Error: {result.ExceptionOccurred?.Message}");
                        errorCount++;
                        if (outputMode == OutputMode.Human)
                            AnsiConsole.MarkupLine("[red]✗[/]");
                    }
                    else
                    {
                        await writer.WriteOutputAsync(result, dll.Path);
                        successCount++;
                        if (outputMode == OutputMode.Human)
                            AnsiConsole.MarkupLine("[green]✓[/]");
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    errorCount++;
                    if (outputMode == OutputMode.Human)
                        AnsiConsole.MarkupLine("[red]✗[/]");
                }
            }

            if (outputMode == OutputMode.Human)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(
                    $"[dim]Complete:[/] [green]{successCount} succeeded[/], [red]{errorCount} failed[/]");
                AnsiConsole.MarkupLine($"[dim]Output written to[/] {Markup.Escape(packageOutput)}/");
            }

            return errorCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error downloading or processing package: {ex.Message}");
            return 1;
        }
    }

    private static IEnumerable<object> FlattenInvocations(AssemblyResult result) =>
        result.Types.SelectMany(t =>
            t.Methods.SelectMany(m =>
                m.Invocations.Select(i => new
                {
                    type = t.ClassType,
                    method = m.Name,
                    parameters = m.Parameters,
                    invocation = i.Invocation,
                    returnType = i.ReturnType,
                    sequence = i.Sequence,
                })));

    private static OutputFormat ParseFileFormat(string format) =>
        string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
            ? OutputFormat.Json
            : OutputFormat.Fxt;
}
