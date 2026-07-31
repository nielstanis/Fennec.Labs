namespace FennecLabs.TaintAnalysis.Models;

/// <summary>
/// A source-code reference produced by <see cref="SymbolMapper"/>, correlating an IL
/// instruction/method to a file/line/column position when debug symbols are available.
/// </summary>
public sealed record SourceRef
{
    /// <summary>Path (or PDB-recorded URL) of the source file, or <c>null</c> when unresolved.</summary>
    public string? File { get; init; }

    /// <summary>1-based start line, or <c>null</c> when unresolved.</summary>
    public int? StartLine { get; init; }

    /// <summary>0-based start column, or <c>null</c> when unresolved.</summary>
    public int? StartColumn { get; init; }

    /// <summary>1-based end line, or <c>null</c> when unresolved.</summary>
    public int? EndLine { get; init; }

    /// <summary>0-based end column, or <c>null</c> when unresolved.</summary>
    public int? EndColumn { get; init; }

    /// <summary>
    /// How precisely this reference was resolved: <c>"exact"</c> (direct sequence point match),
    /// <c>"approximate"</c> (nearest preceding non-hidden sequence point), or
    /// <c>"unresolved"</c> (no usable debug symbols).
    /// </summary>
    public required string Fidelity { get; init; }

    /// <summary>
    /// Hex-encoded metadata token of the containing method, populated only when
    /// <see cref="Fidelity"/> is <c>"unresolved"</c>.
    /// </summary>
    public string? MetadataToken { get; init; }
}
