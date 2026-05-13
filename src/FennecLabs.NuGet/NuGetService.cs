using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using System.IO.Compression;
using System.Xml.Linq;

namespace FennecLabs.NuGet;

public class NuGetService
{
    private readonly SourceRepository _repository;
    private readonly FeedService? _feedService;

    public NuGetService(string? sourceUrl = null, FeedService? feedService = null)
    {
        _feedService = feedService;

        if (sourceUrl != null)
        {
            var providers = Repository.Provider.GetCoreV3();
            _repository = new SourceRepository(new PackageSource(sourceUrl), providers);
        }
        else
        {
            // Will be initialized lazily via FeedService if needed
            var providers = Repository.Provider.GetCoreV3();
            _repository = new SourceRepository(
                new PackageSource("https://api.nuget.org/v3/index.json"), providers);
        }
    }

    private async Task<SourceRepository> GetRepositoryAsync(CancellationToken cancellationToken = default)
    {
        if (_feedService != null)
        {
            return await _feedService.GetRepositoryAsync();
        }
        return _repository;
    }

    public async Task<IEnumerable<string>> SearchPackagesAsync(
        string searchTerm,
        int take = 10,
        CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryAsync(cancellationToken);
        var searchResource = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken);
        var searchFilter = new SearchFilter(includePrerelease: false);
        var results = await searchResource.SearchAsync(
            searchTerm, searchFilter, 0, take, NullLogger.Instance, cancellationToken);
        return results.Select(p => p.Identity.Id);
    }

    public async Task<IEnumerable<NuGetVersion>> GetPackageVersionsAsync(
        string packageId,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryAsync(cancellationToken);
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
        using var cacheContext = new SourceCacheContext();
        var metadata = await metadataResource.GetMetadataAsync(
            packageId, includePrerelease, includeUnlisted: false,
            cacheContext, NullLogger.Instance, cancellationToken);
        return metadata.Select(m => m.Identity.Version);
    }

    public async Task<IPackageSearchMetadata?> GetPackageMetadataAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryAsync(cancellationToken);
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
        using var cacheContext = new SourceCacheContext();
        var metadata = await metadataResource.GetMetadataAsync(
            packageId, includePrerelease: true, includeUnlisted: false,
            cacheContext, NullLogger.Instance, cancellationToken);

        if (version != null && NuGetVersion.TryParse(version, out var nugetVersion))
        {
            return metadata.FirstOrDefault(m => m.Identity.Version == nugetVersion);
        }

        return metadata.OrderByDescending(m => m.Identity.Version).FirstOrDefault();
    }

    public static string GetGlobalPackagesFolder()
    {
        // Check environment variable first
        var nugetPackagesPath = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(nugetPackagesPath))
        {
            return nugetPackagesPath;
        }

        // Try to get from NuGet configuration
        try
        {
            var settings = Settings.LoadDefaultSettings(root: null);
            var globalPackagesFolder = SettingsUtility.GetGlobalPackagesFolder(settings);
            if (!string.IsNullOrEmpty(globalPackagesFolder))
            {
                return globalPackagesFolder;
            }
        }
        catch
        {
            // Fall back to default if configuration loading fails
        }

        // Default path
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages"
        );
    }

    private async Task<T?> ResolveAndDownloadAsync<T>(
        string packageId,
        string? version,
        Func<string, ZipArchive?, T?> extractor,
        CancellationToken ct)
    {
        var repository = await GetRepositoryAsync(ct);
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(ct);
        using var cacheContext = new SourceCacheContext();

        var packages = await metadataResource.GetMetadataAsync(
            packageId,
            includePrerelease: true,
            includeUnlisted: false,
            cacheContext,
            NullLogger.Instance,
            ct);

        IPackageSearchMetadata? targetPackage;
        if (!string.IsNullOrEmpty(version))
        {
            if (NuGetVersion.TryParse(version, out var parsedVersion))
            {
                targetPackage = packages.FirstOrDefault(p => p.Identity.Version.Equals(parsedVersion));
                if (targetPackage == null)
                    throw new InvalidOperationException(
                        $"Version '{version}' of package '{packageId}' not found");
            }
            else
            {
                throw new ArgumentException($"Invalid version format: '{version}'");
            }
        }
        else
        {
            targetPackage = packages.OrderByDescending(p => p.Identity.Version).FirstOrDefault();
            if (targetPackage == null)
                throw new InvalidOperationException($"Package '{packageId}' not found");
        }

        var globalPackagesFolder = GetGlobalPackagesFolder();
        var packagePath = Path.Combine(
            globalPackagesFolder,
            targetPackage.Identity.Id.ToLowerInvariant(),
            targetPackage.Identity.Version.ToNormalizedString());

        if (Directory.Exists(packagePath))
        {
            return extractor(packagePath, null);
        }

        // Download package
        var downloadResource = await repository.GetResourceAsync<DownloadResource>(ct);
        using var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
            targetPackage.Identity,
            new PackageDownloadContext(cacheContext),
            globalPackagesFolder,
            NullLogger.Instance,
            ct);

        if (downloadResult.Status != DownloadResourceResultStatus.Available
            || downloadResult.PackageStream == null)
        {
            throw new InvalidOperationException("Failed to download package");
        }

        using var archive = new ZipArchive(downloadResult.PackageStream, ZipArchiveMode.Read);
        return extractor(packagePath, archive);
    }

    public async Task<string> DownloadPackageAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ResolveAndDownloadAsync<string>(
            packageId,
            version,
            (packagePath, archive) =>
            {
                // Already on disk — nothing to extract
                if (archive is null)
                    return packagePath;

                Directory.CreateDirectory(packagePath);
                SavePackageToGlobalFolder(archive, packagePath);
                return packagePath;
            },
            cancellationToken);

        return result!;
    }

    public async Task<List<PackageFileInfo>> GetPackageContentsAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ResolveAndDownloadAsync<List<PackageFileInfo>>(
            packageId,
            version,
            (packagePath, archive) =>
            {
                if (archive is null)
                    return GetPackageContentsFromDirectory(packagePath);

                Directory.CreateDirectory(packagePath);
                SavePackageToGlobalFolder(archive, packagePath);
                return GetPackageContentsFromDirectory(packagePath);
            },
            cancellationToken);

        return result ?? [];
    }

    public async Task<string> ExtractPackageFileAsync(
        string packageId,
        string filePath,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        var result = await ResolveAndDownloadAsync<string>(
            packageId,
            version,
            (packagePath, archive) =>
            {
                if (archive is null)
                {
                    var localFilePath = Path.Combine(packagePath, filePath);
                    if (File.Exists(localFilePath))
                        return File.ReadAllText(localFilePath);
                    throw new FileNotFoundException(
                        $"File '{filePath}' not found in package '{packageId}'");
                }

                var entry = archive.Entries.FirstOrDefault(e =>
                    string.Equals(e.FullName, filePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        e.FullName.Replace('/', Path.DirectorySeparatorChar),
                        filePath,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        e.FullName.Replace('\\', Path.DirectorySeparatorChar),
                        filePath,
                        StringComparison.OrdinalIgnoreCase))
                    ?? throw new FileNotFoundException(
                        $"File '{filePath}' not found in package '{packageId}'");

                using var entryStream = entry.Open();
                using var reader = new StreamReader(entryStream);
                return reader.ReadToEnd();
            },
            cancellationToken);

        return result!;
    }

    public async Task<string?> GetPackageNuspecContentAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var nuspecFileName = $"{packageId}.nuspec";

            return await ResolveAndDownloadAsync<string>(
                packageId,
                version,
                (packagePath, archive) =>
                {
                    if (archive is null)
                    {
                        // Package already extracted — look for nuspec on disk
                        var nuspecPath = Path.Combine(packagePath, nuspecFileName);
                        if (File.Exists(nuspecPath))
                            return File.ReadAllText(nuspecPath);

                        var files = Directory.GetFiles(packagePath, "*.nuspec", SearchOption.TopDirectoryOnly);
                        return files.Length > 0 ? File.ReadAllText(files[0]) : null;
                    }

                    // Find in archive
                    var entry = archive.Entries.FirstOrDefault(e =>
                        string.Equals(e.FullName, nuspecFileName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Name, nuspecFileName, StringComparison.OrdinalIgnoreCase))
                        ?? archive.Entries.FirstOrDefault(e =>
                            e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));

                    if (entry == null)
                        return null;

                    using var entryStream = entry.Open();
                    using var reader = new StreamReader(entryStream);
                    return reader.ReadToEnd();
                },
                cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    private static void SavePackageToGlobalFolder(ZipArchive archive, string packagePath)
    {
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var entryPath = Path.Combine(packagePath, entry.FullName);
            var entryDirectory = Path.GetDirectoryName(entryPath);

            if (!string.IsNullOrEmpty(entryDirectory))
                Directory.CreateDirectory(entryDirectory);

            using var entryStream = entry.Open();
            using var fileStream = File.Create(entryPath);
            entryStream.CopyTo(fileStream);
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

    public static string? ExtractRepositoryUrlFromNuspec(string nuspecContent)
    {
        if (string.IsNullOrWhiteSpace(nuspecContent))
        {
            return null;
        }

        try
        {
            var doc = XDocument.Parse(nuspecContent);

            var repositoryElement = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals(
                    "repository", StringComparison.OrdinalIgnoreCase));

            if (repositoryElement == null)
            {
                return null;
            }

            var urlAttribute = repositoryElement.Attribute("url");
            if (urlAttribute != null && !string.IsNullOrWhiteSpace(urlAttribute.Value))
            {
                return urlAttribute.Value;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
