# FD-016: Structured Output Directory — Per-Command Subfolders and Result Cache

**Status:** Complete
**Completed:** 2026-05-16
**Priority:** Medium
**Effort:** Medium (2–4 hours)
**Impact:** Every command that produces output writes under its own subfolder of `.fennec/`,
making results browsable, identifiable, and reusable — running the same command twice
can return cached output instead of re-downloading or re-computing.

## Problem

Currently only `instrument` writes to the output folder (`.fennec/`). The remaining commands
(`scorecard`, `compare`, `reproduce`) either write to the current working directory (HTML report)
or produce no persistent output at all. This means:

1. **No per-command organisation** — if multiple commands write to `.fennec/`, their outputs
   collide in a flat directory with no indication of which command produced what.
2. **No reuse** — every invocation re-downloads packages and re-queries the Scorecard API,
   even when the result for those exact inputs was already computed. This is slow and burns
   API quota.
3. **Instrument subfolder is already inconsistent** — NuGet instrumentation scopes output under
   `<packageId>/<version>/`, but direct-file instrumentation writes flat into `.fennec/`. Both
   should live under `.fennec/instrument/`.

## Solution

### Output layout

All persistent output lives under the `--output` folder (default `.fennec/`):

```
.fennec/
  instrument/
    BasicConsole.fxt                          ← direct-file instrumentation
    Polly/8.6.6/
      Polly.dll.fxt
      Polly.Core.dll.fxt
  scorecard/
    PollyAwsMvcApp/2026-05-14T13-22-00/
      result.json                             ← structured JSON (FD-015 schema)
      report.html                             ← HTML report (if --report was passed)
  compare/
    Polly/8.6.6-vs-8.5.0/
      result.json
  reproduce/
    Polly/8.6.6/
      result.json
```

### Cache semantics

Before executing a command, check whether a result file already exists for that cache key:

- `instrument/<file-stem>.<ext>` (direct file) or `instrument/<id>/<version>/<dll>.<ext>` (NuGet)
- `scorecard/<project-name>/latest/result.json` — treat "latest" as the most-recent timestamped
  run; always re-run scorecard (API results change over time) and write a fresh timestamp dir
- `compare/<packageId>/<currentVersion>-vs-<previousVersion>/result.json`
- `reproduce/<packageId>/<version>/result.json`

**Cache hit behaviour (compare and reproduce only):** if `result.json` exists for the given
key, print a `[dim](cached)[/]` notice and load from disk rather than downloading. Add
`--no-cache` flag to force a fresh run.

Scorecard is intentionally not cached — scores change as projects improve. Instrument is also
not cached by default (assembly may be rebuilt), but the file-presence check already acts as a
natural signal (overwrite on every run).

### Changes to existing paths

| Command | Old output location | New output location |
|---------|-------------------|-------------------|
| `instrument` (file) | `.fennec/<name>.fxt` | `.fennec/instrument/<name>.fxt` |
| `instrument` (NuGet) | `.fennec/<id>/<version>/` | `.fennec/instrument/<id>/<version>/` |
| `scorecard` HTML | `<cwd>/scorecard-report-<ts>.html` | `.fennec/scorecard/<project>/<ts>/report.html` |
| `scorecard` JSON | stdout only | also written to `.fennec/scorecard/<project>/<ts>/result.json` |
| `compare` | stdout only | also written to `.fennec/compare/<id>/<v1>-vs-<v2>/result.json` |
| `reproduce` | stdout only | also written to `.fennec/reproduce/<id>/<version>/result.json` |

`--output` continues to control the root folder for all of these.

### `--no-cache` flag

Add `--no-cache` as a global root option (like `--format`) that bypasses any cache check and
overwrites existing results.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/OutputCache.cs` | CREATE | `OutputCache` helper: `TryLoad(path)`, `Write(path, json)`, `Exists(path)` |
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Add `--no-cache` global option; pass to all handlers |
| `src/FennecLabs.Cli/Commands/InstrumentCommandHandler.cs` | MODIFY | Change output root from `.fennec/` to `.fennec/instrument/` |
| `src/FennecLabs.Cli/Commands/ScorecardCommandHandler.cs` | MODIFY | Write result.json + report.html to `.fennec/scorecard/<project>/<ts>/` |
| `src/FennecLabs.Cli/Commands/CompareCommandHandler.cs` | MODIFY | Write result.json to `.fennec/compare/<id>/<v1>-vs-<v2>/`; cache hit skips download |
| `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` | MODIFY | Write result.json to `.fennec/reproduce/<id>/<version>/`; cache hit skips download |

## Verification

1. `dotnet build` — 0 errors, 0 warnings.
2. `fennec instrument -f BasicConsole.dll` → output at `.fennec/instrument/BasicConsole.fxt`.
3. `fennec instrument --nuget Polly` → output at `.fennec/instrument/Polly/<version>/`.
4. `fennec scorecard -p PollyAwsMvcApp.csproj` → `result.json` written under `.fennec/scorecard/PollyAwsMvcApp/<ts>/`.
5. `fennec compare --nuget Polly` → `result.json` written under `.fennec/compare/Polly/<v1>-vs-<v2>/`.
6. Run step 5 again → `(cached)` notice, no download, same result.
7. Run step 5 with `--no-cache` → fresh download, result.json overwritten.
8. `fennec reproduce ...` → `result.json` written under `.fennec/reproduce/<id>/<version>/`.
9. Existing `--output` override still scopes all output under the custom folder.

## Related

- FD-005 — established `--output` / `-o` option and `.fennec/` default
- FD-015 — JSON schemas that `result.json` files will use
- FD-013 — MCP server will benefit from reading cached result files directly
