using FennecLabs.Contracts;
using FennecLabs.TaintAnalysis.Models;

namespace FennecLabs.TaintAnalysis;

/// <summary>
/// Builds the canonical taint findings artifact (<c>result.json</c>) from a loaded
/// <see cref="TaintPolicy"/>. In this vertical slice (Story 2.1), the source/sink inventories are
/// derived directly from the policy rule set — the CFG/call-graph propagation engine that
/// correlates them with actual call sites and populates <see cref="TaintPayload.Findings"/> is
/// implemented in later Epic 2 stories.
/// </summary>
public static class TaintArtifactBuilder
{
    /// <summary>Command name used for the envelope's <c>command</c> field.</summary>
    public const string CommandName = "instrument";

    /// <summary>
    /// Builds a <see cref="DashboardArtifactEnvelope{TPayload}"/> wrapping a <see cref="TaintPayload"/>
    /// for the given merged <paramref name="policy"/> and analyzed <paramref name="assemblyPath"/>.
    /// </summary>
    public static DashboardArtifactEnvelope<TaintPayload> Build(
        TaintPolicy policy,
        string assemblyPath,
        string workingDirectory,
        string producerVersion,
        TaintOptionsInfo options,
        string? projectPath = null,
        string? gitCommit = null,
        DateTimeOffset? producedAt = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(producerVersion);
        ArgumentNullException.ThrowIfNull(options);

        var payload = new TaintPayload
        {
            PolicyId = policy.PolicyId,
            PolicyVersion = policy.SchemaVersion,
            Options = options,
            SourcesInventory = BuildSourcesInventory(policy),
            SinksInventory = BuildSinksInventory(policy),
            Findings = [],
        };

        return new DashboardArtifactEnvelope<TaintPayload>
        {
            Schema = SchemaIds.Envelope(),
            SchemaVersion = "1.0.0",
            Command = CommandName,
            ProducedAt = producedAt ?? DateTimeOffset.UtcNow,
            ProducerVersion = producerVersion,
            SourceContext = new ArtifactSourceContext
            {
                ProjectPath = projectPath ?? assemblyPath,
                WorkingDirectory = workingDirectory,
                GitCommit = gitCommit,
            },
            Payload = payload,
        };
    }

    private static IReadOnlyList<TaintSourceInventoryItem> BuildSourcesInventory(TaintPolicy policy) =>
        policy.Sources
            .Select(rule => new TaintSourceInventoryItem
            {
                PolicyRuleId = rule.Id,
                Category = rule.Category ?? "unknown",
                Assembly = rule.Assembly,
                TypeName = rule.TypeName,
                MemberName = rule.MemberName,
                Confidence = rule.Confidence,
                Description = rule.Description,
            })
            .ToList();

    private static IReadOnlyList<TaintSinkInventoryItem> BuildSinksInventory(TaintPolicy policy) =>
        policy.Sinks
            .Select(rule => new TaintSinkInventoryItem
            {
                PolicyRuleId = rule.Id,
                Category = rule.Category ?? "unknown",
                Assembly = rule.Assembly,
                TypeName = rule.TypeName,
                MemberName = rule.MemberName,
                Severity = rule.Severity ?? TaintSeverity.Low,
                Description = rule.Description,
            })
            .ToList();
}
