using System.Text.Json;
using System.Text.Json.Nodes;
using FennecLabs.Contracts;

namespace FennecLabs.Contracts.Tests;

public class DashboardArtifactEnvelopeTests
{
    private sealed record SamplePayload(string Value);

    private static DashboardArtifactEnvelope<SamplePayload> CreateEnvelope() => new()
    {
        Schema = SchemaIds.Envelope(),
        SchemaVersion = "1.0.0",
        Command = "scorecard",
        ProducedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ProducerVersion = "0.7.5",
        SourceContext = new ArtifactSourceContext
        {
            ProjectPath = "src/Sample/Sample.csproj",
            WorkingDirectory = "/workspaces/Fennec.Labs",
            TargetFramework = "net10.0",
            GitCommit = "abc123",
        },
        Payload = new SamplePayload("hello"),
    };

    [Fact]
    public void Serializes_all_required_envelope_fields_with_canonical_names()
    {
        var envelope = CreateEnvelope();

        var json = JsonSerializer.Serialize(envelope, ContractJsonOptions.Default);
        var node = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("fennec.envelope.v1", (string?)node["$schema"]);
        Assert.Equal("1.0.0", (string?)node["schemaVersion"]);
        Assert.Equal("scorecard", (string?)node["command"]);
        Assert.NotNull(node["producedAt"]);
        Assert.Equal("0.7.5", (string?)node["producerVersion"]);
        Assert.NotNull(node["sourceContext"]);
        Assert.NotNull(node["payload"]);
    }

    [Fact]
    public void SourceContext_serializes_project_and_working_directory()
    {
        var envelope = CreateEnvelope();

        var json = JsonSerializer.Serialize(envelope, ContractJsonOptions.Default);
        var sourceContext = JsonNode.Parse(json)!.AsObject()["sourceContext"]!.AsObject();

        Assert.Equal("src/Sample/Sample.csproj", (string?)sourceContext["projectPath"]);
        Assert.Equal("/workspaces/Fennec.Labs", (string?)sourceContext["workingDirectory"]);
        Assert.Equal("net10.0", (string?)sourceContext["targetFramework"]);
        Assert.Equal("abc123", (string?)sourceContext["gitCommit"]);
    }

    [Fact]
    public void Omits_null_optional_source_context_fields()
    {
        var envelope = CreateEnvelope() with
        {
            SourceContext = new ArtifactSourceContext
            {
                ProjectPath = "src/Sample/Sample.csproj",
                WorkingDirectory = "/workspaces/Fennec.Labs",
            },
        };

        var json = JsonSerializer.Serialize(envelope, ContractJsonOptions.Default);
        var sourceContext = JsonNode.Parse(json)!.AsObject()["sourceContext"]!.AsObject();

        Assert.False(sourceContext.ContainsKey("targetFramework"));
        Assert.False(sourceContext.ContainsKey("gitCommit"));
    }

    [Fact]
    public void Round_trips_through_deserialization()
    {
        var envelope = CreateEnvelope();

        var json = JsonSerializer.Serialize(envelope, ContractJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<DashboardArtifactEnvelope<SamplePayload>>(json, ContractJsonOptions.Default);

        Assert.NotNull(deserialized);
        Assert.Equal(envelope.Schema, deserialized!.Schema);
        Assert.Equal(envelope.SchemaVersion, deserialized.SchemaVersion);
        Assert.Equal(envelope.Command, deserialized.Command);
        Assert.Equal(envelope.ProducedAt, deserialized.ProducedAt);
        Assert.Equal(envelope.ProducerVersion, deserialized.ProducerVersion);
        Assert.Equal(envelope.SourceContext, deserialized.SourceContext);
        Assert.Equal(envelope.Payload, deserialized.Payload);
    }
}
