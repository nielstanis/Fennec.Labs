namespace FennecLabs.Contracts;

/// <summary>
/// Canonical payload for the <c>scorecard</c> command, carried inside a
/// <see cref="DashboardArtifactEnvelope{TPayload}"/>. Represents package-level OpenSSF Scorecard
/// results keyed by normalized package identity, so consumers can link risk signals directly to
/// <see cref="DependencyGraphPayload"/> nodes for the same project/framework.
/// </summary>
public sealed record ScorecardGraphPayload
{
    /// <summary>Target framework moniker the scorecard results were resolved for (e.g. <c>net10.0</c>).</summary>
    public required string TargetFramework { get; init; }

    /// <summary>Scorecard results for each package analyzed, keyed by normalized package identity.</summary>
    public required IReadOnlyList<ScorecardPackageResult> Results { get; init; }
}
