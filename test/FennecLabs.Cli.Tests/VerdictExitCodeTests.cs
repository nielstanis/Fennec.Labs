using FennecLabs.Cli.Commands;

namespace FennecLabs.Cli.Tests;

public class VerdictExitCodeTests
{
    [Theory]
    [InlineData(3, 0, 0, 0, 0, 0)] // all identical, same DLL sets → reproducible
    [InlineData(2, 1, 0, 0, 0, 2)] // one DLL differs → not reproducible
    [InlineData(3, 0, 0, 1, 0, 2)] // extra DLL only in local → not reproducible
    [InlineData(3, 0, 0, 0, 1, 2)] // DLL only in feed → not reproducible
    [InlineData(2, 0, 1, 0, 0, 1)] // a DLL could not be compared → error
    [InlineData(0, 2, 1, 0, 0, 1)] // errors take precedence over differences
    [InlineData(0, 0, 0, 1, 1, 1)] // nothing compared → error
    public void ReproduceExitCode_FollowsContract(
        int identical, int different, int errors, int onlyInLocal, int onlyInFeed, int expected)
    {
        Assert.Equal(expected,
            DllPipeline.ReproduceExitCode(identical, different, errors, onlyInLocal, onlyInFeed));
    }

    [Fact]
    public void ReproduceExitCodeFromJson_CachedDifference_ReturnsNotReproducible()
    {
        const string json = """
            {"summary":{"identical":1,"different":1,"errors":0},"onlyInLocal":[],"onlyInFeed":[]}
            """;
        Assert.Equal(DllPipeline.ExitNotReproducible, DllPipeline.ReproduceExitCodeFromJson(json));
    }

    [Fact]
    public void ReproduceExitCodeFromJson_CachedOnlyInFeed_ReturnsNotReproducible()
    {
        const string json = """
            {"summary":{"identical":2,"different":0,"errors":0},"onlyInLocal":[],"onlyInFeed":["lib/net8.0/X.dll"]}
            """;
        Assert.Equal(DllPipeline.ExitNotReproducible, DllPipeline.ReproduceExitCodeFromJson(json));
    }

    [Fact]
    public void ReproduceExitCodeFromJson_CachedIdentical_ReturnsReproducible()
    {
        const string json = """
            {"summary":{"identical":2,"different":0,"errors":0},"onlyInLocal":[],"onlyInFeed":[]}
            """;
        Assert.Equal(DllPipeline.ExitReproducible, DllPipeline.ReproduceExitCodeFromJson(json));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"packageId":"X"}""")]
    public void ReproduceExitCodeFromJson_Malformed_ReturnsError(string json)
    {
        Assert.Equal(DllPipeline.ExitError, DllPipeline.ReproduceExitCodeFromJson(json));
    }

    [Theory]
    [InlineData("""{"summary":{"identical":1,"different":3,"errors":0}}""", 0)]
    [InlineData("""{"summary":{"identical":1,"different":0,"errors":2}}""", 1)]
    [InlineData("garbage", 1)]
    public void CompareExitCodeFromJson_MatchesFreshRunContract(string json, int expected)
    {
        Assert.Equal(expected, DllPipeline.CompareExitCodeFromJson(json));
    }

    [Fact]
    public async Task ComputeSha256Async_ReturnsLowercaseHexOfContent()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".nupkg");
        try
        {
            await File.WriteAllTextAsync(path, "abc");
            Assert.Equal(
                "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
                await ReproduceCommandHandler.ComputeSha256Async(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
