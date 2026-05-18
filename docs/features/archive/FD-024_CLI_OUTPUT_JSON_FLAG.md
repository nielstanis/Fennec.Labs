# FD-024: Replace --format with --json / -j Flag

**Status:** Complete
**Completed:** 2026-05-17
**Priority:** Medium
**Effort:** Low (< 1 hour)
**Impact:** Simpler, more ergonomic way to request JSON output; removes stringly-typed `--format` option

## Problem

All commands that produce output currently accept `--format human|json` (a global string option) to
switch between human-readable and JSON output. This requires typing `--format json` for a very
common, binary choice. A boolean flag is idiomatic for this pattern and easier to use in scripts.

Current interface:

```
fennec scorecard --format json
fennec compare --format json
```

Desired interface:

```
fennec scorecard --json
fennec scorecard -j
fennec compare -j
```

## Solution

Replace `--format` (`Option<string>`) with `--json` / `-j` (`Option<bool>`) as a global option on
the root command. Update `ResolveOutputMode` to accept `bool` instead of `string?`. Remove the
`--format` option entirely — no backward-compatibility shim.

### Changes

1. **`Program.cs`** — swap `globalFormatOption` from `Option<string>("--format")` to
   `Option<bool>("--json", "-j")` with description `"Write output as JSON"`. Update all
   `parseResult.GetValue(globalFormatOption)` call sites to pass `bool`.
2. **`Program.cs`** — update `ResolveOutputMode(string?)` → `ResolveOutputMode(bool)`:
   returns `OutputMode.Json` when `true`, `OutputMode.Human` otherwise.
3. **`--format` option removed** — no `DefaultValueFactory` needed; `bool` defaults to `false`.

No changes needed in command handlers — they consume `OutputMode` which is unchanged.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Replace `--format` option with `--json`/`-j`; update `ResolveOutputMode` signature |

## Verification

1. `fennec scorecard` → human-readable table output (default)
2. `fennec scorecard --json` → JSON to stdout
3. `fennec scorecard -j` → same JSON output
4. `fennec compare -j` → JSON diff output
5. `fennec --format json` → unknown option error (old flag removed)
6. All existing CLI tests pass

## Related

- [FD-015](archive/FD-015_GLOBAL_JSON_OUTPUT.md) — original `--format human|json` implementation being replaced
