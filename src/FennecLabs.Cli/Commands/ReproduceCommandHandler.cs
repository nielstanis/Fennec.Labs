using System.Text.Json;
using System.Text.RegularExpressions;
using FennecLabs.Cli.Rendering;
using FennecLabs.NuGet;
using Spectre.Console;

namespace FennecLabs.Cli.Commands;

internal class ReproduceCommandHandler
{
    private readonly NuGetService _nugetService;

    private static readonly Regex TfmPattern =
        new(@"^net[\w.-]+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ReproduceCommandHandler(NuGetService nugetService)
    {
        _nugetService = nugetService;
    }

    public async Task<int> ExecuteAsync(
        string? nupkgFilePath, string? directoryPath, string? tfm,
        string packageId, string? version, OutputMode outputMode, string output, bool noCache)
    {
        return directoryPath != null
            ? await ExecuteDirectoryAsync(directoryPath, tfm, packageId, version, outputMode)
            : await ExecuteFileAsync(nupkgFilePath!, packageId, version, outputMode, output, noCache);
    }

    private async Task<int> ExecuteFileAsync(
        string nupkgFilePath, string packageId, string? version, OutputMode outputMode,
        string output, bool noCache)
    {
        if (!File.Exists(nupkgFilePath))
        {
            Console.Error.WriteLine($"File not found: {nupkgFilePath}");
            return 1;
        }

        if (!nupkgFilePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"File must be a .nupkg file: {nupkgFilePath}");
            return 1;
        }

        string? tempExtractPath = null;

        try
        {
            var localSha256 = await ComputeSha256Async(nupkgFilePath);
            var resolvedVersion = await _nugetService.ResolveVersionAsync(packageId, version);
            var cachePath = OutputCache.ReproducePath(output, packageId, resolvedVersion, localSha256);

            if (!noCache && OutputCache.Exists(cachePath))
            {
                var cached = OutputCache.TryLoad(cachePath)!;
                if (outputMode == OutputMode.Json)
                    Console.WriteLine(cached);
                else
                    DllPipeline.RenderCachedResult(cached, cachePath);
                return DllPipeline.ReproduceExitCodeFromJson(cached);
            }

            tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempExtractPath);

            await StatusRunner.RunAsync(
                outputMode, $"Extracting {Path.GetFileName(nupkgFilePath)}…",
                () => NupkgHelper.ExtractAsync(nupkgFilePath, tempExtractPath));

            var localDlls = NupkgHelper.GetDlls(tempExtractPath);

            await StatusRunner.RunAsync(
                outputMode, $"Downloading {packageId} {resolvedVersion} from feed…",
                () => _nugetService.DownloadPackageAsync(packageId, resolvedVersion));

            var feedDlls = (await _nugetService.GetPackageContentsAsync(packageId, resolvedVersion))
                .Where(DllPipeline.IsLibraryDll)
                .ToDictionary(f => f.Path, f => f);

            var matchingDlls = localDlls.Keys.Intersect(feedDlls.Keys).ToList();
            var onlyInLocal = localDlls.Keys.Except(feedDlls.Keys).ToList();
            var onlyInFeed = feedDlls.Keys.Except(localDlls.Keys).ToList();

            if (matchingDlls.Count == 0)
            {
                Console.Error.WriteLine("No matching DLL files found to compare.");
                return DllPipeline.ExitError;
            }

            var (dllResults, identicalCount, differentCount, errorCount) =
                DllPipeline.CompareMatchedDlls(matchingDlls, localDlls, feedDlls);
            var exitCode = DllPipeline.ReproduceExitCode(
                identicalCount, differentCount, errorCount, onlyInLocal.Count, onlyInFeed.Count);

            var reproduceResult = new
            {
                packageId,
                localSource = nupkgFilePath,
                localSha256,
                feedVersion = resolvedVersion,
                perDll = dllResults.Select(DllPipeline.FormatDllResult),
                onlyInLocal,
                onlyInFeed,
                summary = new { identical = identicalCount, different = differentCount, errors = errorCount },
            };
            var json = JsonSerializer.Serialize(reproduceResult, Json.Options);

            await OutputCache.WriteAsync(cachePath, json);

            if (outputMode == OutputMode.Json)
            {
                Console.WriteLine(json);
                return exitCode;
            }

            DiffRenderer.Render(
                $"{Path.GetFileName(nupkgFilePath)} vs {packageId} {resolvedVersion}",
                dllResults,
                onlyInLocal,
                onlyInFeed,
                "local",
                "feed");
            RenderVerdict(exitCode);

            return exitCode;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error comparing packages: {ex.Message}");
            return 1;
        }
        finally
        {
            if (tempExtractPath != null && Directory.Exists(tempExtractPath))
            {
                try { Directory.Delete(tempExtractPath, recursive: true); }
                catch { /* ignore cleanup errors */ }
            }
        }
    }

    private async Task<int> ExecuteDirectoryAsync(
        string directoryPath, string? tfm, string packageId, string? version, OutputMode outputMode)
    {
        if (!Directory.Exists(directoryPath))
        {
            Console.Error.WriteLine($"Directory not found: {directoryPath}");
            return 1;
        }

        var isInteractive = outputMode == OutputMode.Human && AnsiConsole.Profile.Capabilities.Interactive;
        var (resolvedDir, resolvedTfm, tfmError) = ResolveTfmDirectory(directoryPath, tfm, isInteractive);
        if (tfmError != null)
        {
            Console.Error.WriteLine(tfmError);
            return 1;
        }

        var rawLocalDlls = NupkgHelper.GetDlls(resolvedDir);
        var localByName = new Dictionary<string, PackageFileInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in rawLocalDlls)
            localByName[Path.GetFileName(kv.Key)] = kv.Value;

        var resolvedVersion = await _nugetService.ResolveVersionAsync(packageId, version);
        var allFeedContents = await StatusRunner.RunAsync(
            outputMode,
            $"Downloading {packageId} {resolvedVersion} from feed…",
            async () => (await _nugetService.GetPackageContentsAsync(packageId, resolvedVersion))
                .Where(DllPipeline.IsLibraryDll)
                .ToList());

        var feedByName = BuildFeedByName(allFeedContents, resolvedTfm);

        var matchingNames = localByName.Keys
            .Intersect(feedByName.Keys, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var onlyInLocal = localByName.Keys
            .Except(feedByName.Keys, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var onlyInFeed = feedByName.Keys
            .Except(localByName.Keys, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matchingNames.Count == 0)
        {
            Console.Error.WriteLine("No matching DLL files found to compare.");
            return DllPipeline.ExitError;
        }

        var (dllResults, identicalCount, differentCount, errorCount) =
            DllPipeline.CompareMatchedDlls(matchingNames, localByName, feedByName);
        // A build output directory also holds dependency DLLs that are not part of the package,
        // so only DLLs the package ships but the local build lacks count against reproducibility.
        var exitCode = DllPipeline.ReproduceExitCode(
            identicalCount, differentCount, errorCount, onlyInLocal: 0, onlyInFeed.Count);

        var reproduceResult = new
        {
            packageId,
            localSource = resolvedDir,
            resolvedTfm,
            feedVersion = resolvedVersion,
            perDll = dllResults.Select(DllPipeline.FormatDllResult),
            onlyInLocal,
            onlyInFeed,
            summary = new { identical = identicalCount, different = differentCount, errors = errorCount },
        };
        var json = JsonSerializer.Serialize(reproduceResult, Json.Options);

        if (outputMode == OutputMode.Json)
        {
            Console.WriteLine(json);
            return exitCode;
        }

        DiffRenderer.Render(
            $"{resolvedDir} vs {packageId} {resolvedVersion}",
            dllResults,
            onlyInLocal,
            onlyInFeed,
            "local",
            "feed");
        RenderVerdict(exitCode);

        return exitCode;
    }

    internal static (string resolvedDir, string? resolvedTfm, string? error) ResolveTfmDirectory(
        string directoryPath, string? tfmHint, bool isInteractive)
    {
        if (!string.IsNullOrWhiteSpace(tfmHint))
            return (directoryPath, tfmHint, null);

        var dirName = Path.GetFileName(
            directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (TfmPattern.IsMatch(dirName))
            return (directoryPath, dirName, null);

        var tfmSubdirs = Directory.GetDirectories(directoryPath)
            .Where(d => TfmPattern.IsMatch(Path.GetFileName(d)))
            .OrderBy(d => d)
            .ToList();

        if (tfmSubdirs.Count == 1)
        {
            var sub = tfmSubdirs[0];
            return (sub, Path.GetFileName(sub), null);
        }

        if (tfmSubdirs.Count > 1)
        {
            var names = tfmSubdirs.Select(d => Path.GetFileName(d)!).ToList();
            if (isInteractive)
            {
                var selected = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Select a target framework:")
                        .AddChoices(names));
                var selectedDir = tfmSubdirs.First(d =>
                    string.Equals(Path.GetFileName(d), selected, StringComparison.OrdinalIgnoreCase));
                return (selectedDir, selected, null);
            }

            return (directoryPath, null,
                $"Multiple target frameworks found: {string.Join(", ", names)}. Use --tfm to select one.");
        }

        return (directoryPath, null,
            $"Cannot determine target framework from directory '{directoryPath}'. " +
            "Use --tfm (e.g. --tfm net8.0) to specify one.");
    }

    private static Dictionary<string, PackageFileInfo> BuildFeedByName(
        IEnumerable<PackageFileInfo> allFeedDlls, string? tfm)
    {
        var source = tfm != null
            ? allFeedDlls.Where(f => f.Path.StartsWith($"lib/{tfm}/", StringComparison.OrdinalIgnoreCase))
            : allFeedDlls;

        var result = new Dictionary<string, PackageFileInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in source)
            result[Path.GetFileName(f.Path)] = f;
        return result;
    }

    private static void RenderVerdict(int exitCode)
    {
        AnsiConsole.MarkupLine(exitCode switch
        {
            DllPipeline.ExitReproducible => "[green]✓ Reproducible[/]",
            DllPipeline.ExitNotReproducible => "[red]✗ Not reproducible[/] [dim](exit code 2)[/]",
            _ => "[red]✗ Verification incomplete: some DLLs could not be compared[/] [dim](exit code 1)[/]",
        });
    }

    internal static async Task<string> ComputeSha256Async(string path)
    {
        await using var stream = File.OpenRead(path);
        var hash = await System.Security.Cryptography.SHA256.HashDataAsync(stream);
        return Convert.ToHexStringLower(hash);
    }
}
