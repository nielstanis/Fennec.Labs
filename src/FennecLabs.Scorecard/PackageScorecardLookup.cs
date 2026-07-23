namespace FennecLabs.Scorecard;

/// <summary>
/// A single package's scorecard lookup outcome, as gathered by a producer (e.g. the CLI's
/// <c>scorecard</c> command) before normalization into the canonical
/// <see cref="FennecLabs.Contracts.ScorecardGraphPayload"/> shape.
/// </summary>
public sealed record PackageScorecardLookup
{
    /// <summary>NuGet package ID the lookup was performed for (not yet identity-normalized).</summary>
    public required string PackageId { get; init; }

    /// <summary>Resolved version of the package.</summary>
    public required string PackageVersion { get; init; }

    /// <summary>Scorecard result, when one was successfully retrieved.</summary>
    public ScorecardResult? Result { get; init; }

    /// <summary>Error message describing why the lookup failed, when applicable.</summary>
    public string? ErrorMessage { get; init; }
}
