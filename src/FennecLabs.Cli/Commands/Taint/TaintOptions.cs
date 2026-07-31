namespace FennecLabs.Cli.Commands.Taint;

/// <summary>
/// Options controlling the optional taint analysis pass for `fennec instrument`.
/// When <see cref="Enabled"/> is false, no taint-specific behavior executes and the
/// existing instrument workflow is completely unchanged.
/// </summary>
internal record TaintOptions(
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
