# FD-019: Consolidate nupkg Extraction Helpers

**Status:** Complete
**Completed:** 2026-05-17
**Priority:** Low
**Effort:** Low (< 1 hour)
**Impact:** Removes duplicated nupkg extraction and DLL-scanning logic shared by
`ReproduceCommandHandler` and `CompareLocalFilesCommandHandler`.

## Problem

Two private static helpers are duplicated across both command handlers:

| Helper | Reproduce | CompareLocalFiles |
|--------|-----------|-------------------|
| nupkg extraction | `ExtractNupkgFileAsync` | `ExtractNupkgAsync` |
| DLL directory scan | `GetPackageContentsFromDirectory` + inline filter | `GetDllsFromDirectory` |

The extraction logic is byte-for-byte identical (different local variable names only).
The directory scan is logically identical — Reproduce returns all files then filters for
`.dll` + `!_._` at the call site; CompareLocalFiles applies the filter inside the method and
returns a dictionary directly.

## Solution

Extract a `static class NupkgHelper` in `FennecLabs.Cli` with two methods:

```csharp
internal static class NupkgHelper
{
    internal static async Task ExtractAsync(string nupkgPath, string extractPath) { ... }

    internal static Dictionary<string, PackageFileInfo> GetDlls(string dir) { ... }
}
```

`GetDlls` is the CompareLocalFiles variant (returns `Dictionary<string, PackageFileInfo>`,
filters `*.dll` and `_._` inline). Update `ReproduceCommandHandler` to call `GetDlls` and
remove its separate `GetPackageContentsFromDirectory` method and the inline `.Where` filter
at the call site — they collapse into one `NupkgHelper.GetDlls` call.

Remove the private helpers from both handlers.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/NupkgHelper.cs` | CREATE | Shared `ExtractAsync` and `GetDlls` |
| `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` | MODIFY | Use `NupkgHelper`; remove private helpers |
| `src/FennecLabs.Cli/Commands/CompareLocalFilesCommandHandler.cs` | MODIFY | Use `NupkgHelper`; remove private helpers |

## Verification

1. Build solution — 0 warnings, 0 errors
2. `fennec reproduce --filename x.nupkg --nuget Foo` still works
3. `fennec compare --file a.nupkg b.nupkg` still works
4. Run all existing tests — 0 failures

## Related

- FD-018: Introduced `CompareLocalFilesCommandHandler` and the second copy of these helpers
