namespace FennecLabs.Contracts;

/// <summary>
/// A single OpenSSF Scorecard check result within a canonical scorecard payload.
/// </summary>
public sealed record ScorecardCheckResult
{
    /// <summary>Name of the check (e.g. <c>Maintained</c>, <c>Vulnerabilities</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Score for this check, in the OpenSSF Scorecard 0-10 range.</summary>
    public required int Score { get; init; }

    /// <summary>Human-readable explanation of the check result, when available.</summary>
    public string? Reason { get; init; }
}
