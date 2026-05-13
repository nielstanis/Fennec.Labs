using FennecLabs.AssemblyDiff;
using FennecLabs.NuGet;
using Mono.Cecil;
using NuGet.Versioning;

namespace FennecLabs.Cli.Commands;

internal class CompareCommandHandler
{
    private readonly NuGetService _nugetService;

    public CompareCommandHandler(NuGetService nugetService)
    {
        _nugetService = nugetService;
    }

    public async Task<int> ExecuteAsync(string packageId, string? version)
    {
        Console.WriteLine($"Comparing NuGet package: {packageId}");

        try
        {
            var allVersions = await _nugetService.GetPackageVersionsAsync(packageId, includePrerelease: false);
            var sortedVersions = allVersions.OrderByDescending(v => v).ToList();

            if (sortedVersions.Count < 2)
            {
                Console.WriteLine($"Package '{packageId}' has less than 2 versions. Cannot compare.");
                return 0;
            }

            NuGetVersion? currentVersion;
            NuGetVersion? previousVersion;

            if (!string.IsNullOrWhiteSpace(version))
            {
                if (!NuGetVersion.TryParse(version, out var parsedVersion))
                {
                    Console.Error.WriteLine($"Invalid version format: {version}");
                    return 1;
                }
                currentVersion = parsedVersion;

                var currentIndex = sortedVersions.FindIndex(v => v == currentVersion);
                if (currentIndex == -1)
                {
                    Console.Error.WriteLine($"Version '{version}' not found for package '{packageId}'");
                    return 1;
                }

                if (currentIndex == sortedVersions.Count - 1)
                {
                    Console.Error.WriteLine(
                        $"Version '{version}' is the oldest version. Cannot compare with previous version.");
                    return 1;
                }

                previousVersion = sortedVersions[currentIndex + 1];
            }
            else
            {
                currentVersion = sortedVersions[0];
                previousVersion = sortedVersions[1];
            }

            Console.WriteLine($"Comparing version {currentVersion} with previous version {previousVersion}");
            Console.WriteLine();

            Console.WriteLine($"Downloading version {currentVersion}...");
            var currentPackagePath = await _nugetService.DownloadPackageAsync(
                packageId, currentVersion.ToNormalizedString());
            Console.WriteLine($"Downloaded to: {currentPackagePath}");

            Console.WriteLine($"Downloading version {previousVersion}...");
            var previousPackagePath = await _nugetService.DownloadPackageAsync(
                packageId, previousVersion.ToNormalizedString());
            Console.WriteLine($"Downloaded to: {previousPackagePath}");
            Console.WriteLine();

            var currentContents = await _nugetService.GetPackageContentsAsync(
                packageId, currentVersion.ToNormalizedString());
            var previousContents = await _nugetService.GetPackageContentsAsync(
                packageId, previousVersion.ToNormalizedString());

            var currentDlls = currentContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            var previousDlls = previousContents
                .Where(f => f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !f.Path.Contains("_._"))
                .ToDictionary(f => f.Path, f => f);

            var matchingDlls = currentDlls.Keys.Intersect(previousDlls.Keys).ToList();
            var onlyInCurrent = currentDlls.Keys.Except(previousDlls.Keys).ToList();
            var onlyInPrevious = previousDlls.Keys.Except(currentDlls.Keys).ToList();

            Console.WriteLine($"Found {matchingDlls.Count} matching DLL file(s) to compare");
            if (onlyInCurrent.Count > 0)
                Console.WriteLine($"  {onlyInCurrent.Count} DLL(s) only in version {currentVersion}");
            if (onlyInPrevious.Count > 0)
                Console.WriteLine($"  {onlyInPrevious.Count} DLL(s) only in version {previousVersion}");
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
                    var currentDll = currentDlls[dllPath];
                    var previousDll = previousDlls[dllPath];

                    using var assembly1 = AssemblyDefinition.ReadAssembly(previousDll.FullPath);
                    using var assembly2 = AssemblyDefinition.ReadAssembly(currentDll.FullPath);

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
                                $"    - {result.TypesOnlyInAssembly1.Count} type(s) only in {previousVersion}");
                        if (result.TypesOnlyInAssembly2.Count > 0)
                            Console.WriteLine(
                                $"    - {result.TypesOnlyInAssembly2.Count} type(s) only in {currentVersion}");
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
            Console.WriteLine($"Package: {packageId}");
            Console.WriteLine($"Version {currentVersion} vs {previousVersion}");
            Console.WriteLine($"  Identical: {identicalCount}");
            Console.WriteLine($"  Different: {differentCount}");
            Console.WriteLine($"  Errors: {errorCount}");
            return errorCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error comparing package: {ex.Message}");
            return 1;
        }
    }
}
