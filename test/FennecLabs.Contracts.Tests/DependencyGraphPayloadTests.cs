using System.Text.Json;
using System.Text.Json.Nodes;

namespace FennecLabs.Contracts.Tests;

public class DependencyGraphPayloadTests
{
    private static DependencyNode CreateNode(string id = "newtonsoft.json", bool isTopLevel = true) => new()
    {
        Id = id,
        ResolvedVersion = "13.0.3",
        RequestedVersion = "13.0.*",
        IsTopLevel = isTopLevel,
    };

    private static DashboardArtifactEnvelope<DependencyGraphPayload> CreateEnvelope() => new()
    {
        Schema = SchemaIds.Envelope(),
        SchemaVersion = "1.0.0",
        Command = "dependencies",
        ProducedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ProducerVersion = "0.7.5",
        SourceContext = new ArtifactSourceContext
        {
            ProjectPath = "src/Sample/Sample.csproj",
            WorkingDirectory = "/workspaces/Fennec.Labs",
            TargetFramework = "net10.0",
        },
        Payload = new DependencyGraphPayload
        {
            TargetFramework = "net10.0",
            Nodes = [CreateNode()],
        },
    };

    [Fact]
    public void PayloadSchemaId_FollowsCommandNamingConvention()
    {
        Assert.Equal("fennec.dependencies.v1", SchemaIds.Payload("dependencies", 1));
    }

    [Fact]
    public void Serializes_dependency_node_fields_with_canonical_names()
    {
        var node = CreateNode();

        var json = JsonSerializer.Serialize(node, ContractJsonOptions.Default);
        var obj = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("newtonsoft.json", (string?)obj["id"]);
        Assert.Equal("13.0.3", (string?)obj["resolvedVersion"]);
        Assert.Equal("13.0.*", (string?)obj["requestedVersion"]);
        Assert.True((bool?)obj["isTopLevel"]);
    }

    [Fact]
    public void Omits_null_requestedVersion()
    {
        var node = CreateNode() with { RequestedVersion = null };

        var json = JsonSerializer.Serialize(node, ContractJsonOptions.Default);
        var obj = JsonNode.Parse(json)!.AsObject();

        Assert.False(obj.ContainsKey("requestedVersion"));
    }

    [Fact]
    public void Envelope_carries_dependency_graph_payload_with_nodes()
    {
        var envelope = CreateEnvelope();

        var json = JsonSerializer.Serialize(envelope, ContractJsonOptions.Default);
        var payload = JsonNode.Parse(json)!.AsObject()["payload"]!.AsObject();

        Assert.Equal("net10.0", (string?)payload["targetFramework"]);
        var nodes = payload["nodes"]!.AsArray();
        Assert.Single(nodes);
        Assert.Equal("newtonsoft.json", (string?)nodes[0]!["id"]);
    }

    [Fact]
    public void Round_trips_through_deserialization()
    {
        var envelope = CreateEnvelope();

        var json = JsonSerializer.Serialize(envelope, ContractJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<DashboardArtifactEnvelope<DependencyGraphPayload>>(
            json, ContractJsonOptions.Default);

        Assert.NotNull(deserialized);
        Assert.Equal(envelope.Payload.TargetFramework, deserialized!.Payload.TargetFramework);
        Assert.Equal(envelope.Payload.Nodes, deserialized.Payload.Nodes);
    }
}
