using System.Text.Json;
using System.Text.Json.Nodes;

namespace FennecLabs.Contracts.Tests;

public class ScorecardGraphPayloadTests
{
    private static ScorecardPackageResult CreateAvailableResult() => new()
    {
        PackageId = "newtonsoft.json",
        PackageVersion = "13.0.3",
        Status = ScorecardStatus.Available,
        Score = 7.5m,
        RepoName = "github.com/JamesNK/Newtonsoft.Json",
        RepoCommit = "abc123",
        ScorecardDate = "2026-01-01",
        ScorecardVersion = "4.13.0",
        Checks = [new ScorecardCheckResult { Name = "Maintained", Score = 10, Reason = "active" }],
    };

    [Fact]
    public void PayloadSchemaId_FollowsCommandNamingConvention()
    {
        Assert.Equal("fennec.scorecard.v1", SchemaIds.Payload("scorecard", 1));
    }

    [Fact]
    public void Serializes_ScorecardStatus_AsCamelCaseString()
    {
        var result = CreateAvailableResult();

        var json = JsonSerializer.Serialize(result, ContractJsonOptions.Default);
        var obj = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("available", (string?)obj["status"]);
    }

    [Fact]
    public void Serializes_unavailable_result_with_structured_error_not_omitted()
    {
        var result = new ScorecardPackageResult
        {
            PackageId = "no.repo.package",
            PackageVersion = "1.0.0",
            Status = ScorecardStatus.Unavailable,
            Error = new ArtifactError
            {
                Code = "scorecard.unavailable",
                Message = "No scorecard data could be located for this package.",
                Target = "no.repo.package",
            },
        };

        var json = JsonSerializer.Serialize(result, ContractJsonOptions.Default);
        var obj = JsonNode.Parse(json)!.AsObject();

        // Missing data must be explicit, not silently dropped from the payload.
        Assert.Equal("unavailable", (string?)obj["status"]);
        Assert.NotNull(obj["error"]);
        Assert.Equal("scorecard.unavailable", (string?)obj["error"]!["code"]);
        Assert.False(obj.ContainsKey("score"));
    }

    [Fact]
    public void Envelope_carries_scorecard_graph_payload_with_results()
    {
        var envelope = new DashboardArtifactEnvelope<ScorecardGraphPayload>
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
            },
            Payload = new ScorecardGraphPayload
            {
                TargetFramework = "net10.0",
                Results = [CreateAvailableResult()],
            },
        };

        var json = JsonSerializer.Serialize(envelope, ContractJsonOptions.Default);
        var payload = JsonNode.Parse(json)!.AsObject()["payload"]!.AsObject();

        Assert.Equal("net10.0", (string?)payload["targetFramework"]);
        var results = payload["results"]!.AsArray();
        Assert.Single(results);
        Assert.Equal("newtonsoft.json", (string?)results[0]!["packageId"]);
    }

    [Fact]
    public void Round_trips_through_deserialization()
    {
        var payload = new ScorecardGraphPayload
        {
            TargetFramework = "net10.0",
            Results = [CreateAvailableResult()],
        };

        var json = JsonSerializer.Serialize(payload, ContractJsonOptions.Default);
        var deserialized = JsonSerializer.Deserialize<ScorecardGraphPayload>(json, ContractJsonOptions.Default);

        Assert.NotNull(deserialized);
        Assert.Equal(payload.TargetFramework, deserialized!.TargetFramework);

        var original = payload.Results.Single();
        var roundTripped = deserialized.Results.Single();
        Assert.Equal(original.PackageId, roundTripped.PackageId);
        Assert.Equal(original.PackageVersion, roundTripped.PackageVersion);
        Assert.Equal(original.Status, roundTripped.Status);
        Assert.Equal(original.Score, roundTripped.Score);
        Assert.Equal(original.RepoName, roundTripped.RepoName);
        Assert.Equal(original.Checks, roundTripped.Checks);
    }
}
