namespace FennecLabs.DotNetCli;

public record PackageListResult
{
    public int Version { get; init; }
    public string? Parameters { get; init; }
    public required List<Project> Projects { get; init; }
}

