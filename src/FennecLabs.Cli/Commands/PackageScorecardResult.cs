using FennecLabs.Scorecard;

namespace FennecLabs.Cli.Commands;

internal sealed class PackageScorecardResult
{
    public string PackageId { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = string.Empty;
    public ScorecardResult? Scorecard { get; set; }
    public string? Error { get; set; }
}
