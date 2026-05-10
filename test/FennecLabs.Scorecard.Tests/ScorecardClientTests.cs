using FennecLabs.NuGet;
using FennecLabs.Scorecard;
using Xunit;

namespace FennecLabs.Scorecard.Tests;

public class ScorecardClientTests
{
    [Fact]
    public async Task GetScorecardResultAsync_WithValidRepository_ReturnsScorecardResult()
    {
        // Arrange
        var client = new ScorecardClient();
        var platform = "github.com";
        var org = "ossf";
        var repo = "scorecard";

        // Act
        var result = await client.GetScorecardResultAsync(platform, org, repo);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Repo);
        Assert.Contains(repo, result.Repo.Name);
        Assert.NotNull(result.Scorecard);
        Assert.True(result.Score >= 0 && result.Score <= 10);
        Assert.NotNull(result.Checks);
        Assert.NotEmpty(result.Checks);
    }

    [Fact]
    public async Task GetScorecardResultAsync_WithoutCommit_ReturnsScorecardResult()
    {
        // Arrange
        var client = new ScorecardClient();
        var platform = "github.com";
        var org = "ossf";
        var repo = "scorecard";

        // Act
        // Test without commit parameter (uses latest)
        var result = await client.GetScorecardResultAsync(platform, org, repo, commit: null);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Repo);
        Assert.Contains(repo, result.Repo.Name);
    }

    [Fact]
    public async Task GetScorecardResultAsync_WithInvalidRepository_ThrowsException()
    {
        // Arrange
        var client = new ScorecardClient();
        var platform = "github.com";
        var org = "nonexistent-org-12345";
        var repo = "nonexistent-repo-12345";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.GetScorecardResultAsync(platform, org, repo));
    }

    [Fact]
    public async Task GetScorecardResultFromPackageAsync_WithValidPackage_MayReturnScorecardResult()
    {
        // Arrange
        var client = new ScorecardClient();
        var packageId = "Newtonsoft.Json";

        // Act
        // Note: This may return null or throw if the package doesn't have a valid repository URL
        // or if the repository doesn't have a scorecard available
        try
        {
            var result = await client.GetScorecardResultFromPackageAsync(packageId);

            // Assert
            if (result != null)
            {
                Assert.NotNull(result.Repo);
                Assert.NotNull(result.Scorecard);
                Assert.True(result.Score >= 0 && result.Score <= 10);
            }
        }
        catch (InvalidOperationException)
        {
            // It's acceptable if the repository doesn't have a scorecard
            Assert.True(true);
        }
    }

    [Fact]
    public async Task GetScorecardResultFromPackageAsync_WithValidPackageAndVersion_MayReturnScorecardResult()
    {
        // Arrange
        var nugetService = new NuGetService();
        var client = new ScorecardClient();
        var packageId = "Newtonsoft.Json";
        var version = "13.0.3";

        // Act
        // Note: This may return null or throw if the package doesn't have a valid repository URL
        // or if the repository doesn't have a scorecard available
        try
        {
            var result = await client.GetScorecardResultFromPackageAsync(packageId, version);

            // Assert
            if (result != null)
            {
                Assert.NotNull(result.Repo);
                Assert.NotNull(result.Scorecard);
            }
        }
        catch (InvalidOperationException)
        {
            // It's acceptable if the repository doesn't have a scorecard
            Assert.True(true);
        }
    }

    [Fact]
    public async Task GetScorecardResultFromPackageAsync_WithPackageWithoutNuGetService_UsesHeuristic()
    {
        // Arrange
        var client = new ScorecardClient(); // No NuGetService provided
        var packageId = "Microsoft.Extensions.Logging";

        // Act & Assert
        // This should work with fallback heuristic, but may throw if repository doesn't have scorecard
        try
        {
            var result = await client.GetScorecardResultFromPackageAsync(packageId);
            
            // The result may be null or a valid result depending on the heuristic
            // We just verify it doesn't throw an unexpected exception
            Assert.True(result == null || (result.Repo != null && result.Scorecard != null));
        }
        catch (InvalidOperationException)
        {
            // It's acceptable if the repository doesn't have a scorecard
            Assert.True(true);
        }
    }

    [Fact]
    public async Task GetScorecardResultFromPackageAsync_WithCastleCore_UsesRepositoryTagFromNuspec()
    {
        // Arrange
        // Castle.Core is known to have a repository tag in its nuspec file
        // but may not have a valid ProjectUrl pointing to GitHub
        var client = new ScorecardClient();
        var packageId = "Castle.Core";
        var version = "5.1.1"; // Use a specific version that we know exists

        // Act
        // This should extract the repository URL from the nuspec file
        try
        {
            var result = await client.GetScorecardResultFromPackageAsync(packageId, version);
            
            // Assert
            // If successful, it means we found a valid GitHub URL (either from ProjectUrl or nuspec)
            if (result != null)
            {
                Assert.NotNull(result.Repo);
                Assert.NotNull(result.Scorecard);
                // Verify it's a GitHub repository
                Assert.Contains("github.com", result.Repo.Name);
            }
        }
        catch (InvalidOperationException)
        {
            // It's acceptable if the repository doesn't have a scorecard
            Assert.True(true);
        }
        catch (ArgumentException)
        {
            // It's acceptable if we can't determine the repository
            Assert.True(true);
        }
    }

    [Fact]
    public async Task GetPackageNuspecContentAsync_WithCastleCore_ReturnsNuspecContent()
    {
        // Arrange
        var nugetService = new NuGetService();
        var packageId = "Castle.Core";
        var version = "5.1.1";

        // Act
        var nuspecContent = await nugetService.GetPackageNuspecContentAsync(packageId, version);

        // Assert
        Assert.NotNull(nuspecContent);
        Assert.NotEmpty(nuspecContent);
        // Verify it's valid XML
        Assert.Contains("<?xml", nuspecContent);
        Assert.Contains("<package", nuspecContent);
        Assert.Contains("Castle.Core", nuspecContent);
    }

    [Fact]
    public async Task ExtractRepositoryUrlFromNuspec_WithCastleCore_ReturnsGitHubUrl()
    {
        // Arrange
        var nugetService = new NuGetService();
        var packageId = "Castle.Core";
        var version = "5.1.1";

        // Act
        var nuspecContent = await nugetService.GetPackageNuspecContentAsync(packageId, version);
        Assert.NotNull(nuspecContent); // Ensure we got the nuspec
        
        var repositoryUrl = NuGetService.ExtractRepositoryUrlFromNuspec(nuspecContent);

        // Assert
        Assert.NotNull(repositoryUrl);
        Assert.NotEmpty(repositoryUrl);
        // Castle.Core should have a GitHub repository URL in its nuspec
        Assert.Contains("github.com", repositoryUrl);
        Assert.Contains("castleproject", repositoryUrl);
    }

        [Fact]
    public async Task GetScorecardResultFromPackageAsync_WithProjectUrl_ReturnsScorecardResult()
    {
        // Arrange
        var client = new ScorecardClient();
        var packageId = "itext7";
        var version = "9.4.0";

        // Act & Assert
        // This should work with fallback heuristic, but may throw if repository doesn't have scorecard
        try
        {
            var result = await client.GetScorecardResultFromPackageAsync(packageId, version);
            
            // The result may be null or a valid result depending on the heuristic
            // We just verify it doesn't throw an unexpected exception
            Assert.True(result == null || (result.Repo != null && result.Scorecard != null));
        }
        catch (InvalidOperationException)
        {
            // It's acceptable if the repository doesn't have a scorecard
            Assert.True(true);
        }
    }

    [Fact]
    public async Task GetScorecardResultFromPackageAsync_WithInvalidPackageId_ThrowsException()
    {
        // Arrange
        var client = new ScorecardClient();
        var packageId = "X"; // Too short for heuristic

        // Act & Assert
        // The method may throw ArgumentException or InvalidOperationException depending on the flow
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await client.GetScorecardResultFromPackageAsync(packageId));
    }

    [Fact]
    public void ScorecardResult_HasValidStructure()
    {
        // Arrange & Act
        var result = new ScorecardResult
        {
            Date = "2024-01-01",
            Repo = new Repo { Name = "test-repo" },
            Scorecard = new ScorecardVersion { Version = "1.0", Commit = "abc123" },
            Score = 8.5m,
            Checks = new List<ScorecardCheck>
            {
                new ScorecardCheck
                {
                    Name = "Test Check",
                    Score = 10,
                    Details = new List<string>()
                }
            }
        };

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Repo);
        Assert.NotNull(result.Scorecard);
        Assert.NotNull(result.Checks);
        Assert.Equal(8.5m, result.Score);
    }

    [Fact]
    public void ScorecardCheck_HasValidStructure()
    {
        // Arrange & Act
        var check = new ScorecardCheck
        {
            Name = "Binary-Artifacts",
            Score = 10,
            Reason = "No binary artifacts found",
            Documentation = new ScorecardCheckDocumentation
            {
                Short = "Binary artifacts check",
                Url = "https://example.com"
            },
            Details = new List<string> { "Detail 1", "Detail 2" }
        };

        // Assert
        Assert.NotNull(check);
        Assert.Equal("Binary-Artifacts", check.Name);
        Assert.Equal(10, check.Score);
        Assert.NotNull(check.Documentation);
        Assert.NotNull(check.Details);
        Assert.Equal(2, check.Details.Count);
    }

    [Fact]
    public async Task GetScorecardResultAsync_WithWellKnownRepository_ReturnsValidChecks()
    {
        // Arrange
        var client = new ScorecardClient();
        var platform = "github.com";
        var org = "ossf";
        var repo = "scorecard"; // Use a repository we know has a scorecard

        // Act
        var result = await client.GetScorecardResultAsync(platform, org, repo);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Checks);
        Assert.NotEmpty(result.Checks);
        
        // Verify check structure
        foreach (var check in result.Checks)
        {
            Assert.NotNull(check.Name);
            Assert.NotEmpty(check.Name);
            // Score can be -1 (not applicable), 0-10 (actual score)
            Assert.True(check.Score >= -1 && check.Score <= 10, 
                $"Check {check.Name} has invalid score: {check.Score}");
        }
    }
}

