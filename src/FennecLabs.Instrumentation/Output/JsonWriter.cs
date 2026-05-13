using System.Text.Json;
using FennecLabs.Instrumentation.Result;

namespace FennecLabs.Instrumentation.Output
{
    public class JsonWriter : Writer
    {
        public JsonWriter(string outputFolder) : base(outputFolder)
        {
        }

        public override async Task<bool> WriteOutputAsync(
            AssemblyResult assemblyResult,
            string? relativePath,
            CancellationToken cancellationToken)
        {
            string outputFile = ResolveOutputPath(assemblyResult.FilePath, relativePath, "json");

            using var f = File.Create(outputFile);
            await JsonSerializer.SerializeAsync(f, assemblyResult, cancellationToken: cancellationToken);

            return true;
        }
    }
}
