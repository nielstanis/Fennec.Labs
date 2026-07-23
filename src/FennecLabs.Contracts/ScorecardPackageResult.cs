namespace FennecLabs.Contracts;

/// <summary>
/// Scorecard/check results for a single package, keyed by its normalized dependency identity, so
/// consumers can link risk signals directly to <see cref="DependencyNode"/> entries produced by
/// the <c>dependencies</c> command.
/// </summary>
public sealed record ScorecardPackageResult
{
    /// <summary>
    /// Stable package identity: the NuGet package ID normalized to lowercase invariant culture,
    /// matching <see cref="DependencyNode.Id"/> so scorecard results can be joined to dependency
    /// graph nodes without re-normalizing identities downstream.
    /// </summary>
    public required string PackageId { get; init; }

    /// <summary>Resolved version of the package the scorecard lookup was performed for.</summary>
    public required string PackageVersion { get; init; }

    /// <summary>
    /// Explicit state of the scorecard lookup for this package. Per Story 1.3, missing or failed
    /// lookups MUST be represented here rather than omitting the package from the payload.
    /// </summary>
    public required ScorecardStatus Status { get; init; }

    /// <summary>Overall OpenSSF Scorecard score, present only when <see cref="Status"/> is <see cref="ScorecardStatus.Available"/>.</summary>
    public decimal? Score { get; init; }

    /// <summary>Name of the source repository the scorecard was computed against.</summary>
    public string? RepoName { get; init; }

    /// <summary>Commit SHA of the source repository the scorecard was computed against.</summary>
    public string? RepoCommit { get; init; }

    /// <summary>Date the upstream scorecard analysis was run.</summary>
    public string? ScorecardDate { get; init; }

    /// <summary>Version of the OpenSSF Scorecard tool that produced the result.</summary>
    public string? ScorecardVersion { get; init; }

    /// <summary>Individual check results, empty when no scorecard data is available.</summary>
    public IReadOnlyList<ScorecardCheckResult> Checks { get; init; } = [];

    /// <summary>
    /// Structured error describing why scorecard data is unavailable, present when
    /// <see cref="Status"/> is <see cref="ScorecardStatus.Unavailable"/> or <see cref="ScorecardStatus.Error"/>.
    /// </summary>
    public ArtifactError? Error { get; init; }
}
