namespace FennecLabs.TaintAnalysis.Models;

/// <summary>
/// Catalog entry describing one taint source rule that is active in the loaded policy. In this
/// minimal vertical slice (Story 2.1), inventory entries are derived directly from the policy
/// rule set rather than correlated against actual call sites in the analyzed assembly — call-site
/// correlation is added by the CFG/propagation engine in later Epic 2 stories.
/// </summary>
public sealed record TaintSourceInventoryItem
{
    /// <summary>Identifier of the <see cref="TaintRule"/> this inventory entry was derived from.</summary>
    public required string PolicyRuleId { get; init; }

    /// <summary>Taint category of the source (e.g. <c>network-input</c>).</summary>
    public required string Category { get; init; }

    /// <summary>Assembly name of the source call site, per policy.</summary>
    public string? Assembly { get; init; }

    /// <summary>Declaring type name of the source call site, per policy.</summary>
    public string? TypeName { get; init; }

    /// <summary>Member name of the source call site, per policy.</summary>
    public string? MemberName { get; init; }

    /// <summary>Confidence (0.0-1.0) that a match against this rule is a genuine source.</summary>
    public double? Confidence { get; init; }

    /// <summary>Human-readable description carried from the policy rule.</summary>
    public string? Description { get; init; }
}
