using FennecLabs.AssemblyDiff;
using Spectre.Console;

namespace FennecLabs.Cli.Rendering;

internal sealed record DllDiffResult(
    string DllPath,
    AssemblyComparisonResult? Result,
    string? Error);

internal static class DiffRenderer
{
    internal static void Render(
        string header,
        IReadOnlyList<DllDiffResult> dlls,
        IReadOnlyList<string>? onlyInLeft = null,
        IReadOnlyList<string>? onlyInRight = null,
        string leftLabel = "previous",
        string rightLabel = "current")
    {
        AnsiConsole.MarkupLine($"[bold]{Markup.Escape(header)}[/]");
        AnsiConsole.WriteLine();

        foreach (var dll in dlls)
        {
            if (dll.Error != null)
            {
                AnsiConsole.MarkupLine($"  [red]✗[/] {Markup.Escape(dll.DllPath)}");
                AnsiConsole.MarkupLine($"      [red dim italic]{Markup.Escape(dll.Error)}[/]");
            }
            else if (dll.Result!.AreEqual)
            {
                AnsiConsole.MarkupLine(
                    $"  [green]✓[/] {Markup.Escape(dll.DllPath)}  [green dim]identical[/]");
            }
            else
            {
                var r = dll.Result;
                AnsiConsole.MarkupLine(
                    $"  [red]✗[/] {Markup.Escape(dll.DllPath)}  " +
                    $"[red dim]{r.Differences.Count} difference(s)[/]");

                foreach (var t in r.TypesOnlyInAssembly1.Take(5))
                    AnsiConsole.MarkupLine($"      [red]−[/] [dim]removed:[/]  {Markup.Escape(t)}");
                foreach (var t in r.TypesOnlyInAssembly2.Take(5))
                    AnsiConsole.MarkupLine($"      [green]+[/] [dim]added:[/]    {Markup.Escape(t)}");
                foreach (var m in r.MethodBodyDifferences.Take(3))
                    AnsiConsole.MarkupLine(
                        $"      [yellow]~[/] [dim]changed:[/]  " +
                        $"{Markup.Escape(m.TypeName)}.{Markup.Escape(m.MethodSignature)}");
            }
        }

        if (onlyInLeft?.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Only in {leftLabel}:[/]");
            foreach (var p in onlyInLeft)
                AnsiConsole.MarkupLine($"  [yellow dim]{Markup.Escape(p)}[/]");
        }

        if (onlyInRight?.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Only in {rightLabel}:[/]");
            foreach (var p in onlyInRight)
                AnsiConsole.MarkupLine($"  [yellow dim]{Markup.Escape(p)}[/]");
        }

        var identical = dlls.Count(d => d.Result?.AreEqual == true);
        var different = dlls.Count(d => d.Result?.AreEqual == false);
        var errors = dlls.Count(d => d.Error != null);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"[dim]Summary: [green]{identical} identical[/] · " +
            $"[red]{different} different[/] · [red]{errors} error(s)[/][/]");
    }
}
