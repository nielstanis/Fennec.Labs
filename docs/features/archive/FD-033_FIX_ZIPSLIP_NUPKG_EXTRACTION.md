# FD-033: Fix Zip-Slip Path Traversal in NupkgHelper.ExtractAsync

**Status:** Complete
**Completed:** 2026-05-31
**Priority:** High
**Effort:** Low (< 1 hour)
**Impact:** Prevents a malicious `.nupkg` file from writing arbitrary files outside the
extraction directory when using `compare --file` or `reproduce --filename`.

## Problem

`NupkgHelper.ExtractAsync` (`src/FennecLabs.Cli/NupkgHelper.cs:18`) builds the output
path by joining the caller-supplied `extractPath` with the archive entry's `FullName`
without verifying that the resolved path stays under `extractPath`:

```csharp
var entryPath = Path.Combine(extractPath, entry.FullName);   // ← no containment check
var entryDir  = Path.GetDirectoryName(entryPath);
Directory.CreateDirectory(entryDir);
using var outStream = File.Create(entryPath);                 // ← arbitrary write
```

A `.nupkg` file (which is a ZIP archive) with an entry named `../../some/path/evil.dll`
resolves to a path outside the extraction root. Because `File.Create` creates or
**overwrites** the target, this can silently overwrite any file the process has write
access to.

### Attack surface

Both call sites accept user-supplied paths:

| Call site | User input |
|-----------|-----------|
| `ReproduceCommandHandler.ExecuteFileAsync` (`ReproduceCommandHandler.cs:76,81`) | `--filename` / `-f` option |
| `CompareLocalFilesCommandHandler.CompareDlls` (`CompareLocalFilesCommandHandler.cs:81-89`) | `--file` / `-f` option (two paths) |

A developer running `fennec reproduce --filename attacker.nupkg --nuget SomePackage` or
`fennec compare --file attacker.nupkg legitimate.nupkg` against an untrusted package
would trigger the vulnerability.

The extraction target (`Path.GetTempPath() + Guid`) is in `%TEMP%` / `/tmp`, so relative
traversal can reach user home directories, `~/.config`, NuGet caches, SSH keys, etc.,
depending on depth.

## Solution

Validate each archive entry before writing. Reject entries whose resolved full path does
not begin with the canonical extraction root:

```csharp
internal static async Task ExtractAsync(string nupkgPath, string extractPath)
{
    var canonicalRoot = Path.GetFullPath(extractPath) + Path.DirectorySeparatorChar;

    using var fileStream = File.OpenRead(nupkgPath);
    using var archive   = new ZipArchive(fileStream, ZipArchiveMode.Read);

    foreach (var entry in archive.Entries)
    {
        if (string.IsNullOrEmpty(entry.Name))
            continue;

        var entryPath = Path.GetFullPath(Path.Combine(extractPath, entry.FullName));
        if (!entryPath.StartsWith(canonicalRoot, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Archive entry '{entry.FullName}' resolves outside extraction root.");

        var entryDir = Path.GetDirectoryName(entryPath)!;
        Directory.CreateDirectory(entryDir);

        using var entryStream = entry.Open();
        using var outStream   = File.Create(entryPath);
        await entryStream.CopyToAsync(outStream);
    }
}
```

Key points:
- `Path.GetFullPath` resolves `..` components and normalises separators before the check.
- The trailing `Path.DirectorySeparatorChar` on `canonicalRoot` prevents a root like
  `/tmp/abc` from accidentally allowing `/tmp/abcdef/evil` (prefix match without separator
  would pass).
- Throw `InvalidOperationException` rather than silently skipping, so callers know the
  archive is malformed/malicious and can surface an error instead of producing a silent
  partial result.

No changes needed at call sites — both callers already wrap extraction in `try/catch`
that turns exceptions into `Console.Error.WriteLine` + `return 1`.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/NupkgHelper.cs` | MODIFY | Add path-containment check before `File.Create` |

## Verification

1. `dotnet build` — 0 errors, 0 warnings
2. `dotnet test` — existing tests pass (normal extraction path unchanged)
3. Write a unit test in `FennecLabs.Cli.Tests`:
   - Create a `ZipArchive` in memory with an entry named `../../evil.txt`
   - Write it to a temp `.nupkg` file
   - Assert `ExtractAsync` throws `InvalidOperationException` containing `"resolves outside extraction root"`
4. Assert the normal case: a valid `.nupkg` with `lib/net8.0/Foo.dll` extracts correctly
5. Verify the evil file was **not** written to the filesystem after the throw

## Related

- `src/FennecLabs.Cli/NupkgHelper.cs:18` — vulnerable line
- `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs:76,81` — call site 1
- `src/FennecLabs.Cli/Commands/CompareLocalFilesCommandHandler.cs:81-89` — call site 2
- `docs/optimizations.md` — identified in 2026-05-31 re-audit as High priority new issue
