# FD-006: Add Spectre.Console as CLI Rendering Foundation

**Status:** Complete
**Completed:** 2026-05-14
**Priority:** Low
**Effort:** Low (< 1 hour)
**Impact:** Brings Spectre.Console in as the standard rendering layer for all future rich output
(tables, progress bars, colour-coded diffs, structured error messages).

## Problem

`FennecLabs.Cli` renders all output via bare `Console.WriteLine` calls. There is no colour,
no structured table output, and no TTY detection. Adding Spectre.Console as a foundational
dependency establishes the rendering contract for all future CLI improvements without
committing to any specific UI changes yet.

## Solution

1. **Add `Spectre.Console`** NuGet package to `FennecLabs.Cli` (verify current stable version
   on nuget.org before pinning).

2. **Wire `AnsiConsole` into `Program.cs`** as the output surface — no functional changes to
   existing commands yet, just ensure the dependency is present and `AnsiConsole` is reachable
   from all command handlers.

3. **TTY guard** — document the pattern: gate any rich output behind
   `AnsiConsole.Profile.Capabilities.Ansi` so piped/CI output degrades gracefully to plain text.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Fennec.csproj` | MODIFY | Add `<PackageReference Include="Spectre.Console" />` |

## Verification

1. `dotnet build` — 0 errors, 0 warnings.
2. `dotnet add` resolves the package; `dotnet list package` shows it.
3. No behavioural change to any existing command — this is a pure dependency addition.

## Related

- Successor FDs will use this foundation for table output, progress spinners, and coloured diffs.
