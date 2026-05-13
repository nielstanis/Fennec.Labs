using System.IO.Compression;
using FennecLabs.AssemblyDiff;
using FennecLabs.NuGet;
using Mono.Cecil;

namespace FennecLabs.Cli.Commands;

internal class ReproduceCommandHandler
{
    private readonly NuGetService _nugetService;

    public ReproduceCommandHandler(NuGetService nugetService)
    {
        _nugetService = nugetService;
    }

    public async Task<int> ExecuteAsync(string nupkgFilePath, string packageId, string? version)
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

        Console.WriteLine($"Comparing local package file: {nupkgFilePath}");
        Console.WriteLine($"With NuGet package: {packageId} {version ?? "latest"}");
        Console.WriteLine();

        string? tempExtractPath = null;

        try
        {
            tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempExtractPath);

            Console.WriteLine($"Extracting local package to: {tempExtractPath}");
            await ExtractNupkgFileAsync(nupkgFilePath, tempExtractPath);
            Console.WriteLine("Extraction complete.");
            Console.WriteLine();

            var localContents = GetPackageContentsFromDirectory(tempExtractPath);
            var localDlls = localContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            Console.WriteLine($"Downloading NuGet package: {packageId} {version ?? "latest"}");
            var feedPackagePath = await _nugetService.DownloadPackageAsync(packageId, version);
            Console.WriteLine($"Downloaded to: {feedPackagePath}");
            Console.WriteLine();

            var feedContents = await _nugetService.GetPackageContentsAsync(packageId, version);
            var feedDlls = feedContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            var matchingDlls = localDlls.Keys.Intersect(feedDlls.Keys).ToList();
            var onlyInLocal = localDlls.Keys.Except(feedDlls.Keys).ToList();
            var onlyInFeed = feedDlls.Keys.Except(localDlls.Keys).ToList();

            Console.WriteLine($"Found {matchingDlls.Count} matching DLL file(s) to compare");
            if (onlyInLocal.Count > 0)
                Console.WriteLine($"  {onlyInLocal.Count} DLL(s) only in local package");
            if (onlyInFeed.Count > 0)
                Console.WriteLine($"  {onlyInFeed.Count} DLL(s) only in feed package");
            Console.WriteLine();

            if (matchingDlls.Count == 0)
            {
                Console.WriteLine("No matching DLL files found to compare.");
                return 0;
            }

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
                        Console.WriteLine("  ✓ Identical");
                        identicalCount++;
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ Differences found ({result.Differences.Count} difference(s))");
                        differentCount++;

                        if (result.TypesOnlyInAssembly1.Count > 0)
                            Console.WriteLine(
                                $"    - {result.TypesOnlyInAssembly1.Count} type(s) only in local package");
                        if (result.TypesOnlyInAssembly2.Count > 0)
                            Console.WriteLine(
                                $"    - {result.TypesOnlyInAssembly2.Count} type(s) only in feed package");
                        if (result.MethodBodyDifferences.Count > 0)
                            Console.WriteLine(
                                $"    - {result.MethodBodyDifferences.Count} method(s) with body differences");

                        Console.WriteLine();
                        Console.WriteLine("  Detailed Report:");
                        var report = result.GenerateReport();
                        foreach (var line in report.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                                Console.WriteLine($"    {line}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  ✗ Error: {ex.Message}");
                    errorCount++;
                }
                Console.WriteLine();
            }

            Console.WriteLine("=== Comparison Summary ===");
            Console.WriteLine($"Local Package: {nupkgFilePath}");
            Console.WriteLine($"Feed Package: {packageId} {version ?? "latest"}");
            Console.WriteLine($"  Identical: {identicalCount}");
            Console.WriteLine($"  Different: {differentCount}");
            Console.WriteLine($"  Errors: {errorCount}");
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
}
