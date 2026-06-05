using System.Text.Json;
using FennecLabs.Cli.Rendering;
using FennecLabs.NuGet;
using NuGet.Versioning;

namespace FennecLabs.Cli.Commands;

internal class CompareCommandHandler
{
    private readonly NuGetService _nugetService;

    public CompareCommandHandler(NuGetService nugetService)
    {
        _nugetService = nugetService;
    }

    public async Task<int> ExecuteAsync(
        string packageId, string? version, OutputMode outputMode, string output, bool noCache)
    {
        try
        {
            var allVersions = await _nugetService.GetPackageVersionsAsync(packageId, includePrerelease: false);
            var sortedVersions = allVersions.OrderByDescending(v => v).ToList();

            if (sortedVersions.Count < 2)
            {
                Console.Error.WriteLine($"Package '{packageId}' has less than 2 versions. Cannot compare.");
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

            var currentVersionStr = currentVersion.ToNormalizedString();
            var previousVersionStr = previousVersion.ToNormalizedString();

            var cachePath = OutputCache.ComparePath(output, packageId, currentVersionStr, previousVersionStr);
            if (!noCache && OutputCache.Exists(cachePath))
            {
                var cached = OutputCache.TryLoad(cachePath)!;
                if (outputMode == OutputMode.Json)
                    Console.WriteLine(cached);
                else
                    DllPipeline.RenderCachedResult(cached, cachePath);
                return 0;
            }

            await StatusRunner.RunAsync(
                outputMode, $"Downloading {packageId} {currentVersionStr}…",
                () => _nugetService.DownloadPackageAsync(packageId, currentVersionStr));

            await StatusRunner.RunAsync(
                outputMode, $"Downloading {packageId} {previousVersionStr}…",
                () => _nugetService.DownloadPackageAsync(packageId, previousVersionStr));

            var currentContents = await _nugetService.GetPackageContentsAsync(packageId, currentVersionStr);
            var previousContents = await _nugetService.GetPackageContentsAsync(packageId, previousVersionStr);

            var currentDlls = currentContents.Where(DllPipeline.IsLibraryDll).ToDictionary(f => f.Path, f => f);
            var previousDlls = previousContents.Where(DllPipeline.IsLibraryDll).ToDictionary(f => f.Path, f => f);

            var matchingDlls = currentDlls.Keys.Intersect(previousDlls.Keys).ToList();
            var onlyInCurrent = currentDlls.Keys.Except(previousDlls.Keys).ToList();
            var onlyInPrevious = previousDlls.Keys.Except(currentDlls.Keys).ToList();

            if (matchingDlls.Count == 0)
            {
                Console.Error.WriteLine("No matching DLL files found to compare.");
                return 0;
            }

            var (dllResults, identicalCount, differentCount, errorCount) =
                DllPipeline.CompareMatchedDlls(matchingDlls, previousDlls, currentDlls);

            var compareResult = new
            {
                packageId,
                currentVersion = currentVersionStr,
                previousVersion = previousVersionStr,
                perDll = dllResults.Select(DllPipeline.FormatDllResult),
                onlyInCurrent,
                onlyInPrevious,
                summary = new { identical = identicalCount, different = differentCount, errors = errorCount },
            };
            var json = JsonSerializer.Serialize(compareResult, Json.Options);
            await OutputCache.WriteAsync(cachePath, json);

            if (outputMode == OutputMode.Json)
            {
                Console.WriteLine(json);
                return errorCount > 0 ? 1 : 0;
            }

            DiffRenderer.Render(
                $"{packageId} {currentVersionStr} ← {previousVersionStr}",
                dllResults,
                onlyInPrevious,
                onlyInCurrent,
                $"v{previousVersionStr}",
                $"v{currentVersionStr}");

            return errorCount > 0 ? 1 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error comparing package: {ex.Message}");
            return 1;
        }
    }

}
