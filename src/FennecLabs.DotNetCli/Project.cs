namespace FennecLabs.DotNetCli;

public record Project
{
    public required string Path { get; init; }
    public required List<Framework> Frameworks { get; init; }
}

