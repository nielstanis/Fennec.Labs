using FennecLabs.AssemblyDiff;
using FennecLabs.Cli.Rendering;
using FennecLabs.NuGet;
using Mono.Cecil;
using Spectre.Console;

namespace FennecLabs.Cli.Commands;

internal static class DllPipeline
{
    internal static bool IsLibraryDll(PackageFileInfo f) =>
        f.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) && !f.Path.Contains("_._");

    internal static DllDiffResult CompareDllFiles(string file1, string file2)
    {
        using var assembly1 = AssemblyDefinition.ReadAssembly(file1);
        using var assembly2 = AssemblyDefinition.ReadAssembly(file2);
        var result = new AssemblyComparer(assembly1, assembly2).Compare();
        return new DllDiffResult(Path.GetFileName(file1), result, null);
    }

    internal static (List<DllDiffResult> results, int identical, int different, int errors)
        CompareMatchedDlls(
            IEnumerable<string> matchingKeys,
            IReadOnlyDictionary<string, PackageFileInfo> dlls1,
            IReadOnlyDictionary<string, PackageFileInfo> dlls2)
    {
        var results = new List<DllDiffResult>();
        int identical = 0, different = 0, errors = 0;

        foreach (var key in matchingKeys)
        {
            try
            {
                using var a1 = AssemblyDefinition.ReadAssembly(dlls1[key].FullPath);
                using var a2 = AssemblyDefinition.ReadAssembly(dlls2[key].FullPath);
                var result = new AssemblyComparer(a1, a2).Compare();
                results.Add(new DllDiffResult(key, result, null));
                if (result.AreEqual) identical++;
                else different++;
            }
            catch (Exception ex)
            {
                results.Add(new DllDiffResult(key, null, ex.Message));
                errors++;
            }
        }

        return (results, identical, different, errors);
    }

    internal static object FormatDllResult(DllDiffResult d) => new
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
    };

    internal const int ExitReproducible = 0;
    internal const int ExitError = 1;
    internal const int ExitNotReproducible = 2;

    /// <summary>
    /// Verdict exit code for <c>reproduce</c>: 1 on errors or when nothing could be compared,
    /// 2 when any DLL differs or exists on one side only, otherwise 0.
    /// </summary>
    internal static int ReproduceExitCode(
        int identical, int different, int errors, int onlyInLocal, int onlyInFeed)
    {
        if (errors > 0 || identical + different == 0)
            return ExitError;
        if (different > 0 || onlyInLocal > 0 || onlyInFeed > 0)
            return ExitNotReproducible;
        return ExitReproducible;
    }

    /// <summary>Recomputes the <c>reproduce</c> exit code from a cached result.json.</summary>
    internal static int ReproduceExitCodeFromJson(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var summary = root.GetProperty("summary");
            return ReproduceExitCode(
                summary.GetProperty("identical").GetInt32(),
                summary.GetProperty("different").GetInt32(),
                summary.GetProperty("errors").GetInt32(),
                root.GetProperty("onlyInLocal").GetArrayLength(),
                root.GetProperty("onlyInFeed").GetArrayLength());
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException
                                       or InvalidOperationException)
        {
            return ExitError;
        }
    }

    /// <summary>Exit code for a cached <c>compare</c> result: 1 when the cached summary has errors.</summary>
    internal static int CompareExitCodeFromJson(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("summary").GetProperty("errors").GetInt32() > 0 ? 1 : 0;
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException
                                       or InvalidOperationException)
        {
            return 1;
        }
    }

    internal static void RenderCachedResult(string json, string cachePath)
    {
        AnsiConsole.MarkupLine($"[dim](cached)[/] {Markup.Escape(cachePath)}");
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("summary", out var summary))
            {
                var identical = summary.GetProperty("identical").GetInt32();
                var different = summary.GetProperty("different").GetInt32();
                var errors = summary.GetProperty("errors").GetInt32();
                AnsiConsole.MarkupLine(
                    $"[dim]Summary: [green]{identical} identical[/] · " +
                    $"[red]{different} different[/] · [red]{errors} error(s)[/][/]");
            }
        }
        catch (System.Text.Json.JsonException) { }
        AnsiConsole.MarkupLine("[dim]Use --no-cache to force a fresh run.[/]");
    }
}
