namespace FennecLabs.Instrumentation.Result;

public record MethodResult(string Name, string Parameters)
{
    public List<InvocationResult> Invocations { get; } = [];
}
