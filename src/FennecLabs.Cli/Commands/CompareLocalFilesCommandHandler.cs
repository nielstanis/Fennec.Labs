using System.Text.Json;
using FennecLabs.AssemblyDiff;
using FennecLabs.Cli.Rendering;
using Mono.Cecil;
using Spectre.Console;

namespace FennecLabs.Cli.Commands;

internal class CompareLocalFilesCommandHandler
{
    public async Task<int> ExecuteAsync(string file1, string file2, OutputMode outputMode)
    {
        if (!File.Exists(file1))
        {
            Console.Error.WriteLine($"File not found: {file1}");
            return 1;
        }
        if (!File.Exists(file2))
        {
            Console.Error.WriteLine($"File not found: {file2}");
            return 1;
        }

        var ext1 = Path.GetExtension(file1).ToLowerInvariant();
        var ext2 = Path.GetExtension(file2).ToLowerInvariant();

        if (ext1 != ext2)
        {
            Console.Error.WriteLine(
                $"Both files must have the same extension (.dll or .nupkg). Got '{ext1}' and '{ext2}'.");
            return 1;
        }

        if (ext1 != ".dll" && ext1 != ".nupkg")
        {
            Console.Error.WriteLine($"Files must be .dll or .nupkg. Got: {ext1}");
            return 1;
        }

        if (ext1 == ".dll")
            return CompareDlls(file1, file2, outputMode);
        return await CompareNupkgsAsync(file1, file2, outputMode);
    }

    private static int CompareDlls(string file1, string file2, OutputMode outputMode)
    {
        try
        {
            using var assembly1 = AssemblyDefinition.ReadAssembly(file1);
            using var assembly2 = AssemblyDefinition.ReadAssembly(file2);
            var result = new AssemblyComparer(assembly1, assembly2).Compare();
            var dllResults = new List<DllDiffResult> { new(Path.GetFileName(file1), result, null) };
            return EmitResult(
                file1, file2, dllResults, [], [],
                result.AreEqual ? 1 : 0, result.AreEqual ? 0 : 1, 0,
                outputMode);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error comparing files: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> CompareNupkgsAsync(string file1, string file2, OutputMode outputMode)
    {
        string? temp1 = null;
        string? temp2 = null;

        try
        {
            temp1 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            temp2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(temp1);
            Directory.CreateDirectory(temp2);

            if (outputMode == OutputMode.Human)
            {
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("grey"))
                    .StartAsync($"Extracting {Path.GetFileName(file1)}…",
                        async _ => await NupkgHelper.ExtractAsync(file1, temp1));
                await AnsiConsole.Status().Spinner(Spinner.Known.Dots).SpinnerStyle(Style.Parse("grey"))
                    .StartAsync($"Extracting {Path.GetFileName(file2)}…",
                        async _ => await NupkgHelper.ExtractAsync(file2, temp2));
            }
            else
            {
                await NupkgHelper.ExtractAsync(file1, temp1);
                await NupkgHelper.ExtractAsync(file2, temp2);
            }

            var dlls1 = NupkgHelper.GetDlls(temp1);
            var dlls2 = NupkgHelper.GetDlls(temp2);

            var matchingDlls = dlls1.Keys.Intersect(dlls2.Keys).ToList();
            var onlyIn1 = dlls1.Keys.Except(dlls2.Keys).ToList();
            var onlyIn2 = dlls2.Keys.Except(dlls1.Keys).ToList();

            if (matchingDlls.Count == 0)
            {
                Console.Error.WriteLine("No matching DLL files found to compare.");
                return 0;
            }

            var dllResults = new List<DllDiffResult>();
            int identicalCount = 0, differentCount = 0, errorCount = 0;

            foreach (var dllPath in matchingDlls)
            {
                try
                {
                    using var a1 = AssemblyDefinition.ReadAssembly(dlls1[dllPath].FullPath);
                    using var a2 = AssemblyDefinition.ReadAssembly(dlls2[dllPath].FullPath);
                    var result = new AssemblyComparer(a1, a2).Compare();
                    dllResults.Add(new DllDiffResult(dllPath, result, null));
                    if (result.AreEqual) identicalCount++;
                    else differentCount++;
                }
                catch (Exception ex)
                {
                    dllResults.Add(new DllDiffResult(dllPath, null, ex.Message));
                    errorCount++;
                }
            }

            return EmitResult(
                file1, file2, dllResults, onlyIn1, onlyIn2,
                identicalCount, differentCount, errorCount, outputMode);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error comparing packages: {ex.Message}");
            return 1;
        }
        finally
        {
            CleanupTemp(temp1);
            CleanupTemp(temp2);
        }
    }

    private static int EmitResult(
        string file1, string file2,
        List<DllDiffResult> dllResults,
        List<string> onlyIn1, List<string> onlyIn2,
        int identicalCount, int differentCount, int errorCount,
        OutputMode outputMode)
    {
        if (outputMode == OutputMode.Json)
        {
            var compareResult = new
            {
                file1,
                file2,
                perDll = dllResults.Select(d => new
                {
                    dllPath = d.DllPath,
                    areEqual = d.Result?.AreEqual,
                    events = d.Result?.Events.Select(e => new
                    {
                        type = e.GetType().Name,
                        message = e.FormatMessage(),
                    }),
                    typesAdded = d.Result?.TypesOnlyInAssembly2.ToList(),
                    typesRemoved = d.Result?.TypesOnlyInAssembly1.ToList(),
                    methodBodyChanges = d.Result?.MethodBodyChanges.Select(m => new
                    {
                        typeName = m.TypeName,
                        signature = m.Signature,
                        instructionDiffs = m.Changes.Select(c => new
                        {
                            c.Index, c.Instruction1, c.Instruction2,
                        }),
                        instructions1 = m.Instructions1,
                        instructions2 = m.Instructions2,
                    }),
                    error = d.Error,
                }),
                onlyInFile1 = onlyIn1,
                onlyInFile2 = onlyIn2,
                summary = new { identical = identicalCount, different = differentCount, errors = errorCount },
            };
            Console.WriteLine(JsonSerializer.Serialize(compareResult, Json.Options));
            return errorCount > 0 ? 1 : 0;
        }

        DiffRenderer.Render(
            $"{Path.GetFileName(file1)} vs {Path.GetFileName(file2)}",
            dllResults,
            onlyIn1,
            onlyIn2,
            Path.GetFileName(file1),
            Path.GetFileName(file2));

        return errorCount > 0 ? 1 : 0;
    }

    private static void CleanupTemp(string? path)
    {
        if (path != null && Directory.Exists(path))
        {
            try { Directory.Delete(path, recursive: true); }
            catch { /* ignore cleanup errors */ }
        }
    }
}
