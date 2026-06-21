# FD-029: Surface dotnet list package stderr errors in scorecard command

**Status:** Complete
**Completed:** 2026-06-21
**Priority:** Medium
**Effort:** Low (< 1 hour)
**Impact:** Users see the actual `dotnet` error (e.g. "project.assets.json out of sync") instead of a
misleading "No packages found in the project."

## Problem

When `dotnet list package` fails (non-zero exit code), it writes a diagnostic message to stderr — for
example:

```
Unable to read a package reference from the project `/path/to/project.csproj`.
Please make sure that your project file and project.assets.json file are in sync by running restore.
```

`DeserializePackageList` (`DotnetCliResultExtensions.cs`) silently discards `StandardError` and
returns `null` on any non-zero exit code. `ScorecardCommandHandler` treats `null` as "no packages"
and calls `EmitEmpty`, printing "No packages found in the project." — hiding the real cause entirely.

This makes the `scorecard` command appear to succeed with empty results when it has actually failed.

## Solution

1. **`DotnetCliExecutor.GetPackageListAsync`** (both overloads) — after calling `ExecuteAsync`,
   check `ExitCode != 0` with a non-empty `StandardError` and throw `InvalidOperationException` with
   the trimmed stderr text. When stderr is empty and exit code is non-zero, fall through to the
   existing `DeserializePackageList` null return (preserves current behaviour for edge cases).

2. **`ScorecardCommandHandler.ResolvePackagesAsync`** — this private helper now owns both
   `GetPackageListAsync` calls and signals "no packages" by returning `null` (after calling
   `EmitEmpty`). To distinguish a real failure from an empty result, wrap the
   `GetPackageListAsync` calls in a `try/catch InvalidOperationException` here and surface the
   error: print it in Human mode via `AnsiConsole.MarkupLine`, write to `Console.Error` in JSON
   mode. Because `ResolvePackagesAsync` returns a nullable tuple and `ExecuteAsync` maps `null`
   to `return 0`, the error path needs its own exit code — either change `ResolvePackagesAsync`
   to throw/propagate so `ExecuteAsync` can `return 1`, or have `ExecuteAsync` track the failure
   distinctly from the empty case. `ExecuteAsync` currently returns `0` on every path, so the
   non-zero exit code must be threaded through deliberately.

`DeserializePackageList` itself is **not changed** — it has a test asserting null-on-failure
(`DeserializePackageList_WithNonZeroExitCode_ReturnsNull`) that must keep passing.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.DotNetCli/DotnetCliExecutor.cs` | MODIFY | Throw `InvalidOperationException(stderr)` when exit code ≠ 0 and stderr is non-empty |
| `src/FennecLabs.Cli/Commands/ScorecardCommandHandler.cs` | MODIFY | Catch the exception in `ResolvePackagesAsync`, surface the error message, and thread exit code 1 through `ExecuteAsync` |
| `test/FennecLabs.DotNetCli.Tests/DotnetCliExecutorTests.cs` | MODIFY | Add test: `GetPackageListAsync_WhenDotnetReturnsNonZeroWithStderr_ThrowsInvalidOperationException` |

## Verification

1. Create a `.csproj` that has not been restored (or references a non-existent package version).
2. Run `fennec scorecard --project path/to/project.csproj`.
3. **Before fix:** output is "No packages found in the project." with exit code 0.
4. **After fix:** output shows the actual `dotnet` error message and exits with code 1.
5. Confirm `DeserializePackageList_WithNonZeroExitCode_ReturnsNull` still passes.

## Related

- `src/FennecLabs.DotNetCli/DotnetCliResultExtensions.cs` — `DeserializePackageList` (unchanged)
- `src/FennecLabs.DotNetCli/DotnetCliResult.cs` — `StandardError` field already captured, just not used
