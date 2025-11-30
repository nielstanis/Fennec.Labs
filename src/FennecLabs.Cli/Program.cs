using System.CommandLine;
using System.Diagnostics;
using System.IO.Compression;
using FennecLabs.AssemblyDiff;
using FennecLabs.DotNetCli;
using FennecLabs.Instrumentation;
using FennecLabs.Instrumentation.Output;
using FennecLabs.NuGet;
using FennecLabs.Scorecard;
using Mono.Cecil;
using NuGet.Versioning;

namespace FennecLabs;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Fennec Labs CLI");

        var filenameOption = new Option<string>(
            "--filename",
            "-f"
        )
        {
            Description = "Path to the assembly file to instrument"
        };

        var nugetOption = new Option<string>(
            "--nuget",
            "-n"
        )
        {
            Description = "NuGet package ID to download and instrument"
        };

        var versionOption = new Option<string>(
            "--version",
            "-v"
        )
        {
            Description = "Version of the NuGet package (optional, uses latest if not specified)"
        };

        var instrumentCommand = new Command("instrument", "Instrument assembly files or NuGet packages");
        instrumentCommand.Options.Add(filenameOption);
        instrumentCommand.Options.Add(nugetOption);
        instrumentCommand.Options.Add(versionOption);
        instrumentCommand.SetAction(async (ParseResult parseResult) =>
        {
            var filename = parseResult.GetValue(filenameOption);
            var nuget = parseResult.GetValue(nugetOption);
            var version = parseResult.GetValue(versionOption);

            if (!string.IsNullOrWhiteSpace(nuget))
            {
                await InstrumentNuGetPackageAsync(nuget, version);
            }
            else if (!string.IsNullOrWhiteSpace(filename))
            {
                await InstrumentAssemblyAsync(filename);
            }
            else
            {
                Console.Error.WriteLine("Either --filename or --nuget must be specified.");
            }
        });

        var projectPathOption = new Option<string>(
            "--project",
            "-p"
        )
        {
            Description = "Path to the .csproj file"
        };

        var scorecardCommand = new Command("scorecard", "Get security scorecards for packages in a project");
        scorecardCommand.Options.Add(projectPathOption);
        scorecardCommand.SetAction(async (ParseResult parseResult) =>
        {
            var projectPath = parseResult.GetValue(projectPathOption);
            await GetScorecardsForProjectAsync(projectPath);
        });

        var compareNugetOption = new Option<string>(
            "--nuget",
            "-n"
        )
        {
            Description = "NuGet package ID to compare"
        };

        var compareVersionOption = new Option<string>(
            "--version",
            "-v"
        )
        {
            Description = "Version of the NuGet package to compare (optional, uses latest if not specified)"
        };

        var compareCommand = new Command("compare", "Compare assemblies between two versions of a NuGet package");
        compareCommand.Options.Add(compareNugetOption);
        compareCommand.Options.Add(compareVersionOption);
        compareCommand.SetAction(async (ParseResult parseResult) =>
        {
            var nuget = parseResult.GetValue(compareNugetOption);
            var version = parseResult.GetValue(compareVersionOption);

            if (string.IsNullOrWhiteSpace(nuget))
            {
                Console.Error.WriteLine("--nuget is required.");
                return;
            }

            await CompareNuGetPackageAsync(nuget, version);
        });

        var reproduceFilenameOption = new Option<string>(
            "--filename",
            "-f"
        )
        {
            Description = "Path to the .nupkg file to compare"
        };

        var reproduceNugetOption = new Option<string>(
            "--nuget",
            "-n"
        )
        {
            Description = "NuGet package ID to compare against"
        };

        var reproduceVersionOption = new Option<string>(
            "--version",
            "-v"
        )
        {
            Description = "Version of the NuGet package to compare against (optional, uses latest if not specified)"
        };

        var reproduceCommand = new Command("reproduce", "Compare a local .nupkg file with a NuGet package from the feed");
        reproduceCommand.Options.Add(reproduceFilenameOption);
        reproduceCommand.Options.Add(reproduceNugetOption);
        reproduceCommand.Options.Add(reproduceVersionOption);
        reproduceCommand.SetAction(async (ParseResult parseResult) =>
        {
            var filename = parseResult.GetValue(reproduceFilenameOption);
            var nuget = parseResult.GetValue(reproduceNugetOption);
            var version = parseResult.GetValue(reproduceVersionOption);

            if (string.IsNullOrWhiteSpace(filename))
            {
                Console.Error.WriteLine("--filename is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(nuget))
            {
                Console.Error.WriteLine("--nuget is required.");
                return;
            }

            await ReproduceComparisonAsync(filename, nuget, version);
        });

        rootCommand.Subcommands.Add(instrumentCommand);
        rootCommand.Subcommands.Add(scorecardCommand);
        rootCommand.Subcommands.Add(compareCommand);
        rootCommand.Subcommands.Add(reproduceCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }

    static async Task GetScorecardsForProjectAsync(string? projectPath = null)
    {

        Console.WriteLine($"Analyzing project: {projectPath ?? "all projects"}");
        Console.WriteLine();

        // Get package list from the project
        var packageList = projectPath != null ? await DotnetCliExecutor.GetPackageListAsync(projectPath) : await DotnetCliExecutor.GetPackageListAsync();
        
        if (packageList == null || packageList.Projects.Count == 0)
        {
            Console.WriteLine("No packages found in the project.");
            return;
        }

        var project = packageList.Projects[0];
        if (project.Frameworks.Count == 0)
        {
            Console.WriteLine("No frameworks found in the project.");
            return;
        }

        var framework = project.Frameworks[0];
        var topLevelPackages = framework.TopLevelPackages;

        if (topLevelPackages.Count == 0)
        {
            Console.WriteLine("No top-level packages found in the project.");
            return;
        }

        Console.WriteLine($"Found {topLevelPackages.Count} top-level package(s):");
        Console.WriteLine();

        // Initialize services
        //var nugetService = new NuGetService();
        var scorecardClient = new ScorecardClient();

        // Process each package
        var results = new List<PackageScorecardResult>();

        foreach (var package in topLevelPackages)
        {
            Console.Write($"Processing {package.Id} {package.ResolvedVersion}... ");
            
            try
            {
                var scorecardResult = await scorecardClient.GetScorecardResultFromPackageAsync(
                    package.Id, 
                    package.ResolvedVersion);

                if (scorecardResult != null)
                {
                    results.Add(new PackageScorecardResult
                    {
                        PackageId = package.Id,
                        PackageVersion = package.ResolvedVersion ?? "unknown",
                        Scorecard = scorecardResult
                    });
                    Console.WriteLine($"✓ Score: {scorecardResult.Score:F2}/10");
                }
                else
                {
                    results.Add(new PackageScorecardResult
                    {
                        PackageId = package.Id,
                        PackageVersion = package.ResolvedVersion ?? "unknown",
                        Scorecard = null
                    });
                    Console.WriteLine("No scorecard available");
                }
            }
            catch (Exception ex)
            {
                results.Add(new PackageScorecardResult
                {
                    PackageId = package.Id,
                    PackageVersion = package.ResolvedVersion ?? "unknown",
                    Scorecard = null,
                    Error = ex.Message
                });
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Summary ===");
        Console.WriteLine();

        var packagesWithScorecards = results.Where(r => r.Scorecard != null).ToList();
        var packagesWithoutScorecards = results.Where(r => r.Scorecard == null && string.IsNullOrEmpty(r.Error)).ToList();
        var packagesWithErrors = results.Where(r => !string.IsNullOrEmpty(r.Error)).ToList();

        if (packagesWithScorecards.Count > 0)
        {
            Console.WriteLine($"Packages with scorecards ({packagesWithScorecards.Count}):");
            foreach (var result in packagesWithScorecards.OrderByDescending(r => r.Scorecard!.Score))
            {
                Console.WriteLine($"  {result.PackageId} {result.PackageVersion}: {result.Scorecard!.Score:F2}/10");
            }
            Console.WriteLine();
        }

        if (packagesWithoutScorecards.Count > 0)
        {
            Console.WriteLine($"Packages without scorecards ({packagesWithoutScorecards.Count}):");
            foreach (var result in packagesWithoutScorecards)
            {
                Console.WriteLine($"  {result.PackageId} {result.PackageVersion}");
            }
            Console.WriteLine();
        }

        if (packagesWithErrors.Count > 0)
        {
            Console.WriteLine($"Packages with errors ({packagesWithErrors.Count}):");
            foreach (var result in packagesWithErrors)
            {
                Console.WriteLine($"  {result.PackageId} {result.PackageVersion}: {result.Error}");
            }
            Console.WriteLine();
        }

        // Show detailed scorecard information
        if (packagesWithScorecards.Count > 0)
        {
            Console.WriteLine("=== Detailed Scorecard Information ===");
            Console.WriteLine();

            foreach (var result in packagesWithScorecards)
            {
                var sc = result.Scorecard!;
                Console.WriteLine($"Package: {result.PackageId} {result.PackageVersion}");
                Console.WriteLine($"Repository: {sc.Repo.Name}");
                Console.WriteLine($"Score: {sc.Score:F2}/10");
                Console.WriteLine($"Date: {sc.Date}");
                Console.WriteLine($"Scorecard Version: {sc.Scorecard.Version}");

                if (sc.Checks.Count > 0)
                {
                    Console.WriteLine("Checks:");
                    foreach (var check in sc.Checks.OrderByDescending(c => c.Score))
                    {
                        var status = check.Score == -1 ? "N/A" : $"{check.Score}/10";
                        Console.WriteLine($"  {check.Name}: {status}");
                        if (!string.IsNullOrWhiteSpace(check.Reason))
                        {
                            Console.WriteLine($"    {check.Reason}");
                        }
                    }
                }

                Console.WriteLine();
            }
        }
    }

    static async Task InstrumentAssemblyAsync(string filename)
    {
        if (!File.Exists(filename))
        {
            Console.Error.WriteLine($"Assembly file not found: {filename}");
            return;
        }

        Console.WriteLine($"Instrumenting assembly: {filename}");
        
        var analyzer = new AssemblyAnalyzer(filename);
        var result = analyzer.Analyse();

        if (result.HasError)
        {
            Console.Error.WriteLine($"Error analyzing assembly: {result.ExceptionOccurred?.Message}");
            return;
        }

        var writer = WriterFactory.CreateWriter("FXT", "fenneclabs");
        await writer.WriteOutputAsync(result);
        
        Console.WriteLine($"Instrumentation complete. Output written to fenneclabs folder.");
    }

    static async Task InstrumentNuGetPackageAsync(string packageId, string? version)
    {
        Console.WriteLine($"Downloading NuGet package: {packageId} {version ?? "latest"}");
        
        var nugetService = new NuGetService();
        
        try
        {
            // Download the package
            var packagePath = await nugetService.DownloadPackageAsync(packageId, version);
            Console.WriteLine($"Package downloaded to: {packagePath}");

            // Get package contents to find DLL files
            var contents = await nugetService.GetPackageContentsAsync(packageId, version);
            var dllFiles = contents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                    && !f.Path.Contains("_._")) // Exclude placeholder files
                .ToList();

            if (dllFiles.Count == 0)
            {
                Console.WriteLine("No DLL files found in the package.");
                return;
            }

            Console.WriteLine($"Found {dllFiles.Count} DLL file(s) in the package:");
            foreach (var dll in dllFiles)
            {
                Console.WriteLine($"  - {dll.Path}");
            }
            Console.WriteLine();

            // Instrument each DLL
            var writer = WriterFactory.CreateWriter("FXT", ".");
            int successCount = 0;
            int errorCount = 0;

            foreach (var dll in dllFiles)
            {
                Console.Write($"Instrumenting {dll.Path}... ");
                try
                {
                    var analyzer = new AssemblyAnalyzer(dll.FullPath);
                    var result = analyzer.Analyse();

                    if (result.HasError)
                    {
                        Console.WriteLine($"Error: {result.ExceptionOccurred?.Message}");
                        errorCount++;
                    }
                    else
                    {
                        // Pass the relative path from the package to preserve folder structure
                        await writer.WriteOutputAsync(result, dll.Path);
                        Console.WriteLine("✓");
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                    errorCount++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Instrumentation complete: {successCount} succeeded, {errorCount} failed.");
            Console.WriteLine($"Output written to fenneclabs folder.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error downloading or processing package: {ex.Message}");
        }
    }

    static async Task CompareNuGetPackageAsync(string packageId, string? version)
    {
        Console.WriteLine($"Comparing NuGet package: {packageId}");
        
        var nugetService = new NuGetService();
        
        try
        {
            // Get all versions of the package
            var allVersions = await nugetService.GetPackageVersionsAsync(packageId, includePrerelease: false);
            var sortedVersions = allVersions.OrderByDescending(v => v).ToList();

            if (sortedVersions.Count < 2)
            {
                Console.WriteLine($"Package '{packageId}' has less than 2 versions. Cannot compare.");
                return;
            }

            NuGetVersion? currentVersion = null;
            NuGetVersion? previousVersion = null;

            if (!string.IsNullOrWhiteSpace(version))
            {
                if (!NuGetVersion.TryParse(version, out var parsedVersion))
                {
                    Console.Error.WriteLine($"Invalid version format: {version}");
                    return;
                }
                currentVersion = parsedVersion;

                // Find the index of the current version
                var currentIndex = sortedVersions.FindIndex(v => v == currentVersion!);
                if (currentIndex == -1)
                {
                    Console.Error.WriteLine($"Version '{version}' not found for package '{packageId}'");
                    return;
                }

                if (currentIndex == sortedVersions.Count - 1)
                {
                    Console.Error.WriteLine($"Version '{version}' is the oldest version. Cannot compare with previous version.");
                    return;
                }

                previousVersion = sortedVersions[currentIndex + 1];
            }
            else
            {
                // Use latest and previous latest
                currentVersion = sortedVersions[0];
                previousVersion = sortedVersions[1];
            }

            if (currentVersion == null || previousVersion == null)
            {
                Console.Error.WriteLine("Failed to determine versions for comparison.");
                return;
            }

            Console.WriteLine($"Comparing version {currentVersion} with previous version {previousVersion}");
            Console.WriteLine();

            // Download both versions
            Console.WriteLine($"Downloading version {currentVersion}...");
            var currentPackagePath = await nugetService.DownloadPackageAsync(packageId, currentVersion.ToNormalizedString());
            Console.WriteLine($"Downloaded to: {currentPackagePath}");

            Console.WriteLine($"Downloading version {previousVersion}...");
            var previousPackagePath = await nugetService.DownloadPackageAsync(packageId, previousVersion.ToNormalizedString());
            Console.WriteLine($"Downloaded to: {previousPackagePath}");
            Console.WriteLine();

            // Get contents of both packages
            var currentContents = await nugetService.GetPackageContentsAsync(packageId, currentVersion.ToNormalizedString());
            var previousContents = await nugetService.GetPackageContentsAsync(packageId, previousVersion.ToNormalizedString());

            // Find DLL files in both packages
            var currentDlls = currentContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            var previousDlls = previousContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            // Find matching DLLs by relative path
            var matchingDlls = currentDlls.Keys.Intersect(previousDlls.Keys).ToList();
            var onlyInCurrent = currentDlls.Keys.Except(previousDlls.Keys).ToList();
            var onlyInPrevious = previousDlls.Keys.Except(currentDlls.Keys).ToList();

            Console.WriteLine($"Found {matchingDlls.Count} matching DLL file(s) to compare");
            if (onlyInCurrent.Count > 0)
            {
                Console.WriteLine($"  {onlyInCurrent.Count} DLL(s) only in version {currentVersion}");
            }
            if (onlyInPrevious.Count > 0)
            {
                Console.WriteLine($"  {onlyInPrevious.Count} DLL(s) only in version {previousVersion}");
            }
            Console.WriteLine();

            if (matchingDlls.Count == 0)
            {
                Console.WriteLine("No matching DLL files found to compare.");
                return;
            }

            // Compare each matching DLL
            int identicalCount = 0;
            int differentCount = 0;
            int errorCount = 0;

            foreach (var dllPath in matchingDlls)
            {
                Console.WriteLine($"Comparing {dllPath}...");
                try
                {
                    var currentDll = currentDlls[dllPath];
                    var previousDll = previousDlls[dllPath];

                    using var assembly1 = AssemblyDefinition.ReadAssembly(previousDll.FullPath);
                    using var assembly2 = AssemblyDefinition.ReadAssembly(currentDll.FullPath);

                    var comparer = new AssemblyComparer(assembly1, assembly2);
                    var result = comparer.Compare();

                    if (result.AreEqual)
                    {
                        Console.WriteLine($"  ✓ Identical");
                        identicalCount++;
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ Differences found ({result.Differences.Count} difference(s))");
                        differentCount++;

                        // Show summary
                        if (result.TypesOnlyInAssembly1.Count > 0)
                        {
                            Console.WriteLine($"    - {result.TypesOnlyInAssembly1.Count} type(s) only in {previousVersion}");
                        }
                        if (result.TypesOnlyInAssembly2.Count > 0)
                        {
                            Console.WriteLine($"    - {result.TypesOnlyInAssembly2.Count} type(s) only in {currentVersion}");
                        }
                        if (result.MethodBodyDifferences.Count > 0)
                        {
                            Console.WriteLine($"    - {result.MethodBodyDifferences.Count} method(s) with body differences");
                        }

                        // Show detailed report
                        Console.WriteLine();
                        Console.WriteLine("  Detailed Report:");
                        var report = result.GenerateReport();
                        foreach (var line in report.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                Console.WriteLine($"    {line}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ Error: {ex.Message}");
                    errorCount++;
                }
                Console.WriteLine();
            }

            // Summary
            Console.WriteLine("=== Comparison Summary ===");
            Console.WriteLine($"Package: {packageId}");
            Console.WriteLine($"Version {currentVersion} vs {previousVersion}");
            Console.WriteLine($"  Identical: {identicalCount}");
            Console.WriteLine($"  Different: {differentCount}");
            Console.WriteLine($"  Errors: {errorCount}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error comparing package: {ex.Message}");
        }
    }

    static async Task ReproduceComparisonAsync(string nupkgFilePath, string packageId, string? version)
    {
        if (!File.Exists(nupkgFilePath))
        {
            Console.Error.WriteLine($"File not found: {nupkgFilePath}");
            return;
        }

        if (!nupkgFilePath.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"File must be a .nupkg file: {nupkgFilePath}");
            return;
        }

        Console.WriteLine($"Comparing local package file: {nupkgFilePath}");
        Console.WriteLine($"With NuGet package: {packageId} {version ?? "latest"}");
        Console.WriteLine();

        var nugetService = new NuGetService();
        string? tempExtractPath = null;

        try
        {
            // Extract the local .nupkg file
            tempExtractPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempExtractPath);

            Console.WriteLine($"Extracting local package to: {tempExtractPath}");
            await ExtractNupkgFileAsync(nupkgFilePath, tempExtractPath);
            Console.WriteLine("Extraction complete.");
            Console.WriteLine();

            // Get DLL files from the extracted .nupkg
            var localContents = GetPackageContentsFromDirectory(tempExtractPath);
            var localDlls = localContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            // Download the NuGet package from the feed
            Console.WriteLine($"Downloading NuGet package: {packageId} {version ?? "latest"}");
            var feedPackagePath = await nugetService.DownloadPackageAsync(packageId, version);
            Console.WriteLine($"Downloaded to: {feedPackagePath}");
            Console.WriteLine();

            // Get DLL files from the downloaded package
            var feedContents = await nugetService.GetPackageContentsAsync(packageId, version);
            var feedDlls = feedContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) 
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            // Find matching DLLs by relative path
            var matchingDlls = localDlls.Keys.Intersect(feedDlls.Keys).ToList();
            var onlyInLocal = localDlls.Keys.Except(feedDlls.Keys).ToList();
            var onlyInFeed = feedDlls.Keys.Except(localDlls.Keys).ToList();

            Console.WriteLine($"Found {matchingDlls.Count} matching DLL file(s) to compare");
            if (onlyInLocal.Count > 0)
            {
                Console.WriteLine($"  {onlyInLocal.Count} DLL(s) only in local package");
            }
            if (onlyInFeed.Count > 0)
            {
                Console.WriteLine($"  {onlyInFeed.Count} DLL(s) only in feed package");
            }
            Console.WriteLine();

            if (matchingDlls.Count == 0)
            {
                Console.WriteLine("No matching DLL files found to compare.");
                return;
            }

            // Compare each matching DLL
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
                        Console.WriteLine($"  ✓ Identical");
                        identicalCount++;
                    }
                    else
                    {
                        Console.WriteLine($"  ✗ Differences found ({result.Differences.Count} difference(s))");
                        differentCount++;

                        // Show summary
                        if (result.TypesOnlyInAssembly1.Count > 0)
                        {
                            Console.WriteLine($"    - {result.TypesOnlyInAssembly1.Count} type(s) only in local package");
                        }
                        if (result.TypesOnlyInAssembly2.Count > 0)
                        {
                            Console.WriteLine($"    - {result.TypesOnlyInAssembly2.Count} type(s) only in feed package");
                        }
                        if (result.MethodBodyDifferences.Count > 0)
                        {
                            Console.WriteLine($"    - {result.MethodBodyDifferences.Count} method(s) with body differences");
                        }

                        // Show detailed report
                        Console.WriteLine();
                        Console.WriteLine("  Detailed Report:");
                        var report = result.GenerateReport();
                        foreach (var line in report.Split('\n'))
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                Console.WriteLine($"    {line}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ✗ Error: {ex.Message}");
                    errorCount++;
                }
                Console.WriteLine();
            }

            // Summary
            Console.WriteLine("=== Comparison Summary ===");
            Console.WriteLine($"Local Package: {nupkgFilePath}");
            Console.WriteLine($"Feed Package: {packageId} {version ?? "latest"}");
            Console.WriteLine($"  Identical: {identicalCount}");
            Console.WriteLine($"  Different: {differentCount}");
            Console.WriteLine($"  Errors: {errorCount}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error comparing packages: {ex.Message}");
        }
        finally
        {
            // Clean up temporary extraction directory
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

    static async Task ExtractNupkgFileAsync(string nupkgFilePath, string extractPath)
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
            {
                Directory.CreateDirectory(entryDirectory);
            }

            using var entryStream = entry.Open();
            using var fileOutStream = File.Create(entryPath);
            await entryStream.CopyToAsync(fileOutStream);
        }
    }

    static List<PackageFileInfo> GetPackageContentsFromDirectory(string packagePath)
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

    private class PackageScorecardResult
    {
        public string PackageId { get; set; } = string.Empty;
        public string PackageVersion { get; set; } = string.Empty;
        public ScorecardResult? Scorecard { get; set; }
        public string? Error { get; set; }
    }
}
