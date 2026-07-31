namespace FennecLabs.Cli.Commands.Taint;

/// <summary>
/// Effective taint-analysis options for a single <c>instrument --taint</c> invocation. Additive to
/// the pre-existing instrument parameters — when <see cref="Enabled"/> is <c>false</c> (the
/// <see cref="Disabled"/> singleton), no taint path executes and existing instrument behavior is
/// completely unchanged.
/// </summary>
internal sealed record TaintOptions(
    bool Enabled,
    string? PolicyPath,
    int MaxDepth,
    int TimeoutSeconds,
    bool LlmHandoff,
    bool IncludeThirdParty,
    IReadOnlyList<string> SecondPartyPrefixes)
{
    public static TaintOptions Disabled { get; } = new(
        Enabled: false,
        PolicyPath: null,
        MaxDepth: 8,
        TimeoutSeconds: 120,
        LlmHandoff: false,
        IncludeThirdParty: false,
        SecondPartyPrefixes: []);
}
