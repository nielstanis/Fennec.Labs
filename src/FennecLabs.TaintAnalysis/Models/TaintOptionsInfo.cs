namespace FennecLabs.TaintAnalysis.Models;

/// <summary>Echo of the effective taint analysis options used to produce a <see cref="TaintPayload"/>.</summary>
public sealed record TaintOptionsInfo
{
    /// <summary>Maximum call-chain depth walked during propagation (not yet used — no propagation in this slice).</summary>
    public required int MaxDepth { get; init; }

    /// <summary>Whether an LLM handoff artifact was requested.</summary>
    public required bool LlmHandoff { get; init; }

    /// <summary>Whether third-party (NuGet) assemblies are walked.</summary>
    public required bool IncludeThirdParty { get; init; }

    /// <summary>Package/namespace prefixes treated as second-party.</summary>
    public required IReadOnlyList<string> SecondPartyPrefixes { get; init; }
}
