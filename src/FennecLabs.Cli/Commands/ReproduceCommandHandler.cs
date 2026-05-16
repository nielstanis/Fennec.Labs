using System.IO.Compression;
using System.Text.Json;
using FennecLabs.AssemblyDiff;
using FennecLabs.Cli.Rendering;
using FennecLabs.NuGet;
using Mono.Cecil;
using Spectre.Console;

namespace FennecLabs.Cli.Commands;

internal class ReproduceCommandHandler
{
    private readonly NuGetService _nugetService;

    public ReproduceCommandHandler(NuGetService nugetService)
    {
        _nugetService = nugetService;
    }

    public async Task<int> ExecuteAsync(
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
                        await ExtractNupkgFileAsync(nupkgFilePath, tempExtractPath);
                    });
            }
            else
            {
                await ExtractNupkgFileAsync(nupkgFilePath, tempExtractPath);
            }

            var localDlls = GetPackageContentsFromDirectory(tempExtractPath)
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

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

            var dllResults = new List<DllDiffResult>();
            int identicalCount = 0;
            int differentCount = 0;
            int errorCount = 0;

            foreach (var dllPath in matchingDlls)
            {
                try
                {
                    using var assembly1 = AssemblyDefinition.ReadAssembly(localDlls[dllPath].FullPath);
                    using var assembly2 = AssemblyDefinition.ReadAssembly(feedDlls[dllPath].FullPath);
                    var result = new AssemblyComparer(assembly1, assembly2).Compare();

                    dllResults.Add(new DllDiffResult(dllPath, result, null));
                    if (result.AreEqual) identicalCount++;
                    else differentCount++;
                }
                catch (Exception ex)
                {
                    dllResults.Add(new DllDiffResult(dllPath, null, ex.Message));
                    errorCount++;
                }
            }

            var reproduceResult = new
            {
                packageId,
                localFile = nupkgFilePath,
                feedVersion = version ?? "latest",
                perDll = dllResults.Select(d => new
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
                }),
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

    private static async Task ExtractNupkgFileAsync(string nupkgFilePath, string extractPath)
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
                Directory.CreateDirectory(entryDirectory);

            using var entryStream = entry.Open();
            using var fileOutStream = File.Create(entryPath);
            await entryStream.CopyToAsync(fileOutStream);
        }
    }

    private static List<PackageFileInfo> GetPackageContentsFromDirectory(string packagePath)
    {
        return Directory.GetFiles(packagePath, "*", SearchOption.AllDirectories)
            .Select(f => new FileInfo(f))
            .OrderBy(f => f.FullName)
            .Select(f => new PackageFileInfo
            {
                Path = Path.GetRelativePath(packagePath, f.FullName),
                FullPath = f.FullName,
                Size = f.Length,
            })
            .ToList();
    }
}
