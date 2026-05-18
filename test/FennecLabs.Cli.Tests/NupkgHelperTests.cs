using System.IO.Compression;

namespace FennecLabs.Cli.Tests;

public class NupkgHelperTests
{
    [Fact]
    public void GetDlls_ReturnsRelativePathAsKey()
    {
        var dir = CreateTempDir();
        try
        {
            var sub = Path.Combine(dir, "lib", "net10.0");
            Directory.CreateDirectory(sub);
            File.WriteAllBytes(Path.Combine(sub, "Foo.dll"), []);

            var result = NupkgHelper.GetDlls(dir);

            var expectedKey = Path.Combine("lib", "net10.0", "Foo.dll");
            Assert.True(result.ContainsKey(expectedKey));
            Assert.Equal(expectedKey, result[expectedKey].Path);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void GetDlls_ExcludesUnderscoreDotUnderscoreFiles()
    {
        var dir = CreateTempDir();
        try
        {
            var sub = Path.Combine(dir, "lib", "net10.0");
            Directory.CreateDirectory(sub);
            File.WriteAllBytes(Path.Combine(sub, "_._"), []);

            Assert.Empty(NupkgHelper.GetDlls(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void GetDlls_ExcludesNonDllFiles()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "readme.txt"), []);
            File.WriteAllBytes(Path.Combine(dir, "package.nuspec"), []);

            Assert.Empty(NupkgHelper.GetDlls(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void GetDlls_ReturnsEmpty_ForEmptyDirectory()
    {
        var dir = CreateTempDir();
        try
        {
            Assert.Empty(NupkgHelper.GetDlls(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public async Task ExtractAsync_ExtractsEntriesToExpectedPaths()
    {
        var tempDir = CreateTempDir();
        try
        {
            var nupkgPath = Path.Combine(tempDir, "test.nupkg");
            var extractDir = Path.Combine(tempDir, "extracted");
            Directory.CreateDirectory(extractDir);

            using (var zip = ZipFile.Open(nupkgPath, ZipArchiveMode.Create))
            {
                using var stream = zip.CreateEntry("lib/net10.0/Foo.dll").Open();
                stream.Write([0x4D, 0x5A]);
            }

            await NupkgHelper.ExtractAsync(nupkgPath, extractDir);

            Assert.True(File.Exists(Path.Combine(extractDir, "lib", "net10.0", "Foo.dll")));
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        return dir;
    }
}
