using FennecLabs.Instrumentation;
using FennecLabs.Instrumentation.Output;
using FennecLabs.NuGet;

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
        string format)
    {
        if (!string.IsNullOrWhiteSpace(nuget))
            return await InstrumentNuGetPackageAsync(nuget, version, output, format);
        if (!string.IsNullOrWhiteSpace(filename))
            return await InstrumentAssemblyAsync(filename, output, format);

        Console.Error.WriteLine("Either --filename or --nuget must be specified.");
        return 1;
    }

    private static async Task<int> InstrumentAssemblyAsync(string filename, string output, string format)
    {
        if (!File.Exists(filename))
        {
            Console.Error.WriteLine($"Assembly file not found: {filename}");
            return 1;
        }

        Console.WriteLine($"Instrumenting assembly: {filename}");

        var analyzer = new AssemblyAnalyzer(filename);
        var result = analyzer.Analyze();

        if (result.HasError)
        {
            Console.Error.WriteLine($"Error analyzing assembly: {result.ExceptionOccurred?.Message}");
            return 1;
        }

        var writer = WriterFactory.CreateWriter(ParseFormat(format), output);
        await writer.WriteOutputAsync(result);

        Console.WriteLine($"Instrumentation complete. Output written to {output}/");
        return 0;
    }

    private async Task<int> InstrumentNuGetPackageAsync(
        string packageId,
        string? version,
        string output,
        string format)
    {
        Console.WriteLine($"Downloading NuGet package: {packageId} {version ?? "latest"}");

        try
        {
            var packagePath = await _nugetService.DownloadPackageAsync(packageId, version);
            Console.WriteLine($"Package downloaded to: {packagePath}");

            var contents = await _nugetService.GetPackageContentsAsync(packageId, version);
            var dllFiles = contents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !f.Path.Contains("_._"))
                .ToList();

            if (dllFiles.Count == 0)
            {
                Console.WriteLine("No DLL files found in the package.");
                return 0;
            }

            Console.WriteLine($"Found {dllFiles.Count} DLL file(s) in the package:");
            foreach (var dll in dllFiles)
                Console.WriteLine($"  - {dll.Path}");
            Console.WriteLine();

            var resolvedVersion = Path.GetFileName(packagePath);
            var packageOutput = Path.Combine(output, packageId, resolvedVersion);

            var writer = WriterFactory.CreateWriter(ParseFormat(format), packageOutput);
            int successCount = 0;
            int errorCount = 0;

            foreach (var dll in dllFiles)
            {
                Console.Write($"Instrumenting {dll.Path}... ");
                try
                {
                    var analyzer = new AssemblyAnalyzer(dll.FullPath);
                    var result = analyzer.Analyze();

                    if (result.HasError)
                    {
                        Console.Error.WriteLine($"Error: {result.ExceptionOccurred?.Message}");
                        errorCount++;
                    }
                    else
                    {
                        await writer.WriteOutputAsync(result, dll.Path);
                        Console.WriteLine("✓");
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error: {ex.Message}");
                    errorCount++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Instrumentation complete: {successCount} succeeded, {errorCount} failed.");
            Console.WriteLine($"Output written to {packageOutput}/");
            return errorCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error downloading or processing package: {ex.Message}");
            return 1;
        }
    }

    private static OutputFormat ParseFormat(string format) =>
        string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
            ? OutputFormat.Json
            : OutputFormat.Fxt;
}
