using System.Text.Json;
using System.Text.RegularExpressions;
using FennecLabs.AssemblyDiff;
using FennecLabs.Cli.Rendering;
using FennecLabs.NuGet;
using Mono.Cecil;
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

        if (!noCache && version != null)
        {
            var cachePath = OutputCache.ReproducePath(output, packageId, version);
            if (OutputCache.Exists(cachePath))
            {
                var cached = OutputCache.TryLoad(cachePath)!;
                if (outputMode == OutputMode.Json)
                    Console.WriteLine(cached);
                else
                    RenderCachedResult(cached, cachePath);
                return 0;
            }
        }

        string? tempExtractPath = null;

        try
        {
            tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempExtractPath);

            if (outputMode == OutputMode.Human)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("grey"))
                    .StartAsync($"Extracting {Path.GetFileName(nupkgFilePath)}…", async _ =>
                    {
                        await NupkgHelper.ExtractAsync(nupkgFilePath, tempExtractPath);
                    });
            }
            else
            {
                await NupkgHelper.ExtractAsync(nupkgFilePath, tempExtractPath);
            }

            var localDlls = NupkgHelper.GetDlls(tempExtractPath);

            string feedPackagePath = string.Empty;
            if (outputMode == OutputMode.Human)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("grey"))
                    .StartAsync($"Downloading {packageId} {version ?? "latest"} from feed…", async _ =>
                    {
                        feedPackagePath = await _nugetService.DownloadPackageAsync(packageId, version);
                    });
            }
            else
            {
                feedPackagePath = await _nugetService.DownloadPackageAsync(packageId, version);
            }

            var feedDlls = (await _nugetService.GetPackageContentsAsync(packageId, version))
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            var matchingDlls = localDlls.Keys.Intersect(feedDlls.Keys).ToList();
            var onlyInLocal = localDlls.Keys.Except(feedDlls.Keys).ToList();
            var onlyInFeed = feedDlls.Keys.Except(localDlls.Keys).ToList();

            if (matchingDlls.Count == 0)
            {
                Console.Error.WriteLine("No matching DLL files found to compare.");
                return 0;
            }

            var (dllResults, identicalCount, differentCount, errorCount) =
                CompareMatchedDlls(matchingDlls, localDlls, feedDlls);

            var reproduceResult = new
            {
                packageId,
                localSource = nupkgFilePath,
                feedVersion = version ?? "latest",
                perDll = dllResults.Select(FormatDllResult),
                onlyInLocal,
                onlyInFeed,
                summary = new { identical = identicalCount, different = differentCount, errors = errorCount },
            };
            var json = JsonSerializer.Serialize(reproduceResult, Json.Options);

            if (version != null)
                await OutputCache.WriteAsync(OutputCache.ReproducePath(output, packageId, version), json);

            if (outputMode == OutputMode.Json)
            {
                Console.WriteLine(json);
                return errorCount > 0 ? 1 : 0;
            }

            DiffRenderer.Render(
                $"{Path.GetFileName(nupkgFilePath)} vs {packageId} {version ?? "latest"}",
                dllResults,
                onlyInLocal,
                onlyInFeed,
                "local",
                "feed");

            return errorCount > 0 ? 1 : 0;
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

        List<PackageFileInfo> allFeedContents;
        if (outputMode == OutputMode.Human)
        {
            allFeedContents = [];
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("grey"))
                .StartAsync($"Downloading {packageId} {version ?? "latest"} from feed…", async _ =>
                {
                    allFeedContents = (await _nugetService.GetPackageContentsAsync(packageId, version))
                        .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                                    && !f.Path.Contains("_._"))
                        .ToList();
                });
        }
        else
        {
            allFeedContents = (await _nugetService.GetPackageContentsAsync(packageId, version))
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                            && !f.Path.Contains("_._"))
                .ToList();
        }

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
            return 0;
        }

        var (dllResults, identicalCount, differentCount, errorCount) =
            CompareMatchedDlls(matchingNames, localByName, feedByName);

        var reproduceResult = new
        {
            packageId,
            localSource = resolvedDir,
            resolvedTfm,
            feedVersion = version ?? "latest",
            perDll = dllResults.Select(FormatDllResult),
            onlyInLocal,
            onlyInFeed,
            summary = new { identical = identicalCount, different = differentCount, errors = errorCount },
        };
        var json = JsonSerializer.Serialize(reproduceResult, Json.Options);

        if (outputMode == OutputMode.Json)
        {
            Console.WriteLine(json);
            return errorCount > 0 ? 1 : 0;
        }

        DiffRenderer.Render(
            $"{resolvedDir} vs {packageId} {version ?? "latest"}",
            dllResults,
            onlyInLocal,
            onlyInFeed,
            "local",
            "feed");

        return errorCount > 0 ? 1 : 0;
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

    private static (List<DllDiffResult> results, int identical, int different, int errors)
        CompareMatchedDlls(
            IEnumerable<string> matchingNames,
            Dictionary<string, PackageFileInfo> localByKey,
            Dictionary<string, PackageFileInfo> feedByKey)
    {
        var results = new List<DllDiffResult>();
        int identical = 0, different = 0, errors = 0;

        foreach (var name in matchingNames)
        {
            try
            {
                using var local = AssemblyDefinition.ReadAssembly(localByKey[name].FullPath);
                using var feed = AssemblyDefinition.ReadAssembly(feedByKey[name].FullPath);
                var result = new AssemblyComparer(local, feed).Compare();
                results.Add(new DllDiffResult(name, result, null));
                if (result.AreEqual) identical++;
                else different++;
            }
            catch (Exception ex)
            {
                results.Add(new DllDiffResult(name, null, ex.Message));
                errors++;
            }
        }

        return (results, identical, different, errors);
    }

    private static object FormatDllResult(DllDiffResult d) => new
    {
        dllPath = d.DllPath,
        areEqual = d.Result?.AreEqual,
        events = d.Result?.Events.Select(e => new
        {
            type = e.GetType().Name,
            message = e.FormatMessage(),
        }),
        typesAdded = d.Result?.TypesOnlyInAssembly2.ToList(),
        typesRemoved = d.Result?.TypesOnlyInAssembly1.ToList(),
        methodBodyChanges = d.Result?.MethodBodyChanges.Select(m => new
        {
            typeName = m.TypeName,
            signature = m.Signature,
            instructionDiffs = m.Changes.Select(c => new
            {
                c.Index, c.Instruction1, c.Instruction2,
            }),
            instructions1 = m.Instructions1,
            instructions2 = m.Instructions2,
        }),
        error = d.Error,
    };

    private static void RenderCachedResult(string json, string cachePath)
    {
        AnsiConsole.MarkupLine($"[dim](cached)[/] {Markup.Escape(cachePath)}");
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("summary", out var summary))
            {
                var identical = summary.GetProperty("identical").GetInt32();
                var different = summary.GetProperty("different").GetInt32();
                var errors = summary.GetProperty("errors").GetInt32();
                AnsiConsole.MarkupLine(
                    $"[dim]Summary: [green]{identical} identical[/] · " +
                    $"[red]{different} different[/] · [red]{errors} error(s)[/][/]");
            }
        }
        catch (System.Text.Json.JsonException) { }
        AnsiConsole.MarkupLine("[dim]Use --no-cache to force a fresh run.[/]");
    }
}
