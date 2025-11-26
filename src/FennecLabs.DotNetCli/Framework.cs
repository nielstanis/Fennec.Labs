using System.Text.Json.Serialization;

namespace FennecLabs.DotNetCli;

public record Framework
{
    [JsonPropertyName("framework")]
    public required string FrameworkName { get; init; }
    
    public required List<PackageReference> TopLevelPackages { get; init; }
    
    public List<PackageReference> TransitivePackages { get; init; } = new();
}

