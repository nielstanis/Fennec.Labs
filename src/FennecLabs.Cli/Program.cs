using System.CommandLine;
using System.Diagnostics;
using FennecLabs.DotNetCli;
using FennecLabs.Instrumentation;
using FennecLabs.Instrumentation.Output;
using FennecLabs.NuGet;
using FennecLabs.Scorecard;

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

        rootCommand.Subcommands.Add(instrumentCommand);
        rootCommand.Subcommands.Add(scorecardCommand);

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

        var writer = WriterFactory.CreateWriter("json", ".fennec");
        await writer.WriteOutputAsync(result);
        
        Console.WriteLine($"Instrumentation complete. Output written to .fennec folder.");
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
                        await writer.WriteOutputAsync(result);
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
            Console.WriteLine($"Output written to .fennec folder.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error downloading or processing package: {ex.Message}");
        }
    }

    private class PackageScorecardResult
    {
        public string PackageId { get; set; } = string.Empty;
        public string PackageVersion { get; set; } = string.Empty;
        public ScorecardResult? Scorecard { get; set; }
        public string? Error { get; set; }
    }
}
