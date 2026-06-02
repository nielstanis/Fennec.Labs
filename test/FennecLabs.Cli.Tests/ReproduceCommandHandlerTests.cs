using FennecLabs.Cli.Commands;

namespace FennecLabs.Cli.Tests;

public class ReproduceCommandHandlerTests : IDisposable
{
    private readonly string _root;

    public ReproduceCommandHandlerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // ── Case 1: explicit --tfm ────────────────────────────────────────────────

    [Fact]
    public void ExplicitTfm_ReturnsItUnchanged()
    {
        var (dir, tfm, error) = ReproduceCommandHandler.ResolveTfmDirectory(_root, "net8.0", isInteractive: false);
        Assert.Null(error);
        Assert.Equal("net8.0", tfm);
        Assert.Equal(_root, dir);
    }

    // ── Case 2: directory name is a TFM ──────────────────────────────────────

    [Theory]
    [InlineData("net8.0")]
    [InlineData("net6.0")]
    [InlineData("net48")]
    [InlineData("net8.0-windows")]
    [InlineData("netstandard2.0")]
    public void DirNameIsTfm_Derived(string tfmName)
    {
        var tfmDir = Path.Combine(_root, tfmName);
        Directory.CreateDirectory(tfmDir);
        var (dir, tfm, error) = ReproduceCommandHandler.ResolveTfmDirectory(tfmDir, null, isInteractive: false);
        Assert.Null(error);
        Assert.Equal(tfmName, tfm);
        Assert.Equal(tfmDir, dir);
    }

    // ── Case 3: single TFM subdir ─────────────────────────────────────────────

    [Fact]
    public void SingleTfmSubdir_AutoSelected()
    {
        var sub = Path.Combine(_root, "net8.0");
        Directory.CreateDirectory(sub);
        var (dir, tfm, error) = ReproduceCommandHandler.ResolveTfmDirectory(_root, null, isInteractive: false);
        Assert.Null(error);
        Assert.Equal("net8.0", tfm);
        Assert.Equal(sub, dir);
    }

    // ── Case A: multiple TFM subdirs, non-interactive ─────────────────────────

    [Fact]
    public void MultipleTfmSubdirs_NonInteractive_ReturnsError()
    {
        Directory.CreateDirectory(Path.Combine(_root, "net6.0"));
        Directory.CreateDirectory(Path.Combine(_root, "net8.0"));
        var (_, tfm, error) = ReproduceCommandHandler.ResolveTfmDirectory(_root, null, isInteractive: false);
        Assert.Null(tfm);
        Assert.NotNull(error);
        Assert.Contains("net6.0", error);
        Assert.Contains("net8.0", error);
        Assert.Contains("--tfm", error);
    }

    [Fact]
    public void MultipleTfmSubdirs_WithExplicitTfm_NoError()
    {
        Directory.CreateDirectory(Path.Combine(_root, "net6.0"));
        Directory.CreateDirectory(Path.Combine(_root, "net8.0"));
        var (_, tfm, error) = ReproduceCommandHandler.ResolveTfmDirectory(_root, "net8.0", isInteractive: false);
        Assert.Null(error);
        Assert.Equal("net8.0", tfm);
    }

    // ── Case B: no TFM identifiable ───────────────────────────────────────────

    [Fact]
    public void NoTfmIdentifiable_NoSubdirs_ReturnsHardError()
    {
        var (_, tfm, error) = ReproduceCommandHandler.ResolveTfmDirectory(_root, null, isInteractive: false);
        Assert.Null(tfm);
        Assert.NotNull(error);
        Assert.Contains("Cannot determine target framework", error);
        Assert.Contains("--tfm", error);
    }

    [Fact]
    public void NoTfmIdentifiable_NonTfmSubdirOnly_ReturnsHardError()
    {
        Directory.CreateDirectory(Path.Combine(_root, "en-US"));
        var (_, tfm, error) = ReproduceCommandHandler.ResolveTfmDirectory(_root, null, isInteractive: false);
        Assert.Null(tfm);
        Assert.NotNull(error);
        Assert.Contains("Cannot determine target framework", error);
    }

    [Fact]
    public void NoTfmIdentifiable_ExplicitTfmProvided_NoError()
    {
        var (_, tfm, error) = ReproduceCommandHandler.ResolveTfmDirectory(_root, "net8.0", isInteractive: false);
        Assert.Null(error);
        Assert.Equal("net8.0", tfm);
    }
}
