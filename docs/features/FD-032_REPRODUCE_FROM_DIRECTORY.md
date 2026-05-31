# FD-032: Reproduce from a Directory of DLL Files

**Status:** Open
**Priority:** Medium
**Effort:** Medium (1-4 hours)
**Impact:** Lets users run the reproduce check against a build output directory instead of a
packaged `.nupkg`, so they can verify assemblies directly from their CI artifact drop or
local build folder without needing to pack first.

## Problem

`reproduce` currently requires a `.nupkg` file as input (`--filename`/`-f`). The handler
validates the `.nupkg` extension and extracts it to a temp directory before comparing.

Developers working from a build output directory (CI artifact drop, `bin/Release/net8.0/`) have
to pack first, which is extra friction and changes the artifact being tested.

There is also a path-matching subtlety: DLLs extracted from a nupkg carry nupkg-structure paths
(`lib/net8.0/Foo.dll`). A flat build directory produces bare filenames (`Foo.dll`). To match
accurately against feed DLLs (which use `lib/{tfm}/Foo.dll` keys), the TFM must be known so
the right `lib/{tfm}/` slice of the nupkg is used for comparison.

## Solution

Add two new options to the `reproduce` command:
- `--directory`/`-d` — path to a build output directory; when provided, `--filename` is ignored
- `--tfm`/`-t` — target framework moniker (e.g. `net8.0`); optional when it can be derived

### TFM resolution (directory mode only)

Resolve TFM using this precedence:

1. **`--tfm` explicitly provided** — use as-is.
2. **Directory name is a TFM** — if the last path segment matches the TFM pattern
   (`^net[\w.-]+$`, e.g. `net8.0`, `net48`, `net8.0-windows`, `netstandard2.0`), derive TFM
   from it. Typical case: user passes `--directory ./bin/Release/net8.0`.
3. **Single TFM subdirectory** — if the directory contains exactly one subdirectory
   whose name matches the TFM pattern, use it and descend into it automatically.
4. **Multiple TFM subdirectories, no TFM given** — error listing the available TFMs:
   ```
   Multiple target frameworks found: net6.0, net8.0. Use --tfm to select one.
   ```
5. **No TFMs found and no `--tfm`** — fall back to filename-only matching (no TFM filter
   applied), with a Human-mode warning that feed matching may be imprecise.

### Feed DLL matching with TFM

When the TFM is known, filter feed DLLs to `lib/{tfm}/` entries before matching:
```csharp
var feedDlls = allFeedDlls
    .Where(f => f.Path.StartsWith($"lib/{tfm}/", StringComparison.OrdinalIgnoreCase))
    .ToDictionary(f => Path.GetFileName(f.Path), f => f);
```
This replaces the filename-only scan-everything approach and eliminates the multi-TFM
duplicate-key problem entirely.

When TFM is unknown (fallback), use `ToLookup` across all feed DLLs keyed by filename
and take the first match, logging a warning in Human mode.

### Changes to `Program.cs`

```csharp
var reproduceDirOption = new Option<string>("--directory", "-d")
{
    Description = "Path to a directory of .dll files to compare"
};
var reproduceTfmOption = new Option<string>("--tfm", "-t")
{
    Description = "Target framework moniker (e.g. net8.0); derived from directory name if omitted"
};
reproduceCommand.Options.Add(reproduceDirOption);
reproduceCommand.Options.Add(reproduceTfmOption);
```

Drop `Required = true` from `reproduceFilenameOption`. Validate in the action:
- `--directory` provided → directory mode; `--filename` not read
- `--directory` not provided, `--filename` provided → file mode (existing behaviour)
- Neither provided → error + help, return 1
- `--tfm` provided without `--directory` → error + help, return 1 (meaningless for nupkg input)

Pass `directory` (or `filename` when directory is absent) and `tfm` to the handler.

### Changes to `ReproduceCommandHandler.ExecuteAsync`

New signature:
```csharp
public async Task<int> ExecuteAsync(
    string? nupkgFilePath, string? directoryPath, string? tfm,
    string packageId, string? version, OutputMode outputMode, string output, bool noCache)
```

- **File branch** (`nupkgFilePath != null`): unchanged.
- **Directory branch** (`directoryPath != null`):
  1. Resolve TFM per precedence above. If resolution fails, return 1.
  2. Descend into TFM subdir if needed.
  3. `var localDlls = NupkgHelper.GetDlls(resolvedDir)` — keys are filenames.
  4. Filter feed DLLs by `lib/{tfm}/` (or fallback to all, with warning).
  5. Match, compare, render — same structure as file branch.
  6. `localSource` in JSON output → the resolved directory path. (`localFile` renamed to
     `localSource` for both modes so the field name is accurate regardless of input type.)
  7. No temp directory created or cleaned up.
  8. Skip caching entirely — directory contents can change between runs. The `noCache`
     flag and `OutputCache` read/write are not called in this branch.

Extract TFM-resolution logic into a private static helper:
```csharp
private static (string? resolvedDir, string? tfm, string? error)
    ResolveTfmDirectory(string directoryPath, string? tfmHint)
```

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` | MODIFY | Add `directoryPath`/`tfm` params, TFM resolution helper, directory branch; rename `localFile` → `localSource` in JSON output (both branches) |
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Add `--directory`/`-d` and `--tfm`/`-t`; drop `Required` from `--filename`; validation |

## Verification

1. `dotnet build` — 0 errors, 0 warnings
2. `fennec reproduce -d ./bin/Release/net8.0 -n Fennec.Labs` — TFM derived from dir name,
   matches `lib/net8.0/` feed DLLs, shows diff
3. `fennec reproduce -d ./bin/Release -n Fennec.Labs` with a single TFM subdir — auto-selects it
4. `fennec reproduce -d ./bin/Release -n Fennec.Labs` with multiple TFM subdirs — error listing
   available TFMs
5. `fennec reproduce -d ./bin/Release -t net8.0 -n Fennec.Labs` — explicit TFM selected
6. `fennec reproduce -t net8.0 -f ./package.nupkg -n Fennec.Labs` — error "--tfm requires --directory" + help
7. `fennec reproduce -d ./bin/Release/net8.0 -f ./pkg.nupkg -n Fennec.Labs` — `--filename` ignored, directory mode runs normally
8. `fennec reproduce -f ./package.nupkg -n Fennec.Labs` — existing behaviour unchanged; JSON output has `localSource` field
9. Existing tests pass

## Related

- `FD-018` (archive) — added `--file` to `compare` for local file input (similar pattern)
- `src/FennecLabs.Cli/NupkgHelper.cs` — `GetDlls` used in directory branch
- `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` — primary file
