namespace FennecLabs.Instrumentation.Result
{
    public class ClassTypeResult(string classtype)
    {
        public List<MethodResult> Methods { get; } = [];
        public string ClassType { get; } = classtype;
    }
}

