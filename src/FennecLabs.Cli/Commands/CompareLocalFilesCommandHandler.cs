using System.Text.Json;
using FennecLabs.Cli.Rendering;

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
            var dllResult = DllPipeline.CompareDllFiles(file1, file2);
            var dllResults = new List<DllDiffResult> { dllResult };
            return EmitResult(
                file1, file2, dllResults, [], [],
                dllResult.Result!.AreEqual ? 1 : 0, dllResult.Result.AreEqual ? 0 : 1, 0,
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

            await StatusRunner.RunAsync(outputMode, $"Extracting {Path.GetFileName(file1)}…",
                () => NupkgHelper.ExtractAsync(file1, temp1));
            await StatusRunner.RunAsync(outputMode, $"Extracting {Path.GetFileName(file2)}…",
                () => NupkgHelper.ExtractAsync(file2, temp2));

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

            var (dllResults, identicalCount, differentCount, errorCount) =
                DllPipeline.CompareMatchedDlls(matchingDlls, dlls1, dlls2);

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
                perDll = dllResults.Select(DllPipeline.FormatDllResult),
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
