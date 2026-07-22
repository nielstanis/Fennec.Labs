namespace FennecLabs.Contracts;

/// <summary>
/// Describes the environment in which a canonical dashboard artifact was produced, so
/// consumers can trace results back to a specific project, target framework, and working
/// directory without inferring it from the payload shape.
/// </summary>
public sealed record ArtifactSourceContext
{
    /// <summary>Path to the project file (e.g. <c>.csproj</c>) that was analyzed.</summary>
    public required string ProjectPath { get; init; }

    /// <summary>Working directory the producing command was invoked from.</summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>Target framework moniker analyzed, when applicable (e.g. <c>net10.0</c>).</summary>
    public string? TargetFramework { get; init; }

    /// <summary>Git commit SHA of the analyzed source tree, when available.</summary>
    public string? GitCommit { get; init; }
}
