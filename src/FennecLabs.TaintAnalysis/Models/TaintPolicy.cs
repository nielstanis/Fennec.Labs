using System.Text.Json.Serialization;

namespace FennecLabs.TaintAnalysis.Models;

/// <summary>
/// A fully-loaded, merged taint policy document (<c>fennec.taint.policy.v1</c>) — the built-in
/// rule set optionally merged with a user-supplied override file. Rule collections are exposed
/// pre-partitioned by <see cref="TaintRuleKind"/> for convenient lookup by the taint engine.
/// </summary>
public sealed record TaintPolicy
{
    /// <summary>Schema identifier for this policy document (e.g. <c>fennec.taint.policy.v1</c>).</summary>
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }

    /// <summary>SemVer version of the policy schema.</summary>
    public required string SchemaVersion { get; init; }

    /// <summary>Identifier of this policy (e.g. <c>default-v1</c>).</summary>
    public required string PolicyId { get; init; }

    /// <summary>All rules in this policy, in merge order (built-in first, user overrides applied).</summary>
    public required IReadOnlyList<TaintRule> Rules { get; init; }

    /// <summary>Rules with <see cref="TaintRule.Kind"/> == <see cref="TaintRuleKind.Source"/>.</summary>
    public IReadOnlyList<TaintRule> Sources =>
        Rules.Where(r => r.Kind == TaintRuleKind.Source).ToList();

    /// <summary>Rules with <see cref="TaintRule.Kind"/> == <see cref="TaintRuleKind.Sink"/>.</summary>
    public IReadOnlyList<TaintRule> Sinks =>
        Rules.Where(r => r.Kind == TaintRuleKind.Sink).ToList();

    /// <summary>Rules with <see cref="TaintRule.Kind"/> == <see cref="TaintRuleKind.Propagator"/>.</summary>
    public IReadOnlyList<TaintRule> Propagators =>
        Rules.Where(r => r.Kind == TaintRuleKind.Propagator).ToList();

    /// <summary>Rules with <see cref="TaintRule.Kind"/> == <see cref="TaintRuleKind.Sanitizer"/>.</summary>
    public IReadOnlyList<TaintRule> Sanitizers =>
        Rules.Where(r => r.Kind == TaintRuleKind.Sanitizer).ToList();

    /// <summary>
    /// Looks up a rule by (<paramref name="assembly"/>, <paramref name="typeName"/>,
    /// <paramref name="memberName"/>), matching case-insensitively as required by the policy
    /// resolution rules.
    /// </summary>
    public TaintRule? Find(string? assembly, string? typeName, string? memberName) =>
        Rules.FirstOrDefault(r =>
            string.Equals(r.Assembly, assembly, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.TypeName, typeName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.MemberName, memberName, StringComparison.OrdinalIgnoreCase));
}
