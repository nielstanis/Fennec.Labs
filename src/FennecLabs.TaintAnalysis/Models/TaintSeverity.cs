namespace FennecLabs.TaintAnalysis.Models;

/// <summary>
/// Severity assigned to a sink rule (and, transitively, to findings raised against it), per the
/// v1 policy format.
/// </summary>
public enum TaintSeverity
{
    Low,
    Medium,
    High,
    Critical,
}
