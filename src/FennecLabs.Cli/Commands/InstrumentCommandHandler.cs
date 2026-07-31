using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FennecLabs.Cli.Commands.Taint;
using FennecLabs.Contracts;
using FennecLabs.Instrumentation;
using FennecLabs.Instrumentation.Output;
using FennecLabs.Instrumentation.Result;
using FennecLabs.NuGet;
using FennecLabs.TaintAnalysis;
using FennecLabs.TaintAnalysis.Models;
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
        OutputMode outputMode,
        TaintOptions? taintOptions = null)
    {
        taintOptions ??= TaintOptions.Disabled;

        if (!string.IsNullOrWhiteSpace(nuget))
            return await InstrumentNuGetPackageAsync(nuget, version, output, fileFormat, outputMode);
        return await InstrumentAssemblyAsync(filename!, output, fileFormat, outputMode, taintOptions);
    }

    private static async Task<int> InstrumentAssemblyAsync(
        string filename, string output, string fileFormat, OutputMode outputMode, TaintOptions taintOptions)
    {
        // When taint analysis is not requested, existing instrument behavior is completely
        // unchanged: filename must point directly at an assembly file.
        if (!taintOptions.Enabled)
        {
            if (!File.Exists(filename))
            {
                Console.Error.WriteLine($"Assembly file not found: {filename}");
                return 1;
            }

            return await InstrumentSingleAssemblyAsync(filename, output, fileFormat, outputMode);
        }

        IReadOnlyList<string> resolvedDlls;
        try
        {
            resolvedDlls = BuildGraphReader.Resolve(filename);
        }
        catch (BuildOutputNotFoundException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        TaintPolicy policy;
        try
        {
            policy = TaintPolicyLoader.Load(taintOptions.PolicyPath);
        }
        catch (TaintPolicyValidationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var exitCode = 0;
        foreach (var dllPath in resolvedDlls)
        {
            if (!File.Exists(dllPath))
            {
                Console.Error.WriteLine($"Assembly file not found: {dllPath}");
                exitCode = 1;
                continue;
            }

            var instrumentExitCode = await InstrumentSingleAssemblyAsync(dllPath, output, fileFormat, outputMode);
            if (instrumentExitCode != 0)
            {
                exitCode = instrumentExitCode;
                continue;
            }

            var taintExitCode = await WriteTaintArtifactAsync(
                dllPath, filename, output, outputMode, taintOptions, policy);
            if (taintExitCode != 0)
                exitCode = taintExitCode;
        }

        return exitCode;
    }

    private static async Task<int> InstrumentSingleAssemblyAsync(
        string filename, string output, string fileFormat, OutputMode outputMode)
    {
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

        var instrumentOutput = Path.Combine(output, "instrument");
        var writer = WriterFactory.CreateWriter(ParseFileFormat(fileFormat), instrumentOutput);
        await writer.WriteOutputAsync(result);
        AnsiConsole.MarkupLine($"[dim]Output written to[/] {Markup.Escape(instrumentOutput)}/");
        return 0;
    }

    private static async Task<int> WriteTaintArtifactAsync(
        string dllPath,
        string originalInputPath,
        string output,
        OutputMode outputMode,
        TaintOptions taintOptions,
        TaintPolicy policy)
    {
        var options = new TaintOptionsInfo
        {
            MaxDepth = taintOptions.MaxDepth,
            LlmHandoff = taintOptions.LlmHandoff,
            IncludeThirdParty = taintOptions.IncludeThirdParty,
            SecondPartyPrefixes = taintOptions.SecondPartyPrefixes,
        };

        var envelope = TaintArtifactBuilder.Build(
            policy,
            dllPath,
            Environment.CurrentDirectory,
            ProducerVersion.Current,
            options,
            projectPath: originalInputPath);

        var json = JsonSerializer.Serialize(envelope, ContractJsonOptions.Default);

        var scope = Path.GetFileNameWithoutExtension(dllPath);
        var runId = ComputeRunId(dllPath, policy, taintOptions);
        var taintDir = OutputCache.TaintDir(output, scope, runId);
        var resultPath = Path.Combine(taintDir, "result.json");
        await OutputCache.WriteAsync(resultPath, json);

        if (outputMode == OutputMode.Json)
            Console.WriteLine(json);
        else
            AnsiConsole.MarkupLine($"[dim]Taint artifact written to[/] {Markup.Escape(resultPath)}");

        return 0;
    }

    /// <summary>
    /// Deterministic cache key for a taint run, combining assembly identity, policy identity, and
    /// options. A placeholder ahead of the full sha256(assembly-identity + policy-version +
    /// options-fingerprint) scheme described in the taint analysis architecture.
    /// </summary>
    private static string ComputeRunId(string dllPath, TaintPolicy policy, TaintOptions taintOptions)
    {
        var fingerprint = string.Join(
            "|",
            dllPath,
            policy.PolicyId,
            policy.SchemaVersion,
            taintOptions.MaxDepth,
            taintOptions.LlmHandoff,
            taintOptions.IncludeThirdParty,
            string.Join(",", taintOptions.SecondPartyPrefixes));

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint));
        return Convert.ToHexStringLower(hash)[..12];
    }

    private async Task<int> InstrumentNuGetPackageAsync(
        string packageId, string? version, string output, string fileFormat, OutputMode outputMode)
    {
        try
        {
            var packagePath = await StatusRunner.RunAsync(
                outputMode, $"Downloading {packageId} {version ?? "latest"}…",
                () => _nugetService.DownloadPackageAsync(packageId, version));

            var contents = await _nugetService.GetPackageContentsAsync(packageId, version);
            var dllFiles = contents.Where(DllPipeline.IsLibraryDll).ToList();

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

            AnsiConsole.MarkupLine($"[dim]Found {dllFiles.Count} DLL file(s)[/]");

            var resolvedVersion = Path.GetFileName(packagePath);
            var packageOutput = Path.Combine(output, "instrument", packageId, resolvedVersion);
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
