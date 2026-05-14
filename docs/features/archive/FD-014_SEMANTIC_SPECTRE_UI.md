# FD-014: Semantic Spectre.Console UI — Scorecard & Compare

**Status:** Complete
**Completed:** 2026-05-14
**Priority:** High
**Effort:** Medium (2–4 hours)
**Impact:** Replaces undifferentiated `Console.WriteLine` output with color-coded, structured
Spectre.Console rendering where color carries meaning — score thresholds, diff semantics —
rather than decoration.

## Problem

Every command emits plain text with no visual hierarchy. Identical formatting is used for
progress chatter, data, and errors. The scorecard output does not communicate risk at a glance.
The compare/reproduce output lists differences as prose with no visual distinction between
identical and changed DLLs.

## Solution

### Color semantics

Define one consistent color contract used across all commands:

| Signal | Color | When |
|--------|-------|------|
| Good / identical | `green` | Score ≥ 7, DLL identical |
| Warning | `yellow` | Score 4–6.9, DLL has minor differences |
| Bad / different | `red` | Score < 4, DLL has differences, error |
| Muted / progress | `grey` | Download messages, informational chatter |
| Emphasis | `bold` | Section headers, package names, version numbers |

Progress messages (downloads, extraction) move to stderr and render as muted grey — they are
not data. All data renders to stdout in structured form.

### Scorecard command

Replace prose output with a Spectre.Console `Table`:

```
Package                       Version   Score   Checks
─────────────────────────────────────────────────────
Polly                         8.4.1     [green]8.4[/]    ✓ 9 / 10
Newtonsoft.Json               13.0.3    [yellow]5.1[/]    ✗ Code-Review: N/A
AWSSDK.Core                   3.7.400   [red]2.0[/]    ✗ SAST, Pinned-Dependencies, …
```

- Score column colored by threshold: green ≥ 7, yellow 4–6.9, red < 4
- Failing checks listed inline (truncated to 3, `+N more` if longer)
- Error column shows fetch errors in red italic
- Summary line at bottom: `N packages · avg score X.X · Y at risk`

### Compare / Reproduce commands

Replace the per-DLL prose block with structured Spectre.Console output:

```
Comparing Polly 8.4.1 → 8.3.0

  [green]✓[/] Polly.dll                  identical
  [red]✗[/] Polly.Core.dll              3 differences
      [red]−[/] Type removed:  PollyObservabilityOptions.MaxRetries
      [green]+[/] Type added:   ResilienceStrategy<T>
      [yellow]~[/] Method changed: Pipeline.Execute(…)
  [red]✗[/] Polly.Extensions.dll        error: Cecil load failed

Summary: 1 identical · 2 different · 1 error
```

- DLL line: green ✓ for identical, red ✗ for differences/errors
- Difference lines: red `−` removed, green `+` added, yellow `~` changed
- Error line shown in red italic with the exception message

### Download progress (both commands)

Replace `Console.WriteLine("Downloading…")` with an `AnsiConsole.Status()` spinner:

```csharp
await AnsiConsole.Status()
    .StartAsync($"Downloading {packageId} {version}…", async ctx =>
    {
        ctx.Spinner(Spinner.Known.Dots);
        packagePath = await nugetService.DownloadPackageAsync(…);
    });
```

Spinner output goes to stderr so it does not pollute stdout JSON in `--format json` mode
(Spectre.Console renders to `AnsiConsole.Error` when stderr is configured as the target).

### TTY guard

All Spectre.Console rendering is gated on `AnsiConsole.Profile.Capabilities.Ansi`. When
stdout is piped/redirected (CI, scripts), fall back to plain `Console.WriteLine` — no ANSI
escape codes leak into piped output.

## Dependency

Requires FD-006 (Spectre.Console package reference in `FennecLabs.Cli`).

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Rendering/ColorTheme.cs` | CREATE | Static constants for the color contract above |
| `src/FennecLabs.Cli/Rendering/ScorecardRenderer.cs` | CREATE | Renders `IEnumerable<ScorecardResult>` as Spectre Table |
| `src/FennecLabs.Cli/Rendering/DiffRenderer.cs` | CREATE | Renders compare/reproduce result as colored DLL list |
| `src/FennecLabs.Cli/Commands/ScorecardCommandHandler.cs` | MODIFY | Replace Console.WriteLine with ScorecardRenderer |
| `src/FennecLabs.Cli/Commands/CompareCommandHandler.cs` | MODIFY | Replace Console.WriteLine with DiffRenderer + Status spinner |
| `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` | MODIFY | Replace Console.WriteLine with DiffRenderer + Status spinner |

## Verification

1. `dotnet build` — 0 errors, 0 warnings.
2. `fennec scorecard -p <project>` in a TTY — scores colored by threshold, table layout.
3. `fennec scorecard -p <project> | cat` — plain text, no ANSI escape codes.
4. `fennec compare --nuget Polly` — identical DLLs green, different DLLs red, diff lines `+`/`−`/`~`.
5. Download operations show a spinner during the wait, not inline text.
6. Error paths (bad package, fetch failure) display in red, not the same style as normal output.

## Related

- FD-006 — Spectre.Console dependency this builds on
- FD-015 — JSON output mode; renderers must be bypassed when `--format json` is active
- FD-013 — MCP server; the library-level result types this renders are the same ones MCP returns
