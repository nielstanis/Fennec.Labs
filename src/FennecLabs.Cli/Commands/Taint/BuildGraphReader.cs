using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace FennecLabs.Cli.Commands.Taint;

/// <summary>
/// Resolves `.csproj`, `.sln`, and `.slnx` inputs to the build output DLL(s) they
/// produce, so that `fennec instrument --taint` can accept project/solution files
/// alongside plain assembly paths.
/// </summary>
internal static class BuildGraphReader
{
    private static readonly string[] ConfigurationCandidates = ["Debug", "Release"];

    /// <summary>
    /// Resolves <paramref name="inputPath"/> (a `.csproj`, `.sln`, or `.slnx` file) to
    /// the absolute path(s) of its build output DLL(s).
    /// </summary>
    /// <exception cref="BuildOutputNotFoundException">
    /// Thrown when a project's build output DLL cannot be found.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown for unsupported file extensions.</exception>
    public static IReadOnlyList<string> Resolve(string inputPath)
    {
        var extension = Path.GetExtension(inputPath);
        return extension.ToLowerInvariant() switch
        {
            ".csproj" => [ResolveCsproj(inputPath)],
            ".sln" => ResolveSln(inputPath),
            ".slnx" => ResolveSlnx(inputPath),
            _ => throw new ArgumentException($"Unsupported input: {inputPath}"),
        };
    }

    /// <summary>Returns true when <paramref name="inputPath"/> is a project or solution file supported by <see cref="Resolve"/>.</summary>
    public static bool IsProjectOrSolution(string inputPath) =>
        Path.GetExtension(inputPath).ToLowerInvariant() is ".csproj" or ".sln" or ".slnx";

    private static string ResolveCsproj(string csprojPath)
    {
        var fullCsprojPath = Path.GetFullPath(csprojPath);
        if (!File.Exists(fullCsprojPath))
            throw BuildOutputNotFoundException.ForProject(csprojPath);

        var projectDir = Path.GetDirectoryName(fullCsprojPath)!;
        var assemblyName = Path.GetFileNameWithoutExtension(fullCsprojPath);
        string? explicitOutputPath = null;

        try
        {
            var document = XDocument.Load(fullCsprojPath);
            var ns = document.Root?.Name.Namespace ?? XNamespace.None;

            var assemblyNameElement = document.Descendants(ns + "AssemblyName").FirstOrDefault();
            if (assemblyNameElement is not null && !string.IsNullOrWhiteSpace(assemblyNameElement.Value))
                assemblyName = assemblyNameElement.Value.Trim();

            var outputPathElement = document.Descendants(ns + "OutputPath").FirstOrDefault();
            if (outputPathElement is not null && !string.IsNullOrWhiteSpace(outputPathElement.Value))
                explicitOutputPath = outputPathElement.Value.Trim();
        }
        catch (Exception ex) when (ex is not BuildOutputNotFoundException)
        {
            // Fall back to the bin/ heuristic search below if the csproj can't be parsed as XML.
        }

        if (!string.IsNullOrEmpty(explicitOutputPath))
        {
            var candidate = Path.GetFullPath(Path.Combine(projectDir, explicitOutputPath, $"{assemblyName}.dll"));
            if (File.Exists(candidate))
                return candidate;
        }

        var binDir = Path.Combine(projectDir, "bin");
        if (Directory.Exists(binDir))
        {
            var matches = ConfigurationCandidates
                .Select(configuration => Path.Combine(binDir, configuration))
                .Where(Directory.Exists)
                .SelectMany(configDir => Directory.EnumerateDirectories(configDir))
                .Select(tfmDir => Path.Combine(tfmDir, $"{assemblyName}.dll"))
                .Where(File.Exists)
                .Select(path => new FileInfo(path))
                .OrderByDescending(fileInfo => fileInfo.LastWriteTimeUtc)
                .ToList();

            if (matches.Count > 0)
                return matches[0].FullName;
        }

        throw BuildOutputNotFoundException.ForProject(csprojPath);
    }

    private static IReadOnlyList<string> ResolveSln(string slnPath)
    {
        var fullSlnPath = Path.GetFullPath(slnPath);
        if (!File.Exists(fullSlnPath))
            throw BuildOutputNotFoundException.ForProject(slnPath);

        var slnDir = Path.GetDirectoryName(fullSlnPath)!;
        var projectLinePattern = new Regex(
            "^Project\\(\"\\{[0-9A-Fa-f-]+\\}\"\\)\\s*=\\s*\"[^\"]*\"\\s*,\\s*\"(?<path>[^\"]+\\.csproj)\"",
            RegexOptions.Multiline);

        var csprojPaths = projectLinePattern
            .Matches(File.ReadAllText(fullSlnPath))
            .Select(match => match.Groups["path"].Value.Replace('\\', Path.DirectorySeparatorChar))
            .Select(relativePath => Path.GetFullPath(Path.Combine(slnDir, relativePath)))
            .ToList();

        return ResolveAll(csprojPaths);
    }

    private static IReadOnlyList<string> ResolveSlnx(string slnxPath)
    {
        var fullSlnxPath = Path.GetFullPath(slnxPath);
        if (!File.Exists(fullSlnxPath))
            throw BuildOutputNotFoundException.ForProject(slnxPath);

        var slnxDir = Path.GetDirectoryName(fullSlnxPath)!;
        var document = XDocument.Load(fullSlnxPath);

        var csprojPaths = document.Descendants("Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(relativePath => Path.GetFullPath(Path.Combine(slnxDir, relativePath!.Replace('\\', Path.DirectorySeparatorChar))))
            .ToList();

        return ResolveAll(csprojPaths);
    }

    private static IReadOnlyList<string> ResolveAll(IReadOnlyList<string> csprojPaths) =>
        csprojPaths.Select(ResolveCsproj).ToList();
}
