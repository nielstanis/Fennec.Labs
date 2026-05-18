using FennecLabs.Cli.Commands;

namespace FennecLabs.Cli.Tests;

public class CompareLocalFilesHandlerTests
{
    private readonly CompareLocalFilesCommandHandler _handler = new();

    // --- Validation paths (no real files needed for the first, temp files for the rest) ---

    [Fact]
    public async Task ExecuteAsync_ReturnsOne_WhenFile1NotFound()
    {
        var result = await _handler.ExecuteAsync("nonexistent1.dll", "nonexistent2.dll", OutputMode.Json);
        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOne_WhenFile2NotFound()
    {
        var f1 = CreateTempFile(".dll");
        try
        {
            var result = await _handler.ExecuteAsync(f1, "nonexistent.dll", OutputMode.Json);
            Assert.Equal(1, result);
        }
        finally { File.Delete(f1); }
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOne_WhenExtensionsMixed()
    {
        var dll = CreateTempFile(".dll");
        var nupkg = CreateTempFile(".nupkg");
        try
        {
            var result = await _handler.ExecuteAsync(dll, nupkg, OutputMode.Json);
            Assert.Equal(1, result);
        }
        finally { File.Delete(dll); File.Delete(nupkg); }
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsOne_WhenExtensionInvalid()
    {
        var f1 = CreateTempFile(".exe");
        var f2 = CreateTempFile(".exe");
        try
        {
            var result = await _handler.ExecuteAsync(f1, f2, OutputMode.Json);
            Assert.Equal(1, result);
        }
        finally { File.Delete(f1); File.Delete(f2); }
    }

    // --- Integration: real DLL comparison ---

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ExecuteAsync_IdenticalDlls_ReturnsZero()
    {
        var dllPath = typeof(CompareLocalFilesCommandHandler).Assembly.Location;
        var savedOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            var result = await _handler.ExecuteAsync(dllPath, dllPath, OutputMode.Json);
            Assert.Equal(0, result);
        }
        finally { Console.SetOut(savedOut); }
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ExecuteAsync_DifferentDlls_ReturnsZero()
    {
        var dll1 = typeof(CompareLocalFilesCommandHandler).Assembly.Location;
        var dll2 = typeof(CompareLocalFilesHandlerTests).Assembly.Location;
        var savedOut = Console.Out;
        Console.SetOut(TextWriter.Null);
        try
        {
            var result = await _handler.ExecuteAsync(dll1, dll2, OutputMode.Json);
            Assert.Equal(0, result);
        }
        finally { Console.SetOut(savedOut); }
    }

    private static string CreateTempFile(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        File.WriteAllBytes(path, []);
        return path;
    }
}
