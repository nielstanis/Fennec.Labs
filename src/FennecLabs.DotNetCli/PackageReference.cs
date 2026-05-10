namespace FennecLabs.DotNetCli;

public record PackageReference
{
    public required string Id { get; init; }
    public string? RequestedVersion { get; init; }
    public string? ResolvedVersion { get; init; }
}

