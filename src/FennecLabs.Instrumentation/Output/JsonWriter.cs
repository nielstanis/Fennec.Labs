using System.Text.Json;
using FennecLabs.Instrumentation.Result;

namespace FennecLabs.Instrumentation.Output
{
    public class JsonWriter : Writer
    {
        public JsonWriter(string outputFolder) : base(outputFolder)
        {
        }

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
                // Extract directory from relative path (e.g., "lib/net6.0/MyLib.dll" -> "lib/net6.0")
                var relativeDir = Path.GetDirectoryName(relativePath);
                if (!string.IsNullOrWhiteSpace(relativeDir))
                {
                    outputDir = Path.Combine(outputDir, relativeDir);
                }
            }
            
            string outputFile = Path.Combine(outputDir, $"{filename}.json");

            bool result = true;
            try
            {
                Directory.CreateDirectory(outputDir);
                using (var f = File.Create(outputFile))
                {
                    await JsonSerializer.SerializeAsync(f, assemblyResult);
                }
            }
            catch (Exception)
            {
                result = false;
            }
            return result;
        }
    }
}

