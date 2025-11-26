using FennecLabs.NuGet;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using Xunit;

namespace FennecLabs.NuGet.Tests;

public class NuGetServiceTests
{
    private readonly NuGetService _nugetService;

    public NuGetServiceTests()
    {
        _nugetService = new NuGetService();
    }

    [Fact]
    public async Task SearchPackagesAsync_WithValidSearchTerm_ReturnsResults()
    {
        // Arrange
        var searchTerm = "Newtonsoft.Json";

        // Act
        var results = await _nugetService.SearchPackagesAsync(searchTerm, take: 5);

        // Assert
        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Contains("Newtonsoft.Json", results);
    }

    [Fact]
    public async Task SearchPackagesAsync_WithCommonPackage_ReturnsResults()
    {
        // Arrange
        var searchTerm = "Microsoft.Extensions.Logging";

        // Act
        var results = await _nugetService.SearchPackagesAsync(searchTerm, take: 10);

        // Assert
        Assert.NotNull(results);
        Assert.NotEmpty(results);
        Assert.Contains("Microsoft.Extensions.Logging", results);
    }

    [Fact]
    public async Task GetPackageVersionsAsync_WithValidPackageId_ReturnsVersions()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";

        // Act
        var versions = await _nugetService.GetPackageVersionsAsync(packageId);

        // Assert
        Assert.NotNull(versions);
        Assert.NotEmpty(versions);
        Assert.All(versions, v => Assert.NotNull(v));
    }

    [Fact]
    public async Task GetPackageVersionsAsync_WithIncludePrerelease_ReturnsPrereleaseVersions()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";

        // Act
        var versions = await _nugetService.GetPackageVersionsAsync(packageId, includePrerelease: true);

        // Assert
        Assert.NotNull(versions);
        Assert.NotEmpty(versions);
    }

    [Fact]
    public async Task GetPackageMetadataAsync_WithValidPackageId_ReturnsMetadata()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";

        // Act
        var metadata = await _nugetService.GetPackageMetadataAsync(packageId);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(packageId, metadata.Identity.Id);
        Assert.NotNull(metadata.Identity.Version);
    }

    [Fact]
    public async Task GetPackageMetadataAsync_WithSpecificVersion_ReturnsCorrectVersion()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";
        var version = "13.0.3";

        // Act
        var metadata = await _nugetService.GetPackageMetadataAsync(packageId, version);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal(packageId, metadata.Identity.Id);
        Assert.Equal(version, metadata.Identity.Version.ToNormalizedString());
    }

    [Fact]
    public async Task GetPackageMetadataAsync_WithInvalidVersion_ReturnsNull()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";
        var invalidVersion = "999.999.999";

        // Act
        var metadata = await _nugetService.GetPackageMetadataAsync(packageId, invalidVersion);

        // Assert
        Assert.Null(metadata);
    }

    [Fact]
    public void GetGlobalPackagesFolder_ReturnsValidPath()
    {
        // Act
        var path = NuGetService.GetGlobalPackagesFolder();

        // Assert
        Assert.NotNull(path);
        Assert.NotEmpty(path);
    }

    [Fact]
    public async Task DownloadPackageAsync_WithValidPackage_ReturnsPackagePath()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";
        var version = "13.0.3";

        // Act
        var packagePath = await _nugetService.DownloadPackageAsync(packageId, version);

        // Assert
        Assert.NotNull(packagePath);
        Assert.True(Directory.Exists(packagePath));
        Assert.Contains(packageId.ToLowerInvariant(), packagePath);
    }

    [Fact]
    public async Task DownloadPackageAsync_WithLatestVersion_ReturnsPackagePath()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";

        // Act
        var packagePath = await _nugetService.DownloadPackageAsync(packageId);

        // Assert
        Assert.NotNull(packagePath);
        Assert.True(Directory.Exists(packagePath));
        Assert.Contains(packageId.ToLowerInvariant(), packagePath);
    }

    [Fact]
    public async Task GetPackageContentsAsync_WithValidPackage_ReturnsFileList()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";
        var version = "13.0.3";

        // Act
        var contents = await _nugetService.GetPackageContentsAsync(packageId, version);

        // Assert
        Assert.NotNull(contents);
        Assert.NotEmpty(contents);
        Assert.All(contents, f =>
        {
            Assert.NotNull(f.Path);
            Assert.NotNull(f.FullPath);
            Assert.True(f.Size >= 0);
        });
    }

    [Fact]
    public async Task GetPackageContentsAsync_WithLatestVersion_ReturnsFileList()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";

        // Act
        var contents = await _nugetService.GetPackageContentsAsync(packageId);

        // Assert
        Assert.NotNull(contents);
        Assert.NotEmpty(contents);
    }

    [Fact]
    public async Task ExtractPackageFileAsync_WithValidFile_ReturnsFileContent()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";
        var version = "13.0.3";
        var filePath = "lib/net45/Newtonsoft.Json.dll";

        // Act
        var content = await _nugetService.ExtractPackageFileAsync(packageId, filePath, version);

        // Assert
        // Note: This will return binary content for DLL files, so we just check it's not null
        Assert.NotNull(content);
    }

    [Fact]
    public async Task ExtractPackageFileAsync_WithNuspecFile_ReturnsXmlContent()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";
        var version = "13.0.3";
        var filePath = "Newtonsoft.Json.nuspec";

        // Act
        var content = await _nugetService.ExtractPackageFileAsync(packageId, filePath, version);

        // Assert
        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.Contains("<?xml", content);
        Assert.Contains(packageId, content);
    }

    [Fact]
    public async Task ExtractPackageFileAsync_WithInvalidFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var packageId = "Newtonsoft.Json";
        var version = "13.0.3";
        var invalidFilePath = "nonexistent/file.txt";

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await _nugetService.ExtractPackageFileAsync(packageId, invalidFilePath, version));
    }
}

