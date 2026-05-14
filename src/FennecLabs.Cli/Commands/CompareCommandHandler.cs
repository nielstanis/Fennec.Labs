using System.Text.Json;
using FennecLabs.AssemblyDiff;
using FennecLabs.Cli.Rendering;
using FennecLabs.NuGet;
using Mono.Cecil;
using NuGet.Versioning;
using Spectre.Console;

namespace FennecLabs.Cli.Commands;

internal class CompareCommandHandler
{
    private readonly NuGetService _nugetService;

    public CompareCommandHandler(NuGetService nugetService)
    {
        _nugetService = nugetService;
    }

    public async Task<int> ExecuteAsync(string packageId, string? version, OutputMode outputMode)
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

            string currentPackagePath = await DownloadWithStatus(
                packageId, currentVersionStr,
                $"Downloading {packageId} {currentVersionStr}…",
                outputMode);

            string previousPackagePath = await DownloadWithStatus(
                packageId, previousVersionStr,
                $"Downloading {packageId} {previousVersionStr}…",
                outputMode);

            var currentContents = await _nugetService.GetPackageContentsAsync(packageId, currentVersionStr);
            var previousContents = await _nugetService.GetPackageContentsAsync(packageId, previousVersionStr);

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
                    using var assembly1 = AssemblyDefinition.ReadAssembly(previousDlls[dllPath].FullPath);
                    using var assembly2 = AssemblyDefinition.ReadAssembly(currentDlls[dllPath].FullPath);
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

            if (outputMode == OutputMode.Json)
            {
                var output = new
                {
                    packageId,
                    currentVersion = currentVersionStr,
                    previousVersion = previousVersionStr,
                    perDll = dllResults.Select(d => new
                    {
                        dllPath = d.DllPath,
                        areEqual = d.Result?.AreEqual,
                        differences = d.Result?.Differences,
                        typesAdded = d.Result?.TypesOnlyInAssembly2.ToList(),
                        typesRemoved = d.Result?.TypesOnlyInAssembly1.ToList(),
                        methodBodyDifferences = d.Result?.MethodBodyDifferences.Select(m => new
                        {
                            typeName = m.TypeName,
                            methodSignature = m.MethodSignature,
                            instructionDifferences = m.InstructionDifferences,
                        }),
                        error = d.Error,
                    }),
                    onlyInCurrent,
                    onlyInPrevious,
                    summary = new { identical = identicalCount, different = differentCount, errors = errorCount },
                };
                Console.WriteLine(JsonSerializer.Serialize(output, Json.Options));
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

    private async Task<string> DownloadWithStatus(
        string packageId, string version, string statusMessage, OutputMode outputMode)
    {
        if (outputMode == OutputMode.Json)
            return await _nugetService.DownloadPackageAsync(packageId, version);

        string path = string.Empty;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("grey"))
            .StartAsync(statusMessage, async _ =>
            {
                path = await _nugetService.DownloadPackageAsync(packageId, version);
            });
        return path;
    }
}
