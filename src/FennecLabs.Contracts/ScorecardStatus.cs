namespace FennecLabs.Contracts;

/// <summary>
/// Explicit, structured state of a package's scorecard lookup within a canonical scorecard
/// payload. Per Story 1.3's acceptance criteria, missing scorecard data must be represented
/// explicitly rather than being silently omitted from the payload.
/// </summary>
public enum ScorecardStatus
{
    /// <summary>A scorecard result was successfully retrieved for the package.</summary>
    Available,

    /// <summary>No scorecard result could be located for the package (e.g. no known repository).</summary>
    Unavailable,

    /// <summary>An error occurred while attempting to retrieve the scorecard result.</summary>
    Error,
}
