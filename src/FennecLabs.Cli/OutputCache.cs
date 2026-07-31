namespace FennecLabs.Cli;

internal static class OutputCache
{
    internal static string ComparePath(string root, string packageId, string current, string previous) =>
        Path.Combine(root, "compare", packageId, $"{current}-vs-{previous}", "result.json");

    internal static string ReproducePath(string root, string packageId, string version) =>
        Path.Combine(root, "reproduce", packageId, version, "result.json");

    internal static string ScorecardDir(string root, string projectName, string timestamp) =>
        Path.Combine(root, "scorecard", projectName, timestamp);

    internal static string DependenciesDir(string root, string projectName, string timestamp) =>
        Path.Combine(root, "dependencies", projectName, timestamp);

    // Placeholder for Story 1.4, which will generate a sha256-based runId. Any string
    // may be passed as runId until then.
    internal static string TaintDir(string root, string scope, string runId) =>
        Path.Combine(root, "instrument", scope, "taint", runId);

    internal static bool Exists(string path) => File.Exists(path);

    internal static string? TryLoad(string path) =>
        File.Exists(path) ? File.ReadAllText(path) : null;

    internal static async Task WriteAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }
}
