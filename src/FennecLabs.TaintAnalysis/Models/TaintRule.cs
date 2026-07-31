namespace FennecLabs.TaintAnalysis.Models;

/// <summary>
/// A single typed rule from a taint policy document (<c>fennec.taint.policy.v1</c>), identifying
/// a call site by (<see cref="Assembly"/>, <see cref="TypeName"/>, <see cref="MemberName"/>) and
/// the role it plays in the taint state machine.
/// </summary>
public sealed record TaintRule
{
    /// <summary>Stable identifier for this rule, unique within a policy. Used for merge resolution.</summary>
    public required string Id { get; init; }

    /// <summary>Role this rule plays: source, sink, propagator, or sanitizer.</summary>
    public required TaintRuleKind Kind { get; init; }

    /// <summary>Assembly name the call site belongs to (e.g. <c>System.Runtime</c>).</summary>
    public string? Assembly { get; init; }

    /// <summary>Fully-qualified declaring type name of the call site.</summary>
    public string? TypeName { get; init; }

    /// <summary>Member (method/property accessor) name of the call site.</summary>
    public string? MemberName { get; init; }

    /// <summary>Taint category this rule belongs to (e.g. <c>network-input</c>, <c>sql-injection</c>).</summary>
    public string? Category { get; init; }

    /// <summary>Severity assigned to findings raised against a sink rule.</summary>
    public TaintSeverity? Severity { get; init; }

    /// <summary>Confidence (0.0-1.0) that a match against this rule is a genuine source/sink.</summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// For sink rules, the zero-based argument positions whose taint triggers a finding.
    /// An empty or null list means any tainted argument triggers a finding.
    /// </summary>
    public IReadOnlyList<int>? ArgIndices { get; init; }

    /// <summary>Human-readable description of why this rule exists.</summary>
    public string? Description { get; init; }

    /// <summary>Per-execution-context severity/confidence overrides, keyed by context identifier.</summary>
    public IReadOnlyDictionary<string, TaintContextOverride>? ContextOverrides { get; init; }
}
