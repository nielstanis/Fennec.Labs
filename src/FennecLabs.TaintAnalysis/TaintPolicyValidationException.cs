namespace FennecLabs.TaintAnalysis;

/// <summary>
/// Raised when a taint policy file (built-in or user-supplied via <c>--taint-policy</c>) fails
/// to parse or fails schema validation. Carries the offending file path and, where known, the
/// specific field that failed validation so the CLI can report an actionable error.
/// </summary>
public sealed class TaintPolicyValidationException : Exception
{
    /// <summary>Path to the policy file that failed to load, or <c>"&lt;built-in&gt;"</c>.</summary>
    public string FilePath { get; }

    /// <summary>Name of the field that failed validation, when known.</summary>
    public string? FieldName { get; }

    public TaintPolicyValidationException(string filePath, string? fieldName, string message)
        : base(FormatMessage(filePath, fieldName, message))
    {
        FilePath = filePath;
        FieldName = fieldName;
    }

    public TaintPolicyValidationException(string filePath, string? fieldName, string message, Exception innerException)
        : base(FormatMessage(filePath, fieldName, message), innerException)
    {
        FilePath = filePath;
        FieldName = fieldName;
    }

    private static string FormatMessage(string filePath, string? fieldName, string message) =>
        fieldName is null
            ? $"Invalid taint policy at '{filePath}': {message}"
            : $"Invalid taint policy at '{filePath}' (field '{fieldName}'): {message}";
}
