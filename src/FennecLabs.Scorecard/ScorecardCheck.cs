namespace FennecLabs.Scorecard;

public class ScorecardCheck
{
    public required string Name { get; set; }
    public int Score { get; set; }
    public string? Reason { get; set; }
    public ScorecardCheckDocumentation? Documentation { get; set; }
    public List<string> Details { get; set; } = [];
}

public class ScorecardCheckDocumentation
{
    public string? Short { get; set; }
    public string? Url { get; set; }
}

