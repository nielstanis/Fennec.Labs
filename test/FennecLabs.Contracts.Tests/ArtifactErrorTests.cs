using System.Text.Json;
using System.Text.Json.Nodes;
using FennecLabs.Contracts;

namespace FennecLabs.Contracts.Tests;

public class ArtifactErrorTests
{
    [Fact]
    public void Serializes_as_structured_object_not_a_bare_string()
    {
        var error = new ArtifactError
        {
            Code = "scorecard.unavailable",
            Message = "Scorecard data could not be retrieved for this package.",
            Target = "Newtonsoft.Json",
            Details = new Dictionary<string, string> { ["httpStatus"] = "404" },
        };

        var json = JsonSerializer.Serialize(error, ContractJsonOptions.Default);
        var node = JsonNode.Parse(json)!;

        // Must be a structured JSON object, not a plain string value.
        Assert.Equal(JsonValueKind.Object, node.GetValueKind());

        var obj = node.AsObject();
        Assert.Equal("scorecard.unavailable", (string?)obj["code"]);
        Assert.Equal("Scorecard data could not be retrieved for this package.", (string?)obj["message"]);
        Assert.Equal("Newtonsoft.Json", (string?)obj["target"]);
        Assert.Equal("404", (string?)obj["details"]!["httpStatus"]);
    }

    [Fact]
    public void Omits_null_optional_fields()
    {
        var error = new ArtifactError
        {
            Code = "dependency.missing",
            Message = "Dependency graph node could not be resolved.",
        };

        var json = JsonSerializer.Serialize(error, ContractJsonOptions.Default);
        var obj = JsonNode.Parse(json)!.AsObject();

        Assert.False(obj.ContainsKey("target"));
        Assert.False(obj.ContainsKey("details"));
    }

    [Fact]
    public void Requires_code_and_message_at_compile_time()
    {
        // Compiles only because Code and Message are supplied; this test exists to document
        // the typed/required nature of the structured error shape.
        var error = new ArtifactError { Code = "x", Message = "y" };

        Assert.Equal("x", error.Code);
        Assert.Equal("y", error.Message);
    }
}
