namespace FennecLabs.Scorecard;

public record ScorecardResult
{
    public required string Date { get; init; }
    public required Repo Repo { get; init; }
    public required ScorecardVersion Scorecard { get; init; }
    public decimal Score { get; init; }
    public required List<ScorecardCheck> Checks { get; init; }
    public string? Metadata { get; init; }
}
