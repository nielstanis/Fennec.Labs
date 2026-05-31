using System.IO.Compression;
using FennecLabs.NuGet;

namespace FennecLabs.Cli;

internal static class NupkgHelper
{
    internal static async Task ExtractAsync(string nupkgPath, string extractPath)
    {
        var canonicalRoot = Path.GetFullPath(extractPath) + Path.DirectorySeparatorChar;

        using var fileStream = File.OpenRead(nupkgPath);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
                continue;

            var entryPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
            if (!entryPath.StartsWith(canonicalRoot, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Archive entry '{entry.FullName}' resolves outside extraction root.");

            var entryDir = Path.GetDirectoryName(entryPath);
            if (!string.IsNullOrEmpty(entryDir))
                Directory.CreateDirectory(entryDir);

            using var entryStream = entry.Open();
            using var outStream = File.Create(entryPath);
            await entryStream.CopyToAsync(outStream);
        }
    }

    internal static Dictionary<string, PackageFileInfo> GetDlls(string dir) =>
        Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories)
            .Select(f => new { Relative = Path.GetRelativePath(dir, f), Full = f })
            .Where(f => !f.Relative.Contains("_._"))
            .ToDictionary(
                f => f.Relative,
                f => new PackageFileInfo
                {
                    Path = f.Relative,
                    FullPath = f.Full,
                    Size = new FileInfo(f.Full).Length,
                });
}
