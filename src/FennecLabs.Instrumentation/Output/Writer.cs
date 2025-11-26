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

        protected bool EnsureFolderCreated()     
        {
            var result = Directory.Exists(_outputFolder);
            Console.WriteLine($"Creating output folder: {_outputFolder}");
            Console.WriteLine($"Created: {result.ToString()}");
            if (!result)
            {
                Console.WriteLine("Creating output folder: " + _outputFolder);
                var x = Directory.CreateDirectory(_outputFolder);
                result = Directory.Exists(_outputFolder);
            }
            return result;
        }

        public abstract Task<bool> WriteOutputAsync(AssemblyResult assemblyResult);
    }
}

