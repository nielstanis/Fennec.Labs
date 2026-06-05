using System.Text.Json.Serialization;

namespace FennecLabs.Instrumentation.Result;

public class AssemblyResult
{
    public string Assembly { get; }
    [JsonIgnore] public string FilePath { get; }
    public List<ClassTypeResult> Types { get; } = [];
    [JsonIgnore] public Exception? ExceptionOccurred { get; }
    [JsonIgnore] public bool HasError => ExceptionOccurred != null;

    public AssemblyResult(string assembly, string filePath)
    {
        Assembly = assembly;
        FilePath = filePath;
    }

    public AssemblyResult(string assembly, string filePath, Exception exception)
        : this(assembly, filePath)
    {
        ExceptionOccurred = exception;
    }
}
