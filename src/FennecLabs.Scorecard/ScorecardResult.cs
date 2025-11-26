namespace FennecLabs.Scorecard;

public class ScorecardResult
{
    public required string Date { get; set; }
    public required Repo Repo { get; set; }
    public required ScorecardVersion Scorecard { get; set; }
    public decimal Score { get; set; }
    public required List<ScorecardCheck> Checks { get; set; }
    public string? Metadata { get; set; }
}

