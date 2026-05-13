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

        public abstract Task<bool> WriteOutputAsync(AssemblyResult assemblyResult);

        public virtual Task<bool> WriteOutputAsync(AssemblyResult assemblyResult, string? relativePath)
        {
            return WriteOutputAsync(assemblyResult);
        }
    }
}
