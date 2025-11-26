using System.Text.Json;

namespace FennecLabs.DotNetCli;

public static class DotnetCliResultExtensions
{
    public static PackageListResult? DeserializePackageList(this DotnetCliResult result)
    {
        if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return null;
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<PackageListResult>(result.StandardOutput, options);
    }
}

