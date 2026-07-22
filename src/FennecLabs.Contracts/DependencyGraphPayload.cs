namespace FennecLabs.Contracts;

/// <summary>
/// Canonical payload for the <c>dependencies</c> command, carried inside a
/// <see cref="DashboardArtifactEnvelope{TPayload}"/>. Represents a normalized, flattened view of
/// a project's transitive dependency graph for a single target framework, per architecture
/// decision AD-7.
/// </summary>
public sealed record DependencyGraphPayload
{
    /// <summary>Target framework moniker the dependency graph was resolved for (e.g. <c>net10.0</c>).</summary>
    public required string TargetFramework { get; init; }

    /// <summary>
    /// Flattened, deduplicated set of package nodes reachable from the project, each with a
    /// stable package identity per <see cref="DependencyNode.Id"/>.
    /// </summary>
    public required IReadOnlyList<DependencyNode> Nodes { get; init; }
}
