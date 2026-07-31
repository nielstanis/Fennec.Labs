namespace FennecLabs.TaintAnalysis.Models;

/// <summary>
/// Command-specific payload for the taint findings artifact (<c>result.json</c>), carried inside
/// a <c>DashboardArtifactEnvelope&lt;TaintPayload&gt;</c> per architecture decision AD-1.
/// </summary>
public sealed record TaintPayload
{
    /// <summary>Identifier of the effective (merged) policy used to produce this payload.</summary>
    public required string PolicyId { get; init; }

    /// <summary>SemVer version of the policy schema.</summary>
    public required string PolicyVersion { get; init; }

    /// <summary>Effective options used for this analysis run.</summary>
    public required TaintOptionsInfo Options { get; init; }

    /// <summary>
    /// Catalog of active source rules from the loaded policy. Populated directly from policy
    /// definitions in this vertical slice; not yet correlated with actual call sites.
    /// </summary>
    public required IReadOnlyList<TaintSourceInventoryItem> SourcesInventory { get; init; }

    /// <summary>
    /// Catalog of active sink rules from the loaded policy. Populated directly from policy
    /// definitions in this vertical slice; not yet correlated with actual call sites.
    /// </summary>
    public required IReadOnlyList<TaintSinkInventoryItem> SinksInventory { get; init; }

    /// <summary>
    /// Source→sink findings. Always empty in this vertical slice — the CFG/call-graph
    /// propagation engine has not been implemented yet (later Epic 2 stories).
    /// </summary>
    public required IReadOnlyList<TaintFinding> Findings { get; init; }
}
