namespace FennecLabs.TaintAnalysis.Models;

/// <summary>
/// Per-execution-context override of a rule's severity/confidence, keyed by context identifier
/// (e.g. <c>web-aspnet</c>, <c>console</c>) in the containing <see cref="TaintRule.ContextOverrides"/>
/// dictionary. Allows the same policy rule to carry different weight depending on the detected
/// hosting context of the analyzed assembly (AD-13).
/// </summary>
public sealed record TaintContextOverride
{
    /// <summary>Severity to apply instead of <see cref="TaintRule.Severity"/> for this context.</summary>
    public TaintSeverity? Severity { get; init; }

    /// <summary>Confidence to apply instead of <see cref="TaintRule.Confidence"/> for this context.</summary>
    public double? Confidence { get; init; }
}
