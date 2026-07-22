namespace FennecLabs.Contracts;

/// <summary>
/// A typed, structured error representation for canonical dashboard artifacts. Producers must
/// use this shape (rather than a bare string message) so consumers can reliably branch on
/// <see cref="Code"/> and surface <see cref="Target"/>/<see cref="Details"/> without parsing
/// free-form text.
/// </summary>
public sealed record ArtifactError
{
    /// <summary>Stable, machine-readable error code (e.g. <c>scorecard.unavailable</c>).</summary>
    public required string Code { get; init; }

    /// <summary>Human-readable description of the error.</summary>
    public required string Message { get; init; }

    /// <summary>Optional identifier of the item the error relates to (e.g. a package id).</summary>
    public string? Target { get; init; }

    /// <summary>Optional structured, additional context for the error.</summary>
    public IReadOnlyDictionary<string, string>? Details { get; init; }
}
