using System.Text.Json;
using System.Text.Json.Nodes;
using FennecLabs.Cli.Commands;
using FennecLabs.Contracts;
using FennecLabs.DotNetCli;
using FennecLabs.Scorecard;

namespace FennecLabs.Cli.Tests;

public class ProducerArtifactSchemaValidationTests
{
    [Fact]
    public async Task DependencyHandler_EmitsCanonicalEnvelope_AndRoundTripsTypedContract()
    {
        var outputRoot = UniqueTempDirectory();
        Directory.CreateDirectory(outputRoot);

        try
        {
            var framework = new Framework
            {
                FrameworkName = "net10.0",
                TopLevelPackages =
                [
                    new PackageReference
                    {
                        Id = "Newtonsoft.Json",
                        RequestedVersion = "13.0.*",
                        ResolvedVersion = "13.0.3",
                    },
                ],
                TransitivePackages = [],
            };

            var packageList = new PackageListResult
            {
                Version = 1,
                Projects =
                [
                    new Project
                    {
                        Path = "src/Sample/Sample.csproj",
                        Frameworks = [framework],
                    },
                ],
            };

            var handler = new DependencyGraphCommandHandler(
                _ => Task.FromResult<PackageListResult?>(packageList));

            var exitCode = await handler.ExecuteAsync(
                "src/Sample/Sample.csproj",
                OutputMode.Json,
                outputRoot);

            Assert.Equal(0, exitCode);

            var artifactPath = FindSingleResultJson(outputRoot, "dependencies", "Sample");
            var json = await File.ReadAllTextAsync(artifactPath);
            var node = JsonNode.Parse(json)!.AsObject();

            Assert.NotNull(node["$schema"]);
            Assert.NotNull(node["schemaVersion"]);
            Assert.Equal("dependencies", (string?)node["command"]);
            Assert.NotNull(node["producedAt"]);
            Assert.NotNull(node["producerVersion"]);
            Assert.NotNull(node["sourceContext"]);
            Assert.NotNull(node["payload"]);

            var envelope = JsonSerializer.Deserialize<DashboardArtifactEnvelope<DependencyGraphPayload>>(
                json,
                ContractJsonOptions.Default);

            Assert.NotNull(envelope);
            Assert.Equal(SchemaIds.Envelope(), envelope!.Schema);
            Assert.Equal("1.0.0", envelope.SchemaVersion);
            Assert.Equal("dependencies", envelope.Command);
            Assert.Equal("src/Sample/Sample.csproj", envelope.SourceContext.ProjectPath);
            Assert.Equal("net10.0", envelope.Payload.TargetFramework);
            Assert.Single(envelope.Payload.Nodes);
            Assert.Equal("newtonsoft.json", envelope.Payload.Nodes[0].Id);
        }
        finally
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ScorecardHandler_EmitsCanonicalEnvelope_WithExplicitUnavailableState()
    {
        var outputRoot = UniqueTempDirectory();
        Directory.CreateDirectory(outputRoot);

        try
        {
            var framework = new Framework
            {
                FrameworkName = "net10.0",
                TopLevelPackages =
                [
                    new PackageReference
                    {
                        Id = "No.Repo.Package",
                        RequestedVersion = "1.0.*",
                        ResolvedVersion = "1.0.0",
                    },
                ],
                TransitivePackages = [],
            };

            var packageList = new List<PackageReference>
            {
                new()
                {
                    Id = "No.Repo.Package",
                    RequestedVersion = "1.0.*",
                    ResolvedVersion = "1.0.0",
                },
            };

            var handler = new ScorecardCommandHandler(
                new ScorecardClient(new HttpClient()),
                (_, _) => Task.FromResult<(Framework framework, List<PackageReference> packages)?>((framework, packageList)),
                (packages, _, _) => Task.FromResult(
                    packages.Select(p => new PackageScorecardResult
                    {
                        PackageId = p.Id,
                        PackageVersion = p.ResolvedVersion ?? "unknown",
                        Scorecard = null,
                        Error = null,
                    }).ToList()));

            var exitCode = await handler.ExecuteAsync(
                "src/Sample/Sample.csproj",
                reportFormat: null,
                OutputMode.Json,
                outputRoot);

            Assert.Equal(0, exitCode);

            var artifactPath = FindSingleResultJson(outputRoot, "scorecard", "Sample");
            var json = await File.ReadAllTextAsync(artifactPath);
            var node = JsonNode.Parse(json)!.AsObject();

            Assert.NotNull(node["$schema"]);
            Assert.NotNull(node["schemaVersion"]);
            Assert.Equal("scorecard", (string?)node["command"]);
            Assert.NotNull(node["producedAt"]);
            Assert.NotNull(node["producerVersion"]);
            Assert.NotNull(node["sourceContext"]);
            Assert.NotNull(node["payload"]);

            var envelope = JsonSerializer.Deserialize<DashboardArtifactEnvelope<ScorecardGraphPayload>>(
                json,
                ContractJsonOptions.Default);

            Assert.NotNull(envelope);
            Assert.Equal(SchemaIds.Envelope(), envelope!.Schema);
            Assert.Equal("1.0.0", envelope.SchemaVersion);
            Assert.Equal("scorecard", envelope.Command);
            Assert.Equal("src/Sample/Sample.csproj", envelope.SourceContext.ProjectPath);
            Assert.Single(envelope.Payload.Results);

            var result = envelope.Payload.Results[0];
            Assert.Equal("no.repo.package", result.PackageId);
            Assert.Equal(ScorecardStatus.Unavailable, result.Status);
            Assert.NotNull(result.Error);
            Assert.Equal("scorecard.unavailable", result.Error!.Code);
        }
        finally
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }

    private static string UniqueTempDirectory() =>
        Path.Combine(Path.GetTempPath(), "fennec-tests", Guid.NewGuid().ToString("N"));

    private static string FindSingleResultJson(string outputRoot, string commandFolder, string projectName)
    {
        var basePath = Path.Combine(outputRoot, commandFolder, projectName);
        var files = Directory.GetFiles(basePath, "result.json", SearchOption.AllDirectories);
        Assert.Single(files);
        return files[0];
    }
}
