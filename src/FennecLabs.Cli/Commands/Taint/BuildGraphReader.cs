using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FennecLabs.Cli.Commands.Taint;

/// <summary>
/// Resolves project/solution input files (<c>.csproj</c>, <c>.sln</c>, <c>.slnx</c>) to their
/// built output assembly (<c>.dll</c>) paths, so <c>instrument --taint</c> can analyze a project
/// or solution without the caller having to point directly at a build output DLL.
/// </summary>
internal static partial class BuildGraphReader
{
    /// <summary>
    /// Resolves <paramref name="inputPath"/> to one or more absolute build output DLL paths.
    /// Accepts a <c>.dll</c> path directly (returned as-is), or a <c>.csproj</c>/<c>.sln</c>/
    /// <c>.slnx</c> file whose project(s) are resolved to their most recently built output.
    /// </summary>
    /// <exception cref="BuildOutputNotFoundException">
    /// Thrown when a project has no discoverable build output under its <c>bin/</c> folder.
    /// </exception>
    public static IReadOnlyList<string> Resolve(string inputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var fullPath = Path.GetFullPath(inputPath);
        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".dll" => [fullPath],
            ".csproj" => [ResolveCsproj(fullPath)],
            ".sln" => ResolveSln(fullPath),
            ".slnx" => ResolveSlnx(fullPath),
            var ext => throw new ArgumentException($"Unsupported input type '{ext}': {inputPath}"),
        };
    }

    /// <summary>Returns whether <paramref name="path"/> is a project/solution input (not a raw assembly).</summary>
    public static bool IsProjectInput(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".csproj" or ".sln" or ".slnx";

    private static string ResolveCsproj(string csprojPath)
    {
        if (!File.Exists(csprojPath))
            throw new BuildOutputNotFoundException(csprojPath);

        var projectDir = Path.GetDirectoryName(csprojPath)!;
        var assemblyName = ReadAssemblyName(csprojPath) ?? Path.GetFileNameWithoutExtension(csprojPath);

        var binDir = Path.Combine(projectDir, "bin");
        if (!Directory.Exists(binDir))
            throw new BuildOutputNotFoundException(csprojPath);

        var candidates = Directory.EnumerateFiles(binDir, $"{assemblyName}.dll", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderByDescending(fi => fi.LastWriteTimeUtc)
            .ToList();

        if (candidates.Count == 0)
            throw new BuildOutputNotFoundException(csprojPath);

        return candidates[0].FullName;
    }

    private static IReadOnlyList<string> ResolveSln(string slnPath)
    {
        var slnDir = Path.GetDirectoryName(slnPath)!;
        var projectPaths = new List<string>();

        foreach (var line in File.ReadLines(slnPath))
        {
            var match = SlnProjectLine().Match(line);
            if (!match.Success)
                continue;

            var relativePath = match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar);
            if (!relativePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                continue;

            projectPaths.Add(Path.GetFullPath(Path.Combine(slnDir, relativePath)));
        }

        if (projectPaths.Count == 0)
            throw new BuildOutputNotFoundException(slnPath);

        return projectPaths.Select(ResolveCsproj).ToList();
    }

    private static IReadOnlyList<string> ResolveSlnx(string slnxPath)
    {
        var slnxDir = Path.GetDirectoryName(slnxPath)!;
        var document = XDocument.Load(slnxPath);

        var projectPaths = document
            .Descendants("Project")
            .Select(el => el.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(Path.Combine(slnxDir, path!.Replace('\\', Path.DirectorySeparatorChar))))
            .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (projectPaths.Count == 0)
            throw new BuildOutputNotFoundException(slnxPath);

        return projectPaths.Select(ResolveCsproj).ToList();
    }

    private static string? ReadAssemblyName(string csprojPath)
    {
        try
        {
            var document = XDocument.Load(csprojPath);
            return document.Descendants("AssemblyName").FirstOrDefault()?.Value;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    [GeneratedRegex("^Project\\(\".*?\"\\)\\s*=\\s*\".*?\",\\s*\"(?<path>[^\"]+)\"")]
    private static partial Regex SlnProjectLine();
}
