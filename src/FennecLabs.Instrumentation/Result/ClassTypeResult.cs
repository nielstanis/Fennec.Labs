namespace FennecLabs.Instrumentation.Result;

public record ClassTypeResult(string ClassType)
{
    public List<MethodResult> Methods { get; } = [];
}
