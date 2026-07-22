using FennecLabs.Contracts;

namespace FennecLabs.DotNetCli;

/// <summary>
/// Normalizes upstream <c>dotnet package list --include-transitive --format json</c> output into
/// the canonical <see cref="DependencyGraphPayload"/> shape wrapped in a
/// <see cref="DashboardArtifactEnvelope{TPayload}"/>, per architecture decision AD-7.
/// </summary>
public static class DependencyGraphNormalizer
{
    /// <summary>Command name used for the envelope's <c>command</c> field and payload schema id.</summary>
    public const string CommandName = "dependencies";

    /// <summary>Major version of the <c>fennec.dependencies</c> payload schema.</summary>
    public const int PayloadSchemaMajorVersion = 1;

    /// <summary>Schema identifier for the <see cref="DependencyGraphPayload"/> shape, e.g. <c>fennec.dependencies.v1</c>.</summary>
    public static string PayloadSchemaId => SchemaIds.Payload(CommandName, PayloadSchemaMajorVersion);

    /// <summary>Fallback resolved version used when upstream output omits a resolved version.</summary>
    private const string UnknownVersion = "unknown";

    /// <summary>
    /// Normalizes a single resolved <see cref="Framework"/> (as produced by
    /// <see cref="DotnetCliExecutor.GetPackageListAsync(string, CancellationToken)"/>) into a
    /// canonical dependency graph artifact.
    /// </summary>
    /// <param name="framework">The framework whose top-level and transitive packages are normalized.</param>
    /// <param name="projectPath">Path to the project file that was analyzed.</param>
    /// <param name="workingDirectory">Working directory the producing command was invoked from.</param>
    /// <param name="producerVersion">Version of the producer (e.g. the Fennec.Labs CLI) generating this artifact.</param>
    /// <param name="gitCommit">Git commit SHA of the analyzed source tree, when available.</param>
    /// <param name="producedAt">Timestamp the artifact was produced at; defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    public static DashboardArtifactEnvelope<DependencyGraphPayload> Normalize(
        Framework framework,
        string projectPath,
        string workingDirectory,
        string producerVersion,
        string? gitCommit = null,
        DateTimeOffset? producedAt = null)
    {
        ArgumentNullException.ThrowIfNull(framework);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(producerVersion);

        var payload = new DependencyGraphPayload
        {
            TargetFramework = framework.FrameworkName,
            Nodes = BuildNodes(framework),
        };

        return new DashboardArtifactEnvelope<DependencyGraphPayload>
        {
            Schema = SchemaIds.Envelope(),
            SchemaVersion = "1.0.0",
            Command = CommandName,
            ProducedAt = producedAt ?? DateTimeOffset.UtcNow,
            ProducerVersion = producerVersion,
            SourceContext = new ArtifactSourceContext
            {
                ProjectPath = projectPath,
                WorkingDirectory = workingDirectory,
                TargetFramework = framework.FrameworkName,
                GitCommit = gitCommit,
            },
            Payload = payload,
        };
    }

    /// <summary>Builds the deduplicated, identity-normalized node list for a single framework.</summary>
    private static IReadOnlyList<DependencyNode> BuildNodes(Framework framework)
    {
        var nodesById = new Dictionary<string, DependencyNode>(StringComparer.Ordinal);
        var order = new List<string>();

        // Top-level packages are processed first so that a package appearing in both the
        // top-level and transitive lists is always represented with IsTopLevel = true.
        foreach (var package in framework.TopLevelPackages)
        {
            AddOrUpdate(nodesById, order, package, isTopLevel: true);
        }

        foreach (var package in framework.TransitivePackages)
        {
            AddOrUpdate(nodesById, order, package, isTopLevel: false);
        }

        return order.Select(id => nodesById[id]).ToList();
    }

    private static void AddOrUpdate(
        Dictionary<string, DependencyNode> nodesById,
        List<string> order,
        PackageReference package,
        bool isTopLevel)
    {
        var id = NormalizeId(package.Id);

        if (nodesById.TryGetValue(id, out var existing))
        {
            if (isTopLevel && !existing.IsTopLevel)
            {
                nodesById[id] = existing with { IsTopLevel = true };
            }

            return;
        }

        nodesById[id] = new DependencyNode
        {
            Id = id,
            ResolvedVersion = string.IsNullOrWhiteSpace(package.ResolvedVersion)
                ? UnknownVersion
                : package.ResolvedVersion,
            RequestedVersion = package.RequestedVersion,
            IsTopLevel = isTopLevel,
        };
        order.Add(id);
    }

    /// <summary>Normalizes a package id to its stable, lowercase invariant-culture identity.</summary>
    private static string NormalizeId(string id) => id.ToLowerInvariant();
}
