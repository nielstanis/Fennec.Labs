# FD-034: Smart TFM Resolution — Interactive Selection and Strict No-TFM Error

**Status:** In Progress
**Priority:** Medium
**Effort:** Low (< 1 hour)
**Impact:** Removes friction when pointing at a multi-TFM build output dir; eliminates silent
imprecise matching when the TFM cannot be determined at all.

## Problem

`ResolveTfmDirectory` (FD-032) has two edge-case behaviors that need refinement:

**Case A — Multiple TFM subdirectories, no `--tfm`:**
Currently returns an error: _"Multiple target frameworks found: X, Y. Use --tfm to select one."_
This is unnecessarily strict: the tool already has enough information (the list of TFMs) to let
the user choose interactively. Requiring a re-run with `--tfm` is extra friction for a common
scenario (`bin/Release/` containing `net6.0/`, `net8.0/`).

**Case B — No TFM identifiable, no `--tfm`:**
Currently falls back to filename-only DLL matching with a yellow warning. This produces
imprecise, potentially misleading results silently. If the tool cannot determine a TFM at all,
it should say so explicitly and fail rather than guess.

## Solution

### Case A — Multiple TFM subdirs, `--tfm` absent

**Human mode + interactive terminal** (`AnsiConsole.Profile.Capabilities.Interactive`):
Present a `SelectionPrompt<string>` listing the discovered TFM names, sorted ascending.
The first entry (lowest version) is highlighted by default. The user's selection becomes
`resolvedTfm`; the tool descends into that subdirectory and continues normally.

```
? Select a target framework:
  net6.0
> net8.0
```

**Human mode + non-interactive terminal** (piped output, CI, `--no-color`, etc.):
Write to stderr and return 1:
```
Multiple target frameworks found: net6.0, net8.0. Use --tfm to select one.
```
Same message as today, but this code path is now only reached in non-interactive mode.

**JSON mode** (`--json`):
Write to stderr and return 1 with the same message as the non-interactive human case.
Do not emit partial JSON.

### Case B — No TFM identifiable, `--tfm` absent

Remove the current warn-and-fallback path entirely. Replace with a hard error (stderr, return 1):
```
Cannot determine target framework from directory '<dir>'. Use --tfm (e.g. --tfm net8.0) to specify one.
```

This applies whether the output mode is Human or JSON.

### Unchanged cases

| Case | Input | Behavior |
|------|-------|----------|
| 1 | `--tfm` provided | Use as-is |
| 2 | Directory name matches TFM pattern | Auto-derive |
| 3 | Exactly one TFM subdir | Auto-select and descend |

### `resolvedTfm` in JSON output

Add `"resolvedTfm": "<tfm or null>"` to the directory-mode JSON result so callers (CI, agents)
can see which TFM was used without parsing the `localSource` path.

### Edge cases to address

| Scenario | Behavior |
|----------|----------|
| Directory has both flat DLLs and TFM subdirs | TFM subdirs take priority (unchanged from FD-032) |
| `--tfm` provided with `--filename` | Error + help (unchanged from FD-032) |
| Single subdir whose name does not match TFM pattern | Falls into Case B (no TFM identifiable) — hard error |
| All TFM subdirs are empty (no DLLs inside) | Resolution succeeds; "No matching DLL files found" error follows from existing logic |

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` | MODIFY | Change `ResolveTfmDirectory` to accept `OutputMode` and `bool isInteractive`; replace Case B warn+fallback with hard error; replace Case A immediate error with interactive `SelectionPrompt` (human+interactive) or same error (non-interactive/json) |
| `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` | MODIFY | Add `resolvedTfm` field to directory-mode JSON output |
| `src/FennecLabs.Cli.Tests/Commands/ReproduceCommandHandlerTests.cs` | MODIFY / CREATE | Tests for Case A non-interactive error, Case B hard error, `resolvedTfm` in JSON output |

### `ResolveTfmDirectory` signature change

The helper needs context to decide between interactive prompt and error. Pass it through:

```csharp
private static async Task<(string resolvedDir, string? resolvedTfm, string? error)>
    ResolveTfmDirectoryAsync(
        string directoryPath, string? tfmHint,
        OutputMode outputMode, bool isInteractive)
```

`isInteractive` is derived from `AnsiConsole.Profile.Capabilities.Interactive` in
`ExecuteDirectoryAsync` before calling the helper.

The method becomes `async` only if the prompt is shown; use `ValueTask` or keep it sync with
a `Prompt` call guarded behind the interactive check if Spectre's prompt is synchronous (it is).
Since `SelectionPrompt` is synchronous (`AnsiConsole.Prompt`), the method can stay sync — rename
to reflect the mode param:

```csharp
private static (string resolvedDir, string? resolvedTfm, string? error)
    ResolveTfmDirectory(
        string directoryPath, string? tfmHint,
        OutputMode outputMode, bool isInteractive)
```

## Verification

1. `fennec reproduce -d ./bin/Release -n MyPkg` with `net6.0/` and `net8.0/` subdirs, interactive
   terminal → selection prompt appears; selecting `net8.0` runs comparison against `lib/net8.0/` feed DLLs;
   JSON output contains `"resolvedTfm": "net8.0"`.
2. Same invocation piped to `| cat` (non-interactive) → stderr: _"Multiple target frameworks found:
   net6.0, net8.0. Use --tfm to select one."_, exit 1.
3. Same invocation with `--json` → same stderr error, no JSON emitted, exit 1.
4. `fennec reproduce -d ./build/output -n MyPkg` with flat DLLs, no TFM subdir, no `--tfm` → stderr:
   _"Cannot determine target framework from directory './build/output'. Use --tfm (e.g. --tfm net8.0) to specify one."_, exit 1.
5. `fennec reproduce -d ./build/output -t net8.0 -n MyPkg` → flat dir, TFM explicitly provided →
   comparison runs normally (existing behaviour).
6. `fennec reproduce -d ./bin/Release/net8.0 -n MyPkg` → dir name matches TFM → auto-derived, no
   prompt (existing behaviour); JSON output contains `"resolvedTfm": "net8.0"`.
7. `fennec reproduce -d ./bin/Release -n MyPkg` with single TFM subdir `net8.0/` → auto-selected,
   no prompt (existing behaviour); JSON output contains `"resolvedTfm": "net8.0"`.
8. All existing tests pass; no regressions in file mode (`--filename`).

## Related

- [FD-032](FD-032_REPRODUCE_FROM_DIRECTORY.md) (archive) — introduced `--directory`, `--tfm`, and `ResolveTfmDirectory`
- `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs:263` — `ResolveTfmDirectory` current impl
