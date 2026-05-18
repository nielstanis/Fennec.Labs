# FD-022: FennecLabs.Cli Test Coverage

**Status:** In Progress
**Priority:** Medium
**Effort:** Medium (3–5 hours)
**Impact:** Gives `FennecLabs.Cli` non-zero coverage (currently 0%); catches regressions in
path-building, validation, and cache logic without requiring network or a live terminal.

## Problem

`FennecLabs.Cli` has no test project. All logic in the command handlers, renderers,
`OutputCache`, `NupkgHelper`, and `ColorTheme` is untested. FD-021 notes this explicitly:
the project will report 0% coverage.

Not all of it is worth testing — the network-bound and Spectre.Console-bound code is hard to
isolate. But a meaningful slice of the CLI is **pure logic** that can be tested directly.

## What to Test

### Tier 1 — Pure logic, no I/O, high confidence

**`ColorTheme.ForScore`**
Five boundary tests covering `≥7 → "green"`, `≥4 → "yellow"`, `<4 → "red"`, and the exact
boundary values (4.0 and 7.0).

**`OutputCache` path methods** (`ComparePath`, `ReproducePath`, `ScorecardDir`)
Verify the cache path composition — correct directory structure, correct filename, correct
separator handling. These paths are load-bearing for the cache-hit logic.

### Tier 2 — Filesystem, no network, no terminal

**`OutputCache.WriteAsync` / `TryLoad` / `Exists`**
Write to a `Path.GetTempPath()` file, verify `Exists` returns true, `TryLoad` returns the
content, and cleanup succeeds. Uses real filesystem but is fast and hermetic.

**`NupkgHelper.GetDlls`**
Create a temp directory tree with `.dll` files (including a `_._` stub), call `GetDlls`,
assert: correct relative paths returned as keys, `_._` excluded, non-dll files excluded.

**`NupkgHelper.ExtractAsync`**
Build a minimal in-memory zip (nupkg is a zip), write to a temp `.nupkg`, extract to temp
dir, assert expected files are present at expected relative paths.

### Tier 3 — Integration (optional, tagged `Category=Integration`)

**`CompareLocalFilesCommandHandler` validation paths**
The early-return validation in `ExecuteAsync` has no Spectre or network dependency:
- File not found → exit 1
- Mixed extensions (`.dll` + `.nupkg`) → exit 1
- Invalid extension (`.exe`) → exit 1

Test by passing non-existent or temp files with the wrong extension. Capture stderr via
`Console.SetError`. No real DLLs needed for these paths.

**`CompareLocalFilesCommandHandler` DLL comparison** (tagged `Category=Integration`)
Use DLLs from the solution's own build output (e.g. `FennecLabs.AssemblyDiff.dll`):
- Identical DLLs → exit 0, `AreEqual = true`
- Two different DLLs → exit 0, events reported

## What Not to Test Here

| Code | Reason to skip |
|------|---------------|
| `CompareCommandHandler` | Requires NuGet network |
| `ReproduceCommandHandler` | Requires NuGet network |
| `ScorecardCommandHandler` | Requires OSSF API + `dotnet list package` |
| `InstrumentCommandHandler` | Requires NuGet network |
| `DiffRenderer` / `ScorecardRenderer` | Require `AnsiConsole` capture (non-trivial; defer) |
| `ScorecardReportBuilder` | Will be implemented in FD-020; test there |
| `Program.cs` | CLI wiring; end-to-end territory |

## Setup

### `InternalsVisibleTo`

All tested types are `internal`. Add to `src/FennecLabs.Cli/Fennec.csproj`:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
    <_Parameter1>FennecLabs.Cli.Tests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

### Test project

`test/FennecLabs.Cli.Tests/FennecLabs.Cli.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../../src/FennecLabs.Cli/Fennec.csproj" />
  </ItemGroup>
</Project>
```

Add to solution: `dotnet sln add test/FennecLabs.Cli.Tests/FennecLabs.Cli.Tests.csproj`

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `test/FennecLabs.Cli.Tests/FennecLabs.Cli.Tests.csproj` | CREATE | Test project |
| `test/FennecLabs.Cli.Tests/OutputCacheTests.cs` | CREATE | Path methods + WriteAsync/TryLoad/Exists |
| `test/FennecLabs.Cli.Tests/ColorThemeTests.cs` | CREATE | `ForScore` boundary tests |
| `test/FennecLabs.Cli.Tests/NupkgHelperTests.cs` | CREATE | `GetDlls` + `ExtractAsync` |
| `test/FennecLabs.Cli.Tests/CompareLocalFilesHandlerTests.cs` | CREATE | Validation + integration paths |
| `src/FennecLabs.Cli/Fennec.csproj` | MODIFY | Add `InternalsVisibleTo` |
| `FennecLabs.sln` | MODIFY | Add test project to solution |

## Verification

1. `dotnet build` — 0 warnings, 0 errors
2. `dotnet test test/FennecLabs.Cli.Tests/` — all tests pass
3. `dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage"` — coverage XML
   includes `FennecLabs.Cli` with non-zero line coverage for `OutputCache`, `ColorTheme`,
   `NupkgHelper`
4. Integration tests excluded from standard run via `--filter "Category!=Integration"`

## Related

- FD-021: Code coverage setup — `FennecLabs.Cli` will appear with non-zero coverage after this FD
- FD-020: `ScorecardReportBuilder` — will add its own tests when implemented
- FD-019: `NupkgHelper` extracted — direct test target here
