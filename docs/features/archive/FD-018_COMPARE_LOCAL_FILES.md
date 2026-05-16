# FD-018: Compare Local Files — Accept Two .nupkg or .dll Files Directly

**Status:** Complete
**Completed:** 2026-05-16
**Priority:** Medium
**Effort:** Medium (2–4 hours)
**Impact:** Lets users diff any two assemblies or packages without publishing to NuGet first —
useful for pre-release validation, local build comparison, and CI pipelines that never push.

## Problem

`compare` today only works against NuGet: it takes a `--nuget` package ID, downloads two
versions from the feed, and diffs the matched DLLs. There is no way to compare two files
you already have on disk.

Common scenarios that are currently impossible:

- "Does this local build change any method bodies compared to the last release build I have?"
- "Is this candidate `.nupkg` binary-identical to the one we published last week?"
- CI pipeline comparing `bin/Release/MyLib.dll` before and after a patch.

`reproduce` is close but still requires one side to come from a NuGet feed.

## Solution

Add a `--file` option to the `compare` command that accepts exactly two paths (arity 2).
Mutually exclusive with `--nuget`. Each path must be either a `.dll` or a `.nupkg`.

Usage:

```
fennec compare --file a.dll b.dll
fennec compare --file a.nupkg b.nupkg --format json
```

### Command wiring (`Program.cs`)

```csharp
var compareFileOption = new Option<string[]>("--file")
{
    Description = "Two .dll or .nupkg files to compare",
    Arity = new ArgumentArity(2, 2),
    AllowMultipleArgumentsPerToken = true,
};
```

`--file` and `--nuget` are mutually exclusive — validated in the action handler before
dispatching.

### Input handling

| file[0] | file[1] | Behaviour |
|---------|---------|-----------|
| `.dll` | `.dll` | Compare the two DLLs directly (one `DllDiffResult`) |
| `.nupkg` | `.nupkg` | Extract both packages, match DLLs by path, diff matched pairs |
| `.dll` | `.nupkg` | Error: mixed types not supported |
| `.nupkg` | `.dll` | Error: mixed types not supported |

### Cache

Local-file comparisons are **not cached** (files can change between runs). Always run fresh.
JSON output uses the same schema as the NuGet path but `packageId` / `currentVersion` /
`previousVersion` are replaced with `file1` / `file2` path strings.

### Handler

Extract `CompareLocalFilesCommandHandler` rather than adding branching to the existing
`CompareCommandHandler`. The shared diffing logic (`AssemblyComparer`, `DllDiffResult`,
`DiffRenderer`) stays shared.

### nupkg extraction

`NuGetService.GetPackageContentsAsync` already reads a nupkg by path. Reuse it for the
local nupkg case. For `.dll`, use `AssemblyDefinition.ReadAssembly` directly.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Add `--file` option (arity 2); validation; dispatch to new handler |
| `src/FennecLabs.Cli/Commands/CompareLocalFilesCommandHandler.cs` | CREATE | Handle local `.dll`/`.nupkg` compare; emit same JSON schema |
| `src/FennecLabs.Cli/Commands/CompareCommandHandler.cs` | MODIFY (optional) | Extract shared helper if needed to avoid duplication |
| `test/FennecLabs.Cli.Tests/` | CREATE or MODIFY | Integration tests: dll vs dll, nupkg vs nupkg, error cases |

## Verification

1. `fennec compare --file a.dll b.dll` — human output shows diff/identical summary
2. `fennec compare --file a.dll b.dll --format json` — JSON has `file1`, `file2`, `perDll` array
3. `fennec compare --file a.nupkg b.nupkg` — matched DLLs are diffed, unmatched reported
4. `fennec compare --file a.dll b.nupkg` — error: mixed types, exit code 1
5. `fennec compare --file missing.dll b.dll` — error: file not found, exit code 1
6. `fennec compare --file a.dll b.dll --nuget Foo` — error: mutually exclusive, exit code 1
7. `fennec compare --file a.dll` (only one path) — System.CommandLine arity error
8. Existing `fennec compare --nuget Foo --version 1.0.0` still works unchanged

## Related

- FD-017: Introduced typed `DiffEvent` model used by both compare paths
- `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` — similar local-nupkg-vs-feed pattern for reference
