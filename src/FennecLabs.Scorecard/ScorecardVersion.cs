namespace FennecLabs.Scorecard;

public record ScorecardVersion
{
    public required string Version { get; init; }
    public required string Commit { get; init; }
}
