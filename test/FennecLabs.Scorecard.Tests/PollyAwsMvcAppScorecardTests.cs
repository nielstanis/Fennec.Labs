using System.IO;
using System.Linq;
using System.Text.Json;
using FennecLabs.DotNetCli;
using FennecLabs.TestUtilities;

namespace FennecLabs.Scorecard.Tests;

/// <summary>
/// Offline tests that validate committed fixture JSON in TestData/.
/// No network access required. Run with: dotnet test --filter "Category!=Live"
/// To refresh fixtures after a package version bump, re-run the live tests and copy the
/// generated .fennec files back into TestData/.
/// </summary>
public class PollyAwsMvcAppScorecardOfflineTests
{
    private static readonly string TestDataDir =
        Path.Combine(AppContext.BaseDirectory, "TestData");

    [Fact]
    public void CachedScorecards_DeserializeCorrectly()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var polly = JsonSerializer.Deserialize<ScorecardResult>(
            File.ReadAllText(Path.Combine(TestDataDir, "Polly-8.6.6-fsc.json")), options);
        var aws = JsonSerializer.Deserialize<ScorecardResult>(
            File.ReadAllText(Path.Combine(TestDataDir, "AWSSDK.Core-4.0.6.1-fsc.json")), options);

        Assert.NotNull(polly);
        Assert.NotNull(aws);

        foreach (var result in new[] { polly, aws })
        {
            Assert.True(result.Score >= 0 && result.Score <= 10,
                $"Score {result.Score} out of range for {result.Repo.Name}");
            Assert.NotEmpty(result.Checks);
            foreach (var check in result.Checks)
                Assert.True(check.Score >= -1 && check.Score <= 10,
                    $"Check '{check.Name}' score {check.Score} out of range");
        }
    }

    [Fact]
    public void CachedScorecards_HaveExpectedRepos()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var polly = JsonSerializer.Deserialize<ScorecardResult>(
            File.ReadAllText(Path.Combine(TestDataDir, "Polly-8.6.6-fsc.json")), options)!;
        var aws = JsonSerializer.Deserialize<ScorecardResult>(
            File.ReadAllText(Path.Combine(TestDataDir, "AWSSDK.Core-4.0.6.1-fsc.json")), options)!;

        Assert.Contains("App-vNext", polly.Repo.Name, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aws", aws.Repo.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CachedScorecards_HaveExpectedCheckCounts()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var polly = JsonSerializer.Deserialize<ScorecardResult>(
            File.ReadAllText(Path.Combine(TestDataDir, "Polly-8.6.6-fsc.json")), options)!;
        var aws = JsonSerializer.Deserialize<ScorecardResult>(
            File.ReadAllText(Path.Combine(TestDataDir, "AWSSDK.Core-4.0.6.1-fsc.json")), options)!;

        Assert.Equal(18, polly.Checks.Count);
        Assert.Equal(14, aws.Checks.Count);
    }
}

/// <summary>
/// Live integration tests that call api.securityscorecards.dev and write results to
/// .fennec/ next to PollyAwsMvcApp.csproj (gitignored, regenerated each run).
/// Excluded from CI with: dotnet test --filter "Category!=Live"
/// </summary>
[Trait("Category", "Live")]
public class PollyAwsMvcAppScorecardLiveTests
{
    private static readonly string PollyAwsMvcAppProject =
        TestResources.GetTestProjectCsprojPath("PollyAwsMvcApp");

    private static readonly string FennecDir =
        Path.Combine(Path.GetDirectoryName(PollyAwsMvcAppProject)!, ".fennec");

    [Fact]
    public async Task GetScorecards_ForAllTopLevelPackages_WritesJsonToFennecDir()
    {
        Directory.CreateDirectory(FennecDir);

        var packageList = await DotnetCliExecutor.GetPackageListAsync(PollyAwsMvcAppProject);
        Assert.NotNull(packageList);
        var topLevel = packageList.Projects[0].Frameworks[0].TopLevelPackages;
        Assert.NotEmpty(topLevel);

        var client = new ScorecardClient();
        var options = new JsonSerializerOptions { WriteIndented = true };

        foreach (var package in topLevel)
        {
            var scorecard = await client.GetScorecardResultFromPackageAsync(
                package.Id, package.ResolvedVersion);

            Assert.NotNull(scorecard);

            var fileName = $"{package.Id}-{package.ResolvedVersion}-fsc.json";
            var filePath = Path.Combine(FennecDir, fileName);
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(scorecard, options));

            Assert.True(File.Exists(filePath));
            Assert.True(new FileInfo(filePath).Length > 0);
        }

        var writtenFiles = Directory.GetFiles(FennecDir, "*-fsc.json");
        Assert.Equal(topLevel.Count, writtenFiles.Length);
    }

    [Fact]
    public async Task GetScorecards_ForPolly_ReturnsValidScorecard()
    {
        var client = new ScorecardClient();
        var result = await client.GetScorecardResultFromPackageAsync("Polly", "8.6.6");

        Assert.NotNull(result);
        Assert.True(result.Score >= 0 && result.Score <= 10);
        Assert.Contains("App-vNext", result.Repo.Name, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Checks);
        foreach (var check in result.Checks)
            Assert.True(check.Score >= -1 && check.Score <= 10);
    }

    [Fact]
    public async Task GetScorecards_ForAwsSdkCore_ReturnsValidScorecard()
    {
        var client = new ScorecardClient();
        var result = await client.GetScorecardResultFromPackageAsync("AWSSDK.Core", "4.0.6.1");

        Assert.NotNull(result);
        Assert.True(result.Score >= 0 && result.Score <= 10);
        Assert.Contains("aws", result.Repo.Name, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Checks);
    }
}
