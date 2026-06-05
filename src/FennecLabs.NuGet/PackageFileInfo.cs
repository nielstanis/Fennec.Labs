namespace FennecLabs.NuGet;

public record PackageFileInfo
{
    public required string Path { get; init; }
    public required string FullPath { get; init; }
    public long Size { get; init; }
}
