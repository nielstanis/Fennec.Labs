namespace FennecLabs.Cli.Tests;

public class OutputCacheTests
{
    [Fact]
    public void ComparePath_BuildsCorrectPath()
    {
        var expected = Path.Combine(".fennec", "compare", "Newtonsoft.Json", "13.0.3-vs-13.0.2", "result.json");
        Assert.Equal(expected, OutputCache.ComparePath(".fennec", "Newtonsoft.Json", "13.0.3", "13.0.2"));
    }

    [Fact]
    public void ReproducePath_BuildsCorrectPath()
    {
        var expected = Path.Combine(".fennec", "reproduce", "Polly", "8.0.0", "result.json");
        Assert.Equal(expected, OutputCache.ReproducePath(".fennec", "Polly", "8.0.0"));
    }

    [Fact]
    public void ScorecardDir_BuildsCorrectPath()
    {
        var expected = Path.Combine(".fennec", "scorecard", "MyApp", "2026-01-01T00-00-00");
        Assert.Equal(expected, OutputCache.ScorecardDir(".fennec", "MyApp", "2026-01-01T00-00-00"));
    }

    [Fact]
    public void DependenciesDir_BuildsCorrectPath()
    {
        var expected = Path.Combine(".fennec", "dependencies", "MyApp", "2026-01-01_00-00-00");
        Assert.Equal(expected, OutputCache.DependenciesDir(".fennec", "MyApp", "2026-01-01_00-00-00"));
    }

    [Fact]
    public void TaintDir_BuildsCorrectPath()
    {
        var expected = Path.Combine(".fennec", "instrument", "MyApp", "taint", "abc123");
        Assert.Equal(expected, OutputCache.TaintDir(".fennec", "MyApp", "abc123"));
    }

    [Fact]
    public async Task WriteAsync_ThenExists_ReturnsTrue()
    {
        var path = UniqueTempPath("result.json");
        try
        {
            await OutputCache.WriteAsync(path, "{}");
            Assert.True(OutputCache.Exists(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task WriteAsync_ThenTryLoad_ReturnsContent()
    {
        var path = UniqueTempPath("result.json");
        const string content = """{"test":true}""";
        try
        {
            await OutputCache.WriteAsync(path, content);
            Assert.Equal(content, OutputCache.TryLoad(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TryLoad_WhenFileAbsent_ReturnsNull()
    {
        var path = UniqueTempPath("missing.json");
        Assert.Null(OutputCache.TryLoad(path));
    }

    [Fact]
    public void Exists_WhenFileAbsent_ReturnsFalse()
    {
        var path = UniqueTempPath("missing.json");
        Assert.False(OutputCache.Exists(path));
    }

    private static string UniqueTempPath(string filename) =>
        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), filename);
}
