using FennecLabs.Instrumentation.Result;

namespace FennecLabs.Instrumentation.Output
{
    public abstract class Writer
    {
        protected readonly string _outputFolder;

        public Writer(string outputFolder)
        {
            _outputFolder = outputFolder;
        }

        public abstract Task<bool> WriteOutputAsync(
            AssemblyResult assemblyResult,
            string? relativePath,
            CancellationToken cancellationToken);

        public Task<bool> WriteOutputAsync(AssemblyResult assemblyResult)
            => WriteOutputAsync(assemblyResult, null, default);

        public Task<bool> WriteOutputAsync(AssemblyResult assemblyResult, string? relativePath)
            => WriteOutputAsync(assemblyResult, relativePath, default);

        public Task<bool> WriteOutputAsync(AssemblyResult assemblyResult, CancellationToken cancellationToken)
            => WriteOutputAsync(assemblyResult, null, cancellationToken);

        protected string ResolveOutputPath(string assemblyFilePath, string? relativePath, string extension)
        {
            string filename = Path.GetFileNameWithoutExtension(assemblyFilePath);
            string outputDir = _outputFolder;

            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                var relativeDir = Path.GetDirectoryName(relativePath);
                if (!string.IsNullOrWhiteSpace(relativeDir))
                    outputDir = Path.Combine(outputDir, relativeDir);
            }

            Directory.CreateDirectory(outputDir);
            return Path.Combine(outputDir, $"{filename}.{extension}");
        }
    }
}
