namespace FennecLabs.DotNetCli.Tests;

public class DependencyGraphNormalizerTests
{
    private static Framework CreateFramework(
        List<PackageReference>? topLevel = null,
        List<PackageReference>? transitive = null) => new()
    {
        FrameworkName = "net10.0",
        TopLevelPackages = topLevel ?? [],
        TransitivePackages = transitive ?? [],
    };

    [Fact]
    public void Normalize_SetsCanonicalEnvelopeMetadata()
    {
        var framework = CreateFramework(
            topLevel: [new PackageReference { Id = "Newtonsoft.Json", RequestedVersion = "13.0.*", ResolvedVersion = "13.0.3" }]);

        var envelope = DependencyGraphNormalizer.Normalize(
            framework,
            projectPath: "src/Sample/Sample.csproj",
            workingDirectory: "/workspaces/Fennec.Labs",
            producerVersion: "0.7.5",
            gitCommit: "abc123",
            producedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal("fennec.envelope.v1", envelope.Schema);
        Assert.Equal("dependencies", envelope.Command);
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
        var framework = CreateFramework(
            topLevel: [new PackageReference { Id = "Newtonsoft.Json", ResolvedVersion = "13.0.3" }]);

        var envelope = DependencyGraphNormalizer.Normalize(
            framework, "p.csproj", "/wd", "0.1.0");

        Assert.Equal("newtonsoft.json", envelope.Payload.Nodes.Single().Id);
    }

    [Fact]
    public void Normalize_MarksTopLevelPackagesAsTopLevel()
    {
        var framework = CreateFramework(
            topLevel: [new PackageReference { Id = "Top.Package", ResolvedVersion = "1.0.0" }],
            transitive: [new PackageReference { Id = "Transitive.Package", ResolvedVersion = "2.0.0" }]);

        var envelope = DependencyGraphNormalizer.Normalize(framework, "p.csproj", "/wd", "0.1.0");

        var top = envelope.Payload.Nodes.Single(n => n.Id == "top.package");
        var transitive = envelope.Payload.Nodes.Single(n => n.Id == "transitive.package");

        Assert.True(top.IsTopLevel);
        Assert.False(transitive.IsTopLevel);
    }

    [Fact]
    public void Normalize_DeduplicatesPackageAppearingInBothLists_PreferringTopLevel()
    {
        var framework = CreateFramework(
            topLevel: [new PackageReference { Id = "Shared.Package", ResolvedVersion = "1.0.0" }],
            transitive: [new PackageReference { Id = "shared.package", ResolvedVersion = "1.0.0" }]);

        var envelope = DependencyGraphNormalizer.Normalize(framework, "p.csproj", "/wd", "0.1.0");

        var node = Assert.Single(envelope.Payload.Nodes);
        Assert.Equal("shared.package", node.Id);
        Assert.True(node.IsTopLevel);
    }

    [Fact]
    public void Normalize_DeduplicatesTransitiveAppearingBeforeTopLevelIsFound_StillPrefersTopLevel()
    {
        // Transitive list is always processed after top-level in this normalizer, but this test
        // guards the invariant explicitly regardless of internal processing order.
        var framework = CreateFramework(
            topLevel: [new PackageReference { Id = "Shared.Package", ResolvedVersion = "1.0.0" }],
            transitive:
            [
                new PackageReference { Id = "Other.Package", ResolvedVersion = "3.0.0" },
                new PackageReference { Id = "Shared.Package", ResolvedVersion = "1.0.0" },
            ]);

        var envelope = DependencyGraphNormalizer.Normalize(framework, "p.csproj", "/wd", "0.1.0");

        Assert.Equal(2, envelope.Payload.Nodes.Count);
        Assert.True(envelope.Payload.Nodes.Single(n => n.Id == "shared.package").IsTopLevel);
        Assert.False(envelope.Payload.Nodes.Single(n => n.Id == "other.package").IsTopLevel);
    }

    [Fact]
    public void Normalize_FallsBackToUnknownWhenResolvedVersionMissing()
    {
        var framework = CreateFramework(
            topLevel: [new PackageReference { Id = "No.Version", ResolvedVersion = null }]);

        var envelope = DependencyGraphNormalizer.Normalize(framework, "p.csproj", "/wd", "0.1.0");

        Assert.Equal("unknown", envelope.Payload.Nodes.Single().ResolvedVersion);
    }

    [Fact]
    public void Normalize_PreservesRequestedVersion()
    {
        var framework = CreateFramework(
            topLevel: [new PackageReference { Id = "Pkg", RequestedVersion = "1.*", ResolvedVersion = "1.2.3" }]);

        var envelope = DependencyGraphNormalizer.Normalize(framework, "p.csproj", "/wd", "0.1.0");

        Assert.Equal("1.*", envelope.Payload.Nodes.Single().RequestedVersion);
    }

    [Fact]
    public void Normalize_WithNoPackages_ReturnsEmptyNodeList()
    {
        var framework = CreateFramework();

        var envelope = DependencyGraphNormalizer.Normalize(framework, "p.csproj", "/wd", "0.1.0");

        Assert.Empty(envelope.Payload.Nodes);
    }

    [Fact]
    public void Normalize_DefaultsProducedAtToUtcNow_WhenNotSpecified()
    {
        var framework = CreateFramework();
        var before = DateTimeOffset.UtcNow;

        var envelope = DependencyGraphNormalizer.Normalize(framework, "p.csproj", "/wd", "0.1.0");

        var after = DateTimeOffset.UtcNow;
        Assert.InRange(envelope.ProducedAt, before, after);
    }

    [Fact]
    public void PayloadSchemaId_FollowsNamingConvention()
    {
        Assert.Equal("fennec.dependencies.v1", DependencyGraphNormalizer.PayloadSchemaId);
    }

    [Theory]
    [InlineData(null, "p.csproj", "/wd", "0.1.0")]
    [InlineData("net10.0", "", "/wd", "0.1.0")]
    [InlineData("net10.0", "p.csproj", "", "0.1.0")]
    [InlineData("net10.0", "p.csproj", "/wd", "")]
    public void Normalize_ThrowsForMissingRequiredArguments(
        string? frameworkName, string projectPath, string workingDirectory, string producerVersion)
    {
        if (frameworkName == null)
        {
            Assert.Throws<ArgumentNullException>(() =>
                DependencyGraphNormalizer.Normalize(null!, projectPath, workingDirectory, producerVersion));
            return;
        }

        var framework = CreateFramework();
        Assert.ThrowsAny<ArgumentException>(() =>
            DependencyGraphNormalizer.Normalize(framework, projectPath, workingDirectory, producerVersion));
    }
}
