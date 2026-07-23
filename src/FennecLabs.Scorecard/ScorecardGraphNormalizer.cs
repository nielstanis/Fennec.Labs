using FennecLabs.Contracts;

namespace FennecLabs.Scorecard;

/// <summary>
/// Normalizes package-level OpenSSF Scorecard lookup outcomes into the canonical
/// <see cref="ScorecardGraphPayload"/> shape wrapped in a
/// <see cref="DashboardArtifactEnvelope{TPayload}"/>, per Story 1.3: results are keyed by
/// normalized package identity, and missing/failed lookups are represented explicitly rather
/// than omitted.
/// </summary>
public static class ScorecardGraphNormalizer
{
    /// <summary>Command name used for the envelope's <c>command</c> field and payload schema id.</summary>
    public const string CommandName = "scorecard";

    /// <summary>Major version of the <c>fennec.scorecard</c> payload schema.</summary>
    public const int PayloadSchemaMajorVersion = 1;

    /// <summary>Schema identifier for the <see cref="ScorecardGraphPayload"/> shape, e.g. <c>fennec.scorecard.v1</c>.</summary>
    public static string PayloadSchemaId => SchemaIds.Payload(CommandName, PayloadSchemaMajorVersion);

    private const string UnavailableErrorCode = "scorecard.unavailable";
    private const string FetchFailedErrorCode = "scorecard.fetch_failed";

    /// <summary>
    /// Normalizes a set of package scorecard lookups for a single target framework into a
    /// canonical scorecard graph artifact.
    /// </summary>
    /// <param name="targetFramework">Target framework moniker the lookups were performed for.</param>
    /// <param name="lookups">Scorecard lookup outcomes for each package analyzed.</param>
    /// <param name="projectPath">Path to the project file that was analyzed.</param>
    /// <param name="workingDirectory">Working directory the producing command was invoked from.</param>
    /// <param name="producerVersion">Version of the producer (e.g. the Fennec.Labs CLI) generating this artifact.</param>
    /// <param name="gitCommit">Git commit SHA of the analyzed source tree, when available.</param>
    /// <param name="producedAt">Timestamp the artifact was produced at; defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    public static DashboardArtifactEnvelope<ScorecardGraphPayload> Normalize(
        string targetFramework,
        IReadOnlyList<PackageScorecardLookup> lookups,
        string projectPath,
        string workingDirectory,
        string producerVersion,
        string? gitCommit = null,
        DateTimeOffset? producedAt = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetFramework);
        ArgumentNullException.ThrowIfNull(lookups);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(producerVersion);

        var payload = new ScorecardGraphPayload
        {
            TargetFramework = targetFramework,
            Results = lookups.Select(NormalizeLookup).ToList(),
        };

        return new DashboardArtifactEnvelope<ScorecardGraphPayload>
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
                TargetFramework = targetFramework,
                GitCommit = gitCommit,
            },
            Payload = payload,
        };
    }

    private static ScorecardPackageResult NormalizeLookup(PackageScorecardLookup lookup)
    {
        var id = NormalizeId(lookup.PackageId);

        if (!string.IsNullOrWhiteSpace(lookup.ErrorMessage))
        {
            return new ScorecardPackageResult
            {
                PackageId = id,
                PackageVersion = lookup.PackageVersion,
                Status = ScorecardStatus.Error,
                Error = new ArtifactError
                {
                    Code = FetchFailedErrorCode,
                    Message = lookup.ErrorMessage,
                    Target = id,
                },
            };
        }

        if (lookup.Result is null)
        {
            return new ScorecardPackageResult
            {
                PackageId = id,
                PackageVersion = lookup.PackageVersion,
                Status = ScorecardStatus.Unavailable,
                Error = new ArtifactError
                {
                    Code = UnavailableErrorCode,
                    Message = "No scorecard data could be located for this package.",
                    Target = id,
                },
            };
        }

        var result = lookup.Result;
        return new ScorecardPackageResult
        {
            PackageId = id,
            PackageVersion = lookup.PackageVersion,
            Status = ScorecardStatus.Available,
            Score = result.Score,
            RepoName = result.Repo.Name,
            RepoCommit = result.Repo.Commit,
            ScorecardDate = result.Date,
            ScorecardVersion = result.Scorecard.Version,
            Checks = result.Checks
                .Select(c => new ScorecardCheckResult { Name = c.Name, Score = c.Score, Reason = c.Reason })
                .ToList(),
        };
    }

    /// <summary>Normalizes a package id to its stable, lowercase invariant-culture identity.</summary>
    private static string NormalizeId(string id) => id.ToLowerInvariant();
}
