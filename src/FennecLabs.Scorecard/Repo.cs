namespace FennecLabs.Scorecard;

public record Repo
{
    public required string Name { get; init; }
    public string? Commit { get; init; }
}
