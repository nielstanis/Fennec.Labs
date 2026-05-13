using FennecLabs.Instrumentation.Result;

namespace FennecLabs.Instrumentation.Output
{
    public class FxtWriter(string outputFolder) : Writer(outputFolder)
    {
        public override async Task<bool> WriteOutputAsync(
            AssemblyResult assemblyResult,
            string? relativePath,
            CancellationToken cancellationToken)
        {
            string outputFile = ResolveOutputPath(assemblyResult.FilePath, relativePath, "fxt");

            await using var f = File.CreateText(outputFile);
            foreach (var t in assemblyResult.Types.OrderBy(x => x.ClassType))
                foreach (var m in t.Methods.OrderBy(z => z.Name))
                    foreach (var i in m.Invocations.OrderBy(r => r.Sequence))
                        await f.WriteLineAsync(
                            $"{t.ClassType}::{m.Name}({m.Parameters})::{i.Invocation}".AsMemory(),
                            cancellationToken);

            return true;
        }
    }
}
