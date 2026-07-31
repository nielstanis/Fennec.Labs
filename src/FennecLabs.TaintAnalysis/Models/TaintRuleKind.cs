namespace FennecLabs.TaintAnalysis.Models;

/// <summary>
/// Role a <see cref="TaintRule"/> plays in the taint state machine, per the v1 policy format
/// (see <c>fennec.taint.policy.v1</c>).
/// </summary>
public enum TaintRuleKind
{
    /// <summary>Call site whose return value introduces taint (e.g. reading HTTP input).</summary>
    Source,

    /// <summary>Call site where a tainted argument constitutes a security finding.</summary>
    Sink,

    /// <summary>Call site that forwards taint from its arguments to its return value.</summary>
    Propagator,

    /// <summary>Call site that removes taint from its arguments/output.</summary>
    Sanitizer,
}
