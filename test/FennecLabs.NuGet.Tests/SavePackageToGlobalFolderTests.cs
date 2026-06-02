using System.IO.Compression;
using FennecLabs.NuGet;

namespace FennecLabs.NuGet.Tests;

public class SavePackageToGlobalFolderTests : IDisposable
{
    private readonly string _root;

    public SavePackageToGlobalFolderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void ValidArchive_ExtractsToExpectedPaths()
    {
        var extractDir = Path.Combine(_root, "pkg");
        Directory.CreateDirectory(extractDir);

        using var archive = BuildArchive(a =>
        {
            WriteEntry(a, "lib/net8.0/Foo.dll", [0x4D, 0x5A]);
            WriteEntry(a, "Foo.nuspec", "<package/>"u8.ToArray());
        });

        NuGetService.SavePackageToGlobalFolder(archive, extractDir);

        Assert.True(File.Exists(Path.Combine(extractDir, "lib", "net8.0", "Foo.dll")));
        Assert.True(File.Exists(Path.Combine(extractDir, "Foo.nuspec")));
    }

    [Fact]
    public void DirectoryEntry_Skipped_NoException()
    {
        var extractDir = Path.Combine(_root, "pkg");
        Directory.CreateDirectory(extractDir);

        using var archive = BuildArchive(a =>
        {
            // Directory entries have an empty Name
            a.CreateEntry("lib/net8.0/");
            WriteEntry(a, "lib/net8.0/Foo.dll", [0x4D, 0x5A]);
        });

        NuGetService.SavePackageToGlobalFolder(archive, extractDir);

        Assert.True(File.Exists(Path.Combine(extractDir, "lib", "net8.0", "Foo.dll")));
    }

    [Fact]
    public void SingleLevelTraversal_Throws_FileNotWritten()
    {
        var extractDir = Path.Combine(_root, "pkg");
        Directory.CreateDirectory(extractDir);

        using var archive = BuildArchive(a => WriteEntry(a, "../evil.txt", "pwned"u8.ToArray()));

        var ex = Assert.Throws<InvalidOperationException>(
            () => NuGetService.SavePackageToGlobalFolder(archive, extractDir));

        Assert.Contains("resolves outside extraction root", ex.Message);
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
    }

    [Fact]
    public void DeepTraversal_Throws_FileNotWritten()
    {
        var extractDir = Path.Combine(_root, "pkg");
        Directory.CreateDirectory(extractDir);

        using var archive = BuildArchive(a => WriteEntry(a, "lib/../../evil.txt", "pwned"u8.ToArray()));

        var ex = Assert.Throws<InvalidOperationException>(
            () => NuGetService.SavePackageToGlobalFolder(archive, extractDir));

        Assert.Contains("resolves outside extraction root", ex.Message);
        Assert.False(File.Exists(Path.Combine(_root, "evil.txt")));
    }

    [Fact]
    public void AbsolutePathEntry_Throws()
    {
        var extractDir = Path.Combine(_root, "pkg");
        Directory.CreateDirectory(extractDir);

        // Some zip tools write absolute paths; Path.GetFullPath will resolve these
        // to outside the extraction root on most platforms.
        using var archive = BuildArchive(a => WriteEntry(a, "/etc/passwd", "pwned"u8.ToArray()));

        Assert.Throws<InvalidOperationException>(
            () => NuGetService.SavePackageToGlobalFolder(archive, extractDir));
    }

    private static ZipArchive BuildArchive(Action<ZipArchive> populate)
    {
        var ms = new MemoryStream();
        using (var writer = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            populate(writer);
        ms.Position = 0;
        return new ZipArchive(ms, ZipArchiveMode.Read);
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name);
        using var stream = entry.Open();
        stream.Write(content);
    }
}
