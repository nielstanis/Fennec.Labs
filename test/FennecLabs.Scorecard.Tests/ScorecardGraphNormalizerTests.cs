using FennecLabs.Contracts;

namespace FennecLabs.Scorecard.Tests;

public class ScorecardGraphNormalizerTests
{
    private static ScorecardResult CreateResult(decimal score = 8.5m) => new()
    {
        Date = "2026-01-01",
        Repo = new Repo { Name = "github.com/org/repo", Commit = "abc123" },
        Scorecard = new ScorecardVersion { Version = "4.13.0", Commit = "def456" },
        Score = score,
        Checks =
        [
            new ScorecardCheck { Name = "Maintained", Score = 10, Reason = "active" },
            new ScorecardCheck { Name = "Vulnerabilities", Score = 9, Reason = "none found" },
        ],
    };

    [Fact]
    public void Normalize_SetsCanonicalEnvelopeMetadata()
    {
        var lookups = new List<PackageScorecardLookup>
        {
            new() { PackageId = "Newtonsoft.Json", PackageVersion = "13.0.3", Result = CreateResult() },
        };

        var envelope = ScorecardGraphNormalizer.Normalize(
            "net10.0",
            lookups,
            projectPath: "src/Sample/Sample.csproj",
            workingDirectory: "/workspaces/Fennec.Labs",
            producerVersion: "0.7.5",
            gitCommit: "abc123",
            producedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("fennec.envelope.v1", envelope.Schema);
        Assert.Equal("scorecard", envelope.Command);
        Assert.Equal("0.7.5", envelope.ProducerVersion);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), envelope.ProducedAt);
        Assert.Equal("src/Sample/Sample.csproj", envelope.SourceContext.ProjectPath);
        Assert.Equal("/workspaces/Fennec.Labs", envelope.SourceContext.WorkingDirectory);
        Assert.Equal("net10.0", envelope.SourceContext.TargetFramework);
        Assert.Equal("abc123", envelope.SourceContext.GitCommit);
        Assert.Equal("net10.0", envelope.Payload.TargetFramework);
    }

    [Fact]
    public void Normalize_NormalizesPackageIdToLowercaseInvariant()
    {
        var lookups = new List<PackageScorecardLookup>
        {
            new() { PackageId = "Newtonsoft.Json", PackageVersion = "13.0.3", Result = CreateResult() },
        };

        var envelope = ScorecardGraphNormalizer.Normalize("net10.0", lookups, "p.csproj", "/wd", "0.1.0");

        Assert.Equal("newtonsoft.json", envelope.Payload.Results.Single().PackageId);
    }

    [Fact]
    public void Normalize_AvailableResult_MapsAllScorecardFields()
    {
        var lookups = new List<PackageScorecardLookup>
        {
            new() { PackageId = "Pkg", PackageVersion = "1.0.0", Result = CreateResult(7.25m) },
        };

        var envelope = ScorecardGraphNormalizer.Normalize("net10.0", lookups, "p.csproj", "/wd", "0.1.0");
        var result = envelope.Payload.Results.Single();

        Assert.Equal(ScorecardStatus.Available, result.Status);
        Assert.Equal(7.25m, result.Score);
        Assert.Equal("github.com/org/repo", result.RepoName);
        Assert.Equal("abc123", result.RepoCommit);
        Assert.Equal("2026-01-01", result.ScorecardDate);
        Assert.Equal("4.13.0", result.ScorecardVersion);
        Assert.Equal(2, result.Checks.Count);
        Assert.Equal("Maintained", result.Checks[0].Name);
        Assert.Equal(10, result.Checks[0].Score);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Normalize_MissingResult_MarksUnavailableWithStructuredError()
    {
        var lookups = new List<PackageScorecardLookup>
        {
            new() { PackageId = "No.Repo.Package", PackageVersion = "1.0.0", Result = null },
        };

        var envelope = ScorecardGraphNormalizer.Normalize("net10.0", lookups, "p.csproj", "/wd", "0.1.0");
        var result = envelope.Payload.Results.Single();

        Assert.Equal(ScorecardStatus.Unavailable, result.Status);
        Assert.Null(result.Score);
        Assert.Empty(result.Checks);
        Assert.NotNull(result.Error);
        Assert.Equal("scorecard.unavailable", result.Error!.Code);
        Assert.Equal("no.repo.package", result.Error.Target);
    }

    [Fact]
    public void Normalize_FailedLookup_MarksErrorWithStructuredError()
    {
        var lookups = new List<PackageScorecardLookup>
        {
            new()
            {
                PackageId = "Failing.Package",
                PackageVersion = "1.0.0",
                ErrorMessage = "HTTP 500 from scorecard API",
            },
        };

        var envelope = ScorecardGraphNormalizer.Normalize("net10.0", lookups, "p.csproj", "/wd", "0.1.0");
        var result = envelope.Payload.Results.Single();

        Assert.Equal(ScorecardStatus.Error, result.Status);
        Assert.NotNull(result.Error);
        Assert.Equal("scorecard.fetch_failed", result.Error!.Code);
        Assert.Equal("HTTP 500 from scorecard API", result.Error.Message);
        Assert.Equal("failing.package", result.Error.Target);
    }

    [Fact]
    public void Normalize_ErrorTakesPrecedenceOverMissingResult()
    {
        var lookups = new List<PackageScorecardLookup>
        {
            new()
            {
                PackageId = "Pkg",
                PackageVersion = "1.0.0",
                Result = null,
                ErrorMessage = "timeout",
            },
        };

        var envelope = ScorecardGraphNormalizer.Normalize("net10.0", lookups, "p.csproj", "/wd", "0.1.0");

        Assert.Equal(ScorecardStatus.Error, envelope.Payload.Results.Single().Status);
    }

    [Fact]
    public void Normalize_WithNoLookups_ReturnsEmptyResultList()
    {
        var envelope = ScorecardGraphNormalizer.Normalize(
            "net10.0", [], "p.csproj", "/wd", "0.1.0");

        Assert.Empty(envelope.Payload.Results);
    }

    [Fact]
    public void PayloadSchemaId_FollowsNamingConvention()
    {
        Assert.Equal("fennec.scorecard.v1", ScorecardGraphNormalizer.PayloadSchemaId);
    }

    [Theory]
    [InlineData("", "p.csproj", "/wd", "0.1.0")]
    [InlineData("net10.0", "", "/wd", "0.1.0")]
    [InlineData("net10.0", "p.csproj", "", "0.1.0")]
    [InlineData("net10.0", "p.csproj", "/wd", "")]
    public void Normalize_ThrowsForMissingRequiredArguments(
        string targetFramework, string projectPath, string workingDirectory, string producerVersion)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            ScorecardGraphNormalizer.Normalize(targetFramework, [], projectPath, workingDirectory, producerVersion));
    }
}
