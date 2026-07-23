using System.Reflection;

namespace FennecLabs.Cli.Commands;

/// <summary>
/// Resolves the running Fennec.Labs CLI version for use as the <c>producerVersion</c> field on
/// canonical dashboard artifacts.
/// </summary>
internal static class ProducerVersion
{
    internal static string Current { get; } =
        typeof(ProducerVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(ProducerVersion).Assembly.GetName().Version?.ToString()
        ?? "unknown";
}
