# FD-030: Show Full Help When Required Arguments Are Missing

**Status:** Complete
**Completed:** 2026-05-28
**Priority:** Medium
**Effort:** Low (< 1 hour)
**Impact:** Users who invoke a command without required arguments see the full usage text,
not just a bare error string — no need to re-run with `--help`.

## Problem

When required arguments are omitted, commands print a one-line error to stderr and exit 1.
No usage, options list, or examples are shown. The user must re-run with `--help` to learn
what the command needs.

Affected invocations:

| Invocation | Current output |
|------------|----------------|
| `fennec instrument` | `Either --filename or --nuget must be specified.` |
| `fennec compare` | `Either --nuget or --file is required.` |
| `fennec reproduce` | `--filename is required.` / `--nuget is required.` (two separate failures, no help) |
| `fennec feeds add` | `--name is required.` / `--source is required.` |
| `fennec feeds remove` | `--name is required.` |

Positive cases that already work correctly and must not regress:
- `fennec` alone → shows root help (System.CommandLine default, no action set)
- `fennec feeds` alone → shows feeds subcommand help (no action set)
- `fennec scorecard` alone → valid; falls back to scanning the current directory

## Solution

Two strategies, matched to the type of constraint:

### Strategy A — `Required = true` (individually required options)

`reproduce --filename` and `reproduce --nuget` are unconditionally required. Same for
`feeds add --name`, `feeds add --source`, and `feeds remove --name`. Mark these with
`Required = true` on the `Option<T>` object in `Program.cs`:

```csharp
var reproduceFilenameOption = new Option<string>("--filename", "-f")
{
    Description = "Path to the .nupkg file to compare",
    Required = true,
};
```

System.CommandLine 2.x validates required options before the action runs and automatically
shows the error alongside the command help. No handler changes needed.

Remove the manual null checks in `ReproduceCommandHandler.ExecuteAsync` and
`FeedsCommandHandler.ExecuteAddAsync` / `ExecuteRemoveAsync` that are superseded.

### Strategy B — Help-after-error (mutually exclusive required options)

`instrument` requires `--filename` OR `--nuget` (not both, not neither).
`compare` requires `--nuget` OR `--file` (not both, not neither).
These constraints cannot be expressed as `Required = true` on a single option.

Keep the existing validation logic in the handlers (or in the action lambda), but after
writing the error to `Console.Error`, also print help:

```csharp
if (string.IsNullOrWhiteSpace(filename) && string.IsNullOrWhiteSpace(nuget))
{
    Console.Error.WriteLine("Either --filename or --nuget must be specified.");
    // Re-parse with --help to emit help text to stdout.
    await parseResult.CommandResult.Command
        .Parse(["--help"])
        .InvokeAsync();
    return 1;
}
```

If the System.CommandLine 2.0.7 API does not support `Parse` directly on a `Command`,
use the root parser approach:

```csharp
await rootCommand.Parse([commandName, "--help"]).InvokeAsync();
```

The exact API call should be verified during implementation. The intent: one clean
invocation that writes the standard help block (description, usage line, options table)
to stdout, immediately after the error on stderr.

### Consistent error message style

Current error messages are inconsistent:
- `"Either --filename or --nuget must be specified."` (instrument)
- `"Either --nuget or --file is required."` (compare)
- `"--filename is required."` (reproduce)

Normalize to the `"Either X or Y is required."` form for mutual exclusion cases and
`"--option is required."` for individually required options (the latter is handled by
System.CommandLine automatically via Strategy A).

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Add `Required = true` to single-required options; add help invocation after mutual-exclusion errors |
| `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` | MODIFY | Remove null checks superseded by `Required = true` |
| `src/FennecLabs.Cli/Commands/FeedsCommandHandler.cs` | MODIFY | Remove null checks for `--name` / `--source` superseded by `Required = true` |

## Verification

1. `fennec instrument` → stderr shows error; stdout shows `instrument` help (options table visible); exit code 1
2. `fennec instrument --nuget Polly` → succeeds normally (no regression)
3. `fennec compare` → stderr shows "Either --nuget or --file is required."; stdout shows `compare` help; exit code 1
4. `fennec compare --nuget Polly` → succeeds normally
5. `fennec reproduce` → stderr shows error; stdout shows `reproduce` help; exit code 1
6. `fennec reproduce --filename foo.nupkg --nuget Polly` → succeeds normally
7. `fennec feeds add` → stderr shows error for missing `--name`; stdout shows `feeds add` help; exit code 1
8. `fennec feeds add --name local --source https://example.com` → succeeds normally
9. `fennec feeds remove` → stderr shows error; stdout shows `feeds remove` help; exit code 1
10. `fennec` → root help shown (no regression)
11. `fennec feeds` → feeds subcommand help shown (no regression)
12. `dotnet build` → 0 warnings, 0 errors

## Related

- `src/FennecLabs.Cli/Program.cs` — all command/option definitions
- `src/FennecLabs.Cli/Commands/` — handlers with manual null checks to remove
- System.CommandLine 2.0.7 docs — `Option.Required`, `ParseResult.InvokeAsync`
