using System.Text.Json.Serialization;

namespace FennecLabs.NuGet;

public class AppSettings
{
    [JsonPropertyName("defaultFeed")]
    public string? DefaultFeed { get; set; }

    [JsonPropertyName("feeds")]
    public List<FeedConfiguration> Feeds { get; set; } = [];

    [JsonPropertyName("configDirectory")]
    public string? ConfigDirectory { get; set; }
}

