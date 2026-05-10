namespace FennecLabs.NuGet;

public class PackageFileInfo
{
    public required string Path { get; set; }
    public required string FullPath { get; set; }
    public long Size { get; set; }
}

