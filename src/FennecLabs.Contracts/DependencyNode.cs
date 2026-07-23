namespace FennecLabs.Contracts;

/// <summary>
/// A single normalized package identity within a canonical dependency graph payload. Per
/// architecture decision AD-7, this shape is stable regardless of upstream
/// <c>dotnet package list --include-transitive --format json</c> output changes.
/// </summary>
public sealed record DependencyNode
{
    /// <summary>
    /// Stable package identity: the NuGet package ID normalized to lowercase invariant culture,
    /// so the same package is never represented under two different identities due to casing.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Resolved version of the package as reported by the upstream dependency source.</summary>
    public required string ResolvedVersion { get; init; }

    /// <summary>Version range/constraint originally requested, when known.</summary>
    public string? RequestedVersion { get; init; }

    /// <summary>
    /// <see langword="true"/> when the package is a direct (top-level) reference of the analyzed
    /// project; <see langword="false"/> when it is only reachable transitively.
    /// </summary>
    public required bool IsTopLevel { get; init; }
}
