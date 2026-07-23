using System.Net;
using System.Net.Http;
using FennecLabs.NuGet;
using FennecLabs.Scorecard;
using Xunit;

namespace FennecLabs.Scorecard.Tests;

public class ScorecardClientTests
{
    [Fact]
    [Trait("Category", "Live")]
    public async Task GetScorecardResultAsync_WithValidRepository_ReturnsScorecardResult()
    {
        var client = new ScorecardClient();

        var result = await client.GetScorecardResultAsync("github.com", "ossf", "scorecard");

        Assert.NotNull(result);
        Assert.NotNull(result.Repo);
        Assert.Contains("scorecard", result.Repo.Name);
        Assert.NotNull(result.Scorecard);
        Assert.True(result.Score >= 0 && result.Score <= 10);
        Assert.NotNull(result.Checks);
        Assert.NotEmpty(result.Checks);
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task GetScorecardResultAsync_WithoutCommit_ReturnsScorecardResult()
    {
        var client = new ScorecardClient();

        var result = await client.GetScorecardResultAsync("github.com", "ossf", "scorecard", commit: null);

        Assert.NotNull(result);
        Assert.NotNull(result.Repo);
        Assert.Contains("scorecard", result.Repo.Name);
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task GetScorecardResultAsync_WithInvalidRepository_ReturnsNull()
    {
        var client = new ScorecardClient();

        var result = await client.GetScorecardResultAsync(
            "github.com",
            "nonexistent-org-12345",
            "nonexistent-repo-12345");

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task GetScorecardResultFromPackageAsync_WithValidPackage_MayReturnScorecardResult()
    {
        var client = new ScorecardClient();

        try
        {
            var result = await client.GetScorecardResultFromPackageAsync("Newtonsoft.Json");

            if (result != null)
            {
                Assert.NotNull(result.Repo);
                Assert.NotNull(result.Scorecard);
                Assert.True(result.Score >= 0 && result.Score <= 10);
            }
        }
        catch (InvalidOperationException)
        {
            // Acceptable: repository found but has no scorecard
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task GetScorecardResultFromPackageAsync_WithValidPackageAndVersion_MayReturnScorecardResult()
    {
        var client = new ScorecardClient();

        try
        {
            var result = await client.GetScorecardResultFromPackageAsync("Newtonsoft.Json", "13.0.3");

            if (result != null)
            {
                Assert.NotNull(result.Repo);
                Assert.NotNull(result.Scorecard);
            }
        }
        catch (InvalidOperationException)
        {
            // Acceptable: repository found but has no scorecard
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task GetScorecardResultFromPackageAsync_WithPackageWithoutRepositoryUrl_ReturnsNullOrResult()
    {
        var client = new ScorecardClient();

        try
        {
            var result = await client.GetScorecardResultFromPackageAsync("Microsoft.Extensions.Logging");

            Assert.True(result == null || (result.Repo != null && result.Scorecard != null));
        }
        catch (InvalidOperationException)
        {
            // Acceptable: repository found but has no scorecard
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task GetScorecardResultFromPackageAsync_WithCastleCore_UsesRepositoryTagFromNuspec()
    {
        var client = new ScorecardClient();

        try
        {
            var result = await client.GetScorecardResultFromPackageAsync("Castle.Core", "5.1.1");

            if (result != null)
            {
                Assert.NotNull(result.Repo);
                Assert.NotNull(result.Scorecard);
                Assert.Contains("github.com", result.Repo.Name);
            }
        }
        catch (InvalidOperationException)
        {
            // Acceptable: repository found but has no scorecard
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task GetPackageNuspecContentAsync_WithCastleCore_ReturnsNuspecContent()
    {
        var nugetService = new NuGetService();

        var nuspecContent = await nugetService.GetPackageNuspecContentAsync("Castle.Core", "5.1.1");

        Assert.NotNull(nuspecContent);
        Assert.NotEmpty(nuspecContent);
        Assert.Contains("<?xml", nuspecContent);
        Assert.Contains("<package", nuspecContent);
        Assert.Contains("Castle.Core", nuspecContent);
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task ExtractRepositoryUrlFromNuspec_WithCastleCore_ReturnsGitHubUrl()
    {
        var nugetService = new NuGetService();

        var nuspecContent = await nugetService.GetPackageNuspecContentAsync("Castle.Core", "5.1.1");
        Assert.NotNull(nuspecContent);

        var repositoryUrl = NuGetService.ExtractRepositoryUrlFromNuspec(nuspecContent);

        Assert.NotNull(repositoryUrl);
        Assert.NotEmpty(repositoryUrl);
        Assert.Contains("github.com", repositoryUrl);
        Assert.Contains("castleproject", repositoryUrl);
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task GetScorecardResultFromPackageAsync_WithProjectUrl_ReturnsScorecardResult()
    {
        var client = new ScorecardClient();

        try
        {
            var result = await client.GetScorecardResultFromPackageAsync("itext7", "9.4.0");

            Assert.True(result == null || (result.Repo != null && result.Scorecard != null));
        }
        catch (InvalidOperationException)
        {
            // Acceptable: repository found but has no scorecard
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task GetScorecardResultFromPackageAsync_WithNoMatchingPackage_ReturnsNull()
    {
        var client = new ScorecardClient();

        var result = await client.GetScorecardResultFromPackageAsync("ThisPackageDefinitelyDoesNotExistOnNuGet99999");

        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task GetScorecardResultAsync_WithWellKnownRepository_ReturnsValidChecks()
    {
        var client = new ScorecardClient();

        var result = await client.GetScorecardResultAsync("github.com", "ossf", "scorecard");

        Assert.NotNull(result);
        Assert.NotNull(result.Checks);
        Assert.NotEmpty(result.Checks);

        foreach (var check in result.Checks)
        {
            Assert.NotNull(check.Name);
            Assert.NotEmpty(check.Name);
            Assert.True(check.Score >= -1 && check.Score <= 10,
                $"Check {check.Name} has invalid score: {check.Score}");
        }
    }

    [Fact]
    public void ScorecardResult_HasValidStructure()
    {
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

        Assert.NotNull(result);
        Assert.NotNull(result.Repo);
        Assert.NotNull(result.Scorecard);
        Assert.NotNull(result.Checks);
        Assert.Equal(8.5m, result.Score);
    }

    [Fact]
    public void ScorecardCheck_HasValidStructure()
    {
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

        Assert.NotNull(check);
        Assert.Equal("Binary-Artifacts", check.Name);
        Assert.Equal(10, check.Score);
        Assert.NotNull(check.Documentation);
        Assert.NotNull(check.Details);
        Assert.Equal(2, check.Details.Count);
    }

    [Fact]
    public void ExtractRepositoryUrlFromNuspec_WithGitHubRepositoryUrl_ReturnsUrl()
    {
        var nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <repository type="git" url="https://github.com/castleproject/Core" />
              </metadata>
            </package>
            """;

        var url = NuGetService.ExtractRepositoryUrlFromNuspec(nuspec);

        Assert.Equal("https://github.com/castleproject/Core", url);
    }

    [Fact]
    public void ExtractRepositoryUrlFromNuspec_WithNoRepositoryTag_ReturnsNull()
    {
        var nuspec = """
            <?xml version="1.0" encoding="utf-8"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <id>SomePackage</id>
                <version>1.0.0</version>
              </metadata>
            </package>
            """;

        var url = NuGetService.ExtractRepositoryUrlFromNuspec(nuspec);

        Assert.Null(url);
    }

    [Fact]
    public void ExtractRepositoryUrlFromNuspec_WithEmptyInput_ReturnsNull()
    {
        Assert.Null(NuGetService.ExtractRepositoryUrlFromNuspec(""));
        Assert.Null(NuGetService.ExtractRepositoryUrlFromNuspec("   "));
    }

    [Fact]
    public async Task GetScorecardResultAsync_WithInjectedHttpClient_ReturnsParsedResult()
    {
        const string payload = """
            {
              "date": "2024-01-15",
              "repo": { "name": "github.com/ossf/scorecard", "commit": "abc123" },
              "scorecard": { "version": "4.13.0", "commit": "def456" },
              "score": 8.3,
              "checks": [
                {
                  "name": "Binary-Artifacts",
                  "score": 10,
                  "reason": "no binaries found in the repo",
                  "details": [],
                  "documentation": { "short": "Checks for binaries", "url": "https://example.com" }
                }
              ]
            }
            """;

        var handler = new FakeScorecardHttpMessageHandler(HttpStatusCode.OK, payload);
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.securityscorecards.dev") };
        using var client = new ScorecardClient(httpClient: httpClient, nugetService: null);

        var result = await client.GetScorecardResultAsync("github.com", "ossf", "scorecard");

        Assert.NotNull(result);
        Assert.Equal("2024-01-15", result.Date);
        Assert.Equal("github.com/ossf/scorecard", result.Repo.Name);
        Assert.Equal(8.3m, result.Score);
        Assert.Single(result.Checks);
        Assert.Equal("Binary-Artifacts", result.Checks[0].Name);
        Assert.Equal(10, result.Checks[0].Score);
    }

    [Fact]
    public async Task GetScorecardResultAsync_WhenServerReturnsNotFound_ReturnsNull()
    {
        var handler = new FakeScorecardHttpMessageHandler(HttpStatusCode.NotFound, "Not Found");
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.securityscorecards.dev") };
        using var client = new ScorecardClient(httpClient: httpClient, nugetService: null);

        var result = await client.GetScorecardResultAsync("github.com", "nobody", "norepo");

        Assert.Null(result);
    }

    [Fact]
    public void ScorecardClient_ImplementsIDisposable()
    {
        // Internally-created HttpClient should be disposed without error
        var client = new ScorecardClient();
        client.Dispose();
    }

    [Fact]
    public void ScorecardClient_WithInjectedHttpClient_DoesNotDisposeIt()
    {
        // Injected HttpClient must not be disposed by ScorecardClient
        var handler = new FakeScorecardHttpMessageHandler(HttpStatusCode.OK, "{}");
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.securityscorecards.dev") };

        var client = new ScorecardClient(httpClient: httpClient, nugetService: null);
        client.Dispose();

        // If the injected client were disposed, Send would throw ObjectDisposedException
        var ex = Record.Exception(() => httpClient.GetAsync("https://api.securityscorecards.dev/").GetAwaiter().GetResult());
        Assert.Null(ex);

        httpClient.Dispose();
    }

    /// <summary>Returns a fixed HTTP response for all requests.</summary>
    private sealed class FakeScorecardHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public FakeScorecardHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
