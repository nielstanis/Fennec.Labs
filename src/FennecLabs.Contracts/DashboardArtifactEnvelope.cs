using System.Text.Json.Serialization;

namespace FennecLabs.Contracts;

/// <summary>
/// Canonical envelope wrapping every dashboard-consumed artifact produced by Fennec.Labs
/// commands. Per architecture decision AD-1, every dashboard-consumed artifact MUST use this
/// envelope shape; command-specific fields live only inside <see cref="Payload"/>.
/// </summary>
/// <typeparam name="TPayload">Command-specific payload type carried by this artifact.</typeparam>
public sealed record DashboardArtifactEnvelope<TPayload>
{
    /// <summary>Schema identifier for this envelope/payload combination (e.g. <c>fennec.envelope.v1</c>).</summary>
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }

    /// <summary>SemVer version of the schema identified by <see cref="Schema"/>.</summary>
    public required string SchemaVersion { get; init; }

    /// <summary>Name of the command that produced this artifact (e.g. <c>scorecard</c>).</summary>
    public required string Command { get; init; }

    /// <summary>UTC timestamp the artifact was produced at.</summary>
    public required DateTimeOffset ProducedAt { get; init; }

    /// <summary>Version of the producer (e.g. the Fennec.Labs CLI) that generated this artifact.</summary>
    public required string ProducerVersion { get; init; }

    /// <summary>Context describing what was analyzed to produce this artifact.</summary>
    public required ArtifactSourceContext SourceContext { get; init; }

    /// <summary>Command-specific payload data.</summary>
    public required TPayload Payload { get; init; }
}
