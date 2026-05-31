# FD-031: Every Option Gets Both -x and --word Aliases, No Clashes

**Status:** Complete
**Completed:** 2026-05-28
**Priority:** Low
**Effort:** Low (< 1 hour)
**Impact:** Every option is reachable via a predictable single-letter shorthand; the full
alias map is documented and clash-free.

## Problem

Four options currently have a `--long` form but no `-x` short alias:

| Command | Option | Missing short |
|---------|--------|---------------|
| global | `--no-cache` | — |
| `scorecard` | `--report-format` | — |
| `compare` | `--file` | — |
| `feeds add` | `--default` | — |

Additionally, `--file-format` on `instrument` uses `-F` (uppercase). This is technically
valid but inconsistent with every other alias in the CLI (all lowercase). It should be
audited for a cleaner replacement.

## Current Alias Inventory

Global options (`Recursive = true` — present on every subcommand):

| Long | Short | Notes |
|------|-------|-------|
| `--json` | `-j` | ✓ |
| `--output` | `-o` | ✓ |
| `--no-cache` | — | ✗ missing |

`instrument` subcommand (effective set includes global):

| Long | Short | Notes |
|------|-------|-------|
| `--filename` | `-f` | ✓ |
| `--nuget` | `-n` | ✓ |
| `--version` | `-v` | ✓ |
| `--file-format` | `-F` | ✓ (uppercase — inconsistent) |

`scorecard` subcommand:

| Long | Short | Notes |
|------|-------|-------|
| `--project` | `-p` | ✓ |
| `--report-format` | — | ✗ missing |

`compare` subcommand:

| Long | Short | Notes |
|------|-------|-------|
| `--nuget` | `-n` | ✓ |
| `--version` | `-v` | ✓ |
| `--file` | — | ✗ missing |

`reproduce` subcommand:

| Long | Short | Notes |
|------|-------|-------|
| `--filename` | `-f` | ✓ |
| `--nuget` | `-n` | ✓ |
| `--version` | `-v` | ✓ |

`feeds add` subcommand:

| Long | Short | Notes |
|------|-------|-------|
| `--name` | `-n` | ✓ |
| `--source` | `-s` | ✓ |
| `--default` | — | ✗ missing |

`feeds remove` subcommand:

| Long | Short | Notes |
|------|-------|-------|
| `--name` | `-n` | ✓ |

## Solution

### Proposed additions

| Command | Option | Short to add | Rationale |
|---------|--------|--------------|-----------|
| global | `--no-cache` | `-C` | Uppercase C = Cache bypass; avoids collision with any current lowercase alias |
| `scorecard` | `--report-format` | `-r` | r = report |
| `compare` | `--file` | `-f` | f = file; `-f` is unused on `compare` |
| `feeds add` | `--default` | `-d` | d = default |

### `--file-format` / `-F` on `instrument`

`-F` (uppercase) is already present and not technically a clash. Two options:
- **Keep `-F`** — already documented in README/CONTRIBUTING; low churn
- **Replace with `-e`** (format **e**ncoding) or `-F` renamed to `-t` (file **t**ype)

This FD keeps `-F` unchanged to avoid a breaking change. A separate FD can address it if
the uppercase inconsistency becomes a pain point.

### Clash verification (post-change)

Because global options are `Recursive = true`, each subcommand's effective alias set is
the union of global aliases plus its own. Verified clash-free after additions:

| Subcommand effective set | Aliases |
|--------------------------|---------|
| `instrument` | `-C -j -o` (global) + `-f -F -n -v` (local) |
| `scorecard` | `-C -j -o` + `-p -r` |
| `compare` | `-C -j -o` + `-f -n -v` |
| `reproduce` | `-C -j -o` + `-f -n -v` |
| `feeds add` | `-C -j -o` + `-d -n -s` |
| `feeds remove` | `-C -j -o` + `-n` |

No collisions within any effective set.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Add `-C`, `-r`, `-f`, `-d` to the four options listed above |

## Verification

1. `fennec --no-cache scorecard` and `fennec -C scorecard` behave identically
2. `fennec scorecard --report-format html` and `fennec scorecard -r html` behave identically
3. `fennec compare --file a.dll b.dll` and `fennec compare -f a.dll b.dll` behave identically
4. `fennec feeds add --name x --source y --default` and `fennec feeds add -n x -s y -d` behave identically
5. `fennec --help` still renders clean (no duplicate or garbled aliases)
6. Each subcommand `--help` shows the short alias next to every option
7. `dotnet build` → 0 warnings, 0 errors

## Related

- `src/FennecLabs.Cli/Program.cs` — all `Option<T>` declarations
- FD-030 — companion FD adding required-option validation (same file, coordinate changes)
