using FennecLabs.Cli.Commands;
using Spectre.Console;

namespace FennecLabs.Cli.Rendering;

internal static class ScorecardRenderer
{
    internal static void Render(IReadOnlyList<PackageScorecardResult> results)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Package[/]"))
            .AddColumn(new TableColumn("[bold]Version[/]"))
            .AddColumn(new TableColumn("[bold]Score[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Failing Checks[/]"));

        foreach (var r in results.OrderByDescending(r => r.Scorecard?.Score ?? -1m))
        {
            string scoreCell;
            string checksCell;

            if (r.Error != null)
            {
                scoreCell = "[red italic]error[/]";
                checksCell = $"[red dim]{Markup.Escape(r.Error)}[/]";
            }
            else if (r.Scorecard == null)
            {
                scoreCell = "[grey]N/A[/]";
                checksCell = "[grey dim]no scorecard[/]";
            }
            else
            {
                var color = ColorTheme.ForScore(r.Scorecard.Score);
                scoreCell = $"[{color}]{r.Scorecard.Score:F1}/10[/]";

                var allFailing = r.Scorecard.Checks
                    .Where(c => c.Score is >= 0 and < 5)
                    .OrderBy(c => c.Score)
                    .ToList();
                var shown = allFailing.Take(3)
                    .Select(c => $"[red dim]{Markup.Escape(c.Name)}[/]")
                    .ToList();
                var extra = allFailing.Count - shown.Count;
                checksCell = shown.Count == 0
                    ? "[green dim]all passing[/]"
                    : string.Join(", ", shown) + (extra > 0 ? $" [grey]+{extra} more[/]" : "");
            }

            table.AddRow(
                Markup.Escape(r.PackageId),
                Markup.Escape(r.PackageVersion),
                scoreCell,
                checksCell);
        }

        AnsiConsole.Write(table);

        var scored = results.Where(r => r.Scorecard != null).ToList();
        if (scored.Count > 0)
        {
            var avg = scored.Average(r => r.Scorecard!.Score);
            var atRisk = scored.Count(r => r.Scorecard!.Score < 7m);
            AnsiConsole.MarkupLine(
                $"[dim]{results.Count} packages · avg score {avg:F1} · {atRisk} at risk[/]");
        }
    }
}
