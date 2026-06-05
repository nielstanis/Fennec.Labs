namespace FennecLabs.Scorecard;

public record ScorecardCheck
{
    public required string Name { get; init; }
    public int Score { get; init; }
    public string? Reason { get; init; }
    public ScorecardCheckDocumentation? Documentation { get; init; }
    public List<string> Details { get; init; } = [];
}

public record ScorecardCheckDocumentation
{
    public string? Short { get; init; }
    public string? Url { get; init; }
}
