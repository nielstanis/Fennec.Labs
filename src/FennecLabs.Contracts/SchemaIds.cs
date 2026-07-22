namespace FennecLabs.Contracts;

/// <summary>
/// Naming helpers for canonical schema identifiers, per the architecture spine convention:
/// envelope schema ids follow <c>fennec.envelope.v{major}</c> and payload schema ids follow
/// <c>fennec.&lt;command&gt;.v{major}</c>.
/// </summary>
public static class SchemaIds
{
    /// <summary>Current major version of the canonical envelope schema.</summary>
    public const int CurrentEnvelopeMajorVersion = 1;

    /// <summary>Builds the schema identifier for the canonical envelope at the given major version.</summary>
    public static string Envelope(int majorVersion = CurrentEnvelopeMajorVersion) =>
        $"fennec.envelope.v{majorVersion}";

    /// <summary>Builds the schema identifier for a command's payload at the given major version.</summary>
    public static string Payload(string command, int majorVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        return $"fennec.{command}.v{majorVersion}";
    }
}
