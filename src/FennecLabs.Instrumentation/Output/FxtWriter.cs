using FennecLabs.Instrumentation.Result;

namespace FennecLabs.Instrumentation.Output
{
    public class FxtWriter(string outputFolder) : Writer(outputFolder)
    {
        public override async Task<bool> WriteOutputAsync(AssemblyResult assemblyResult)
        {
            return await WriteOutputAsync(assemblyResult, null);
        }

        public override async Task<bool> WriteOutputAsync(AssemblyResult assemblyResult, string? relativePath)
        {
            string filename = Path.GetFileNameWithoutExtension(assemblyResult.FilePath);

            string outputDir = _outputFolder;
            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                var relativeDir = Path.GetDirectoryName(relativePath);
                if (!string.IsNullOrWhiteSpace(relativeDir))
                    outputDir = Path.Combine(outputDir, relativeDir);
            }

            string outputFile = Path.Combine(outputDir, $"{filename}.fxt");
            Directory.CreateDirectory(outputDir);

            await using var f = File.CreateText(outputFile);
            foreach (var t in assemblyResult.Types.OrderBy(x => x.ClassType))
                foreach (var m in t.Methods.OrderBy(z => z.Name))
                    foreach (var i in m.Invocations.OrderBy(r => r.Sequence))
                        await f.WriteLineAsync($"{t.ClassType}::{m.Name}({m.Parameters})::{i.Invocation}");

            return true;
        }
    }
}
