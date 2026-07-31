namespace FennecLabs.TaintAnalysis.Models;

/// <summary>
/// Placeholder shape for a source→sink taint finding. Always empty in this vertical slice
/// (Story 2.1) — populated once the CFG/call-graph propagation engine (Epic 2, later stories)
/// is implemented.
/// </summary>
public sealed record TaintFinding
{
    /// <summary>Stable identifier for this finding.</summary>
    public required string Id { get; init; }

    /// <summary>Taint category of the finding (e.g. <c>sql-injection</c>).</summary>
    public required string Category { get; init; }

    /// <summary>Severity of the finding.</summary>
    public required TaintSeverity Severity { get; init; }
}
