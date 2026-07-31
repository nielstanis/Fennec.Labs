namespace FennecLabs.TaintAnalysis.Models;

/// <summary>
/// Catalog entry describing one taint sink rule that is active in the loaded policy. In this
/// minimal vertical slice (Story 2.1), inventory entries are derived directly from the policy
/// rule set rather than correlated against actual call sites in the analyzed assembly — call-site
/// correlation is added by the CFG/propagation engine in later Epic 2 stories.
/// </summary>
public sealed record TaintSinkInventoryItem
{
    /// <summary>Identifier of the <see cref="TaintRule"/> this inventory entry was derived from.</summary>
    public required string PolicyRuleId { get; init; }

    /// <summary>Taint category of the sink (e.g. <c>sql-injection</c>).</summary>
    public required string Category { get; init; }

    /// <summary>Assembly name of the sink call site, per policy.</summary>
    public string? Assembly { get; init; }

    /// <summary>Declaring type name of the sink call site, per policy.</summary>
    public string? TypeName { get; init; }

    /// <summary>Member name of the sink call site, per policy.</summary>
    public string? MemberName { get; init; }

    /// <summary>Severity assigned to findings raised against this sink rule.</summary>
    public required TaintSeverity Severity { get; init; }

    /// <summary>Human-readable description carried from the policy rule.</summary>
    public string? Description { get; init; }
}
