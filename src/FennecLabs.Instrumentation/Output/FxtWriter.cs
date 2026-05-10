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
            
            string outputDir = Path.Combine(_outputFolder, "fenneclabs");
            if (!string.IsNullOrWhiteSpace(relativePath))
            {
                // Extract directory from relative path (e.g., "lib/net6.0/MyLib.dll" -> "lib/net6.0")
                var relativeDir = Path.GetDirectoryName(relativePath);
                if (!string.IsNullOrWhiteSpace(relativeDir))
                {
                    outputDir = Path.Combine(outputDir, relativeDir);
                }
            }
            
            string outputFile = Path.Combine(outputDir, $"{filename}.fxt");
            Console.WriteLine(outputFile);

            bool result = true;
            //try
            {
                Directory.CreateDirectory(outputDir);
                await using (var f = File.CreateText(outputFile))
                {
                    //for flat file the ordering is important, order by type, methods and sequence of invocation. 
                    foreach (var t in assemblyResult.Types.OrderBy(x => x.ClassType))
                    {
                        foreach (var m in t.Methods.OrderBy(z => z.Name))
                        {
                            foreach (var i in m.Invocations.OrderBy(r => r.Sequence))
                            {
                                await f.WriteLineAsync($"{t.ClassType}::{m.Name}({m.Parameters})::{i.Invocation}");
                            }
                        }

                    }
                }
            }
            // catch (Exception ex)
            // {
            //     Console.WriteLine(ex.Message);
            //     result = false;
            // }
            return result;
        }

    }
}

