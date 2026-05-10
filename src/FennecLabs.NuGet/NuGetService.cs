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
            _repository = new SourceRepository(new PackageSource("https://api.nuget.org/v3/index.json"), providers);
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

    public async Task<IEnumerable<string>> SearchPackagesAsync(string searchTerm, int take = 10, CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryAsync(cancellationToken);
        var searchResource = await repository.GetResourceAsync<PackageSearchResource>(cancellationToken);
        var searchFilter = new SearchFilter(includePrerelease: false);
        var results = await searchResource.SearchAsync(searchTerm, searchFilter, 0, take, NullLogger.Instance, cancellationToken);
        return results.Select(p => p.Identity.Id);
    }

    public async Task<IEnumerable<NuGetVersion>> GetPackageVersionsAsync(string packageId, bool includePrerelease = false, CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryAsync(cancellationToken);
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
        var cacheContext = new SourceCacheContext();
        var metadata = await metadataResource.GetMetadataAsync(packageId, includePrerelease, includeUnlisted: false, cacheContext, NullLogger.Instance, cancellationToken);
        return metadata.Select(m => m.Identity.Version);
    }

    public async Task<IPackageSearchMetadata?> GetPackageMetadataAsync(string packageId, string? version = null, CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryAsync(cancellationToken);
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
        var cacheContext = new SourceCacheContext();
        var metadata = await metadataResource.GetMetadataAsync(packageId, includePrerelease: true, includeUnlisted: false, cacheContext, NullLogger.Instance, cancellationToken);
        
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

    public async Task<string> DownloadPackageAsync(
        string packageId,
        string? version = null,
        string? feedName = null,
        CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryAsync(cancellationToken);
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
        var downloadResource = await repository.GetResourceAsync<DownloadResource>(cancellationToken);
        var cacheContext = new SourceCacheContext();

        var packages = await metadataResource.GetMetadataAsync(
            packageId,
            includePrerelease: true,
            includeUnlisted: false,
            cacheContext,
            NullLogger.Instance,
            cancellationToken);

        IPackageSearchMetadata? targetPackage;
        if (!string.IsNullOrEmpty(version))
        {
            if (NuGetVersion.TryParse(version, out var parsedVersion))
            {
                targetPackage = packages.FirstOrDefault(p => p.Identity.Version.Equals(parsedVersion));
                if (targetPackage == null)
                {
                    throw new InvalidOperationException($"Version '{version}' of package '{packageId}' not found");
                }
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
            {
                throw new InvalidOperationException($"Package '{packageId}' not found");
            }
        }

        var globalPackagesFolder = GetGlobalPackagesFolder();
        var packagePath = Path.Combine(globalPackagesFolder, targetPackage.Identity.Id.ToLowerInvariant(), targetPackage.Identity.Version.ToNormalizedString());

        // Check if package already exists in global packages folder
        if (Directory.Exists(packagePath))
        {
            return packagePath;
        }

        // Download package
        var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
            targetPackage.Identity,
            new PackageDownloadContext(cacheContext),
            globalPackagesFolder,
            NullLogger.Instance,
            cancellationToken);

        if (downloadResult.Status != DownloadResourceResultStatus.Available || downloadResult.PackageStream == null)
        {
            throw new InvalidOperationException("Failed to download package");
        }

        // Save package to global packages folder
        Directory.CreateDirectory(packagePath);
        await SavePackageToGlobalFolderAsync(downloadResult.PackageStream, packagePath, cancellationToken);

        return packagePath;
    }

    private async Task SavePackageToGlobalFolderAsync(Stream packageStream, string packagePath, CancellationToken cancellationToken)
    {
        // Extract the package to the global packages folder
        using var archive = new ZipArchive(packageStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var entryPath = Path.Combine(packagePath, entry.FullName);
            var entryDirectory = Path.GetDirectoryName(entryPath);

            if (!string.IsNullOrEmpty(entryDirectory))
            {
                Directory.CreateDirectory(entryDirectory);
            }

            using var entryStream = entry.Open();
            using var fileStream = File.Create(entryPath);
            await entryStream.CopyToAsync(fileStream, cancellationToken);
        }
    }

    public async Task<List<PackageFileInfo>> GetPackageContentsAsync(
        string packageId,
        string? version = null,
        string? feedName = null,
        CancellationToken cancellationToken = default)
    {
        var globalPackagesFolder = GetGlobalPackagesFolder();
        var repository = await GetRepositoryAsync(cancellationToken);
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
        var cacheContext = new SourceCacheContext();

        var packages = await metadataResource.GetMetadataAsync(
            packageId,
            includePrerelease: true,
            includeUnlisted: false,
            cacheContext,
            NullLogger.Instance,
            cancellationToken);

        IPackageSearchMetadata? targetPackage;
        if (!string.IsNullOrEmpty(version))
        {
            if (NuGetVersion.TryParse(version, out var parsedVersion))
            {
                targetPackage = packages.FirstOrDefault(p => p.Identity.Version.Equals(parsedVersion));
                if (targetPackage == null)
                {
                    throw new InvalidOperationException($"Version '{version}' of package '{packageId}' not found");
                }
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
            {
                throw new InvalidOperationException($"Package '{packageId}' not found");
            }
        }

        var packagePath = Path.Combine(globalPackagesFolder, targetPackage.Identity.Id.ToLowerInvariant(), targetPackage.Identity.Version.ToNormalizedString());

        // Check if package already exists in global packages folder
        if (Directory.Exists(packagePath))
        {
            return GetPackageContentsFromDirectory(packagePath);
        }

        // Download package if not in global packages folder
        var downloadResource = await repository.GetResourceAsync<DownloadResource>(cancellationToken);
        var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
            targetPackage.Identity,
            new PackageDownloadContext(cacheContext),
            globalPackagesFolder,
            NullLogger.Instance,
            cancellationToken);

        if (downloadResult.Status != DownloadResourceResultStatus.Available || downloadResult.PackageStream == null)
        {
            throw new InvalidOperationException("Failed to download package");
        }

        // Extract and get contents
        await SavePackageToGlobalFolderAsync(downloadResult.PackageStream, packagePath, cancellationToken);
        return GetPackageContentsFromDirectory(packagePath);
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

    public async Task<string> ExtractPackageFileAsync(
        string packageId,
        string filePath,
        string? version = null,
        string? feedName = null,
        CancellationToken cancellationToken = default)
    {
        var repository = await GetRepositoryAsync(cancellationToken);
        var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
        var downloadResource = await repository.GetResourceAsync<DownloadResource>(cancellationToken);
        var cacheContext = new SourceCacheContext();

        var packages = await metadataResource.GetMetadataAsync(
            packageId,
            includePrerelease: true,
            includeUnlisted: false,
            cacheContext,
            NullLogger.Instance,
            cancellationToken);

        IPackageSearchMetadata? targetPackage;
        if (!string.IsNullOrEmpty(version))
        {
            if (NuGetVersion.TryParse(version, out var parsedVersion))
            {
                targetPackage = packages.FirstOrDefault(p => p.Identity.Version.Equals(parsedVersion));
                if (targetPackage == null)
                {
                    throw new InvalidOperationException($"Version '{version}' of package '{packageId}' not found");
                }
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
            {
                throw new InvalidOperationException($"Package '{packageId}' not found");
            }
        }

        var globalPackagesFolder = GetGlobalPackagesFolder();
        var packagePath = Path.Combine(globalPackagesFolder, targetPackage.Identity.Id.ToLowerInvariant(), targetPackage.Identity.Version.ToNormalizedString());

        // Check if package already exists in global packages folder
        if (Directory.Exists(packagePath))
        {
            var localFilePath = Path.Combine(packagePath, filePath);
            if (File.Exists(localFilePath))
            {
                return await File.ReadAllTextAsync(localFilePath, cancellationToken);
            }
            else
            {
                throw new FileNotFoundException($"File '{filePath}' not found in package '{packageId}' v{targetPackage.Identity.Version}");
            }
        }

        // Download package if not in global packages folder
        var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
            targetPackage.Identity,
            new PackageDownloadContext(cacheContext),
            globalPackagesFolder,
            NullLogger.Instance,
            cancellationToken);

        if (downloadResult.Status != DownloadResourceResultStatus.Available || downloadResult.PackageStream == null)
        {
            throw new InvalidOperationException("Failed to download package");
        }

        // Extract the specific file from the package stream
        using var archive = new ZipArchive(downloadResult.PackageStream, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(e =>
            string.Equals(e.FullName, filePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.FullName.Replace('/', Path.DirectorySeparatorChar), filePath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(e.FullName.Replace('\\', Path.DirectorySeparatorChar), filePath, StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"File '{filePath}' not found in package '{packageId}' v{targetPackage.Identity.Version}");

        using var entryStream = entry.Open();
        using var reader = new StreamReader(entryStream);
        return await reader.ReadToEndAsync();
    }

    public async Task<string?> GetPackageNuspecContentAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Try to find the nuspec file in the package
            // The nuspec file is typically named {packageId}.nuspec
            var nuspecFileName = $"{packageId}.nuspec";
            
            // First try to get it from an already extracted package
            var globalPackagesFolder = GetGlobalPackagesFolder();
            var repository = await GetRepositoryAsync(cancellationToken);
            var metadataResource = await repository.GetResourceAsync<PackageMetadataResource>(cancellationToken);
            var cacheContext = new SourceCacheContext();

            var packages = await metadataResource.GetMetadataAsync(
                packageId,
                includePrerelease: true,
                includeUnlisted: false,
                cacheContext,
                NullLogger.Instance,
                cancellationToken);

            IPackageSearchMetadata? targetPackage;
            if (!string.IsNullOrEmpty(version))
            {
                if (NuGetVersion.TryParse(version, out var parsedVersion))
                {
                    targetPackage = packages.FirstOrDefault(p => p.Identity.Version.Equals(parsedVersion));
                }
                else
                {
                    return null;
                }
            }
            else
            {
                targetPackage = packages.OrderByDescending(p => p.Identity.Version).FirstOrDefault();
            }

            if (targetPackage == null)
            {
                return null;
            }

            var packagePath = Path.Combine(globalPackagesFolder, targetPackage.Identity.Id.ToLowerInvariant(), targetPackage.Identity.Version.ToNormalizedString());

            // Check if package is already extracted
            if (Directory.Exists(packagePath))
            {
                var nuspecPath = Path.Combine(packagePath, nuspecFileName);
                if (File.Exists(nuspecPath))
                {
                    return await File.ReadAllTextAsync(nuspecPath, cancellationToken);
                }

                // Try case-insensitive search
                var files = Directory.GetFiles(packagePath, "*.nuspec", SearchOption.TopDirectoryOnly);
                if (files.Length > 0)
                {
                    return await File.ReadAllTextAsync(files[0], cancellationToken);
                }
            }

            // Package not extracted, need to download and extract nuspec from stream
            var downloadResource = await repository.GetResourceAsync<DownloadResource>(cancellationToken);
            var downloadResult = await downloadResource.GetDownloadResourceResultAsync(
                targetPackage.Identity,
                new PackageDownloadContext(cacheContext),
                globalPackagesFolder,
                NullLogger.Instance,
                cancellationToken);

            if (downloadResult.Status != DownloadResourceResultStatus.Available || downloadResult.PackageStream == null)
            {
                return null;
            }

            // Extract nuspec from the package stream
            using var archive = new ZipArchive(downloadResult.PackageStream, ZipArchiveMode.Read);
            
            // Try exact match first
            var entry = archive.Entries.FirstOrDefault(e =>
                string.Equals(e.FullName, nuspecFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(e.Name, nuspecFileName, StringComparison.OrdinalIgnoreCase));

            // If not found, try any .nuspec file
            if (entry == null)
            {
                entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
            }

            if (entry == null)
            {
                return null;
            }

            using var entryStream = entry.Open();
            using var reader = new StreamReader(entryStream);
            return await reader.ReadToEndAsync();
        }
        catch
        {
            return null;
        }
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
            var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
            
            // Find repository element - it can be in different locations
            // Try under metadata/repository
            var repositoryElement = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("repository", StringComparison.OrdinalIgnoreCase));

            if (repositoryElement == null)
            {
                return null;
            }

            // Get the url attribute
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

