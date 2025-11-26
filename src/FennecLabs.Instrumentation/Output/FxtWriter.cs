using FennecLabs.Instrumentation.Result;

namespace FennecLabs.Instrumentation.Output
{

    public class FxtWriter(string outputFolder) : Writer(outputFolder)
    {
        public override async Task<bool> WriteOutputAsync(AssemblyResult assemblyResult)
        {
            string filename = Path.GetFileNameWithoutExtension(assemblyResult.FilePath);
           
            string outputFile = Path.Combine(".fennec", $"{filename}.fxt");
            Console.WriteLine(outputFile);

            bool result = true;
            //try
            {
                EnsureFolderCreated();
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

