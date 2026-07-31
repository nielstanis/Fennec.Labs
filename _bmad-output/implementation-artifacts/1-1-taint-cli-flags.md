# Story 1.1: Add taint flags and project-input types to `fennec instrument`

Status: ready-for-dev

## Story

As a developer,
I want `fennec instrument` to accept taint-specific flags and project/solution file inputs,
so that I can enable optional taint analysis without affecting the existing instrument workflow.

## Acceptance Criteria

1. Running `fennec instrument MyAssembly.dll` **without** any `--taint` flag produces byte-identical stdout, JSON output, and `.fennec/instrument/` file layout to the pre-taint baseline; exit code is unchanged.
2. Running `fennec instrument --taint MyApp.csproj` resolves the MSBuild build output DLL for that project and proceeds with analysis — no error about unsupported input type.
3. Running `fennec instrument --taint MyApp.sln` enumerates all projects in the solution and resolves their build output DLLs.
4. Running `fennec instrument --taint MyApp.slnx` parses the `.slnx` XML format and resolves all project build outputs.
5. Running `fennec instrument --taint --taint-max-depth 3 --taint-timeout 60 --taint-policy ./my-policy.json --taint-include-third-party --taint-second-party-prefix MyOrg. MyApp.csproj` parses without error; `--help` lists all new taint flags with defaults; all existing flags remain functional.
6. Running `fennec instrument --taint MyApp.csproj` when the project has **not been built** (no `bin/` output) fails with a clear actionable message (e.g. "Run `dotnet build` first") and a non-zero exit code.

## Tasks / Subtasks

- [ ] Add 7 taint options to `instrumentCommand` in `Program.cs` (AC: 5)
  - [ ] `--taint` (bool, default false)
  - [ ] `--taint-policy <path>` (string, optional)
  - [ ] `--taint-max-depth <int>` (int, default 8)
  - [ ] `--taint-timeout <int>` (int, default 120)
  - [ ] `--taint-llm-handoff` (bool, default false)
  - [ ] `--taint-include-third-party` (bool, default false)
  - [ ] `--taint-second-party-prefix <prefix>` (string[], repeatable/AllowMultipleArgumentsPerToken)
- [ ] Extend `--filename` option (or add new `--input`) to accept `.csproj`, `.sln`, `.slnx` alongside `.dll`/`.nupkg` (AC: 2, 3, 4, 6)
- [ ] Create `BuildGraphReader` in `src/FennecLabs.Cli/Commands/Taint/BuildGraphReader.cs` (AC: 2, 3, 4, 6)
  - [ ] `.csproj` → parse `<OutputPath>` / `<AssemblyName>` from XML; locate DLL under `bin/`
  - [ ] `.sln` → parse `Project(...)` lines; enumerate `.csproj` entries; resolve each via csproj path
  - [ ] `.slnx` → parse XML `<Project Path="..."/>` elements; resolve each
  - [ ] Return `IReadOnlyList<string>` of resolved absolute DLL paths
  - [ ] If no DLL found for any project, throw `BuildOutputNotFoundException` with hint
- [ ] Create `BuildOutputNotFoundException` in `src/FennecLabs.Cli/Commands/Taint/` (AC: 6)
- [ ] Create `TaintOptions` record in `src/FennecLabs.Cli/Commands/Taint/TaintOptions.cs` (AC: 1, 5)
  - [ ] Fields: `Enabled`, `PolicyPath`, `MaxDepth`, `TimeoutSeconds`, `LlmHandoff`, `IncludeThirdParty`, `SecondPartyPrefixes`
  - [ ] Static `Disabled` singleton
- [ ] Thread `TaintOptions` through `InstrumentCommandHandler.ExecuteAsync` (AC: 1)
  - [ ] When `Enabled == false`, no taint path executes — existing behavior completely unchanged
- [ ] Add `OutputCache.TaintDir(root, scope, runId)` helper (placeholder for Story 1.4 run-id generation)
- [ ] Tests in `test/FennecLabs.Cli.Tests/InstrumentTaintFlagTests.cs`:
  - [ ] AC-1: instrument without `--taint` on fixture DLL → byte-identical output set, no taint files
  - [ ] AC-5: `--help` output lists all 7 taint flags
  - [ ] AC-6: `.csproj` with missing `bin/` → non-zero exit + actionable message
  - [ ] AC-2/3/4: `BuildGraphReader` unit tests for each format using in-test fixture strings/temp files

## Dev Notes

### Critical: Do NOT change existing behavior

`InstrumentCommandHandler.ExecuteAsync` currently takes `(string? filename, string? nuget, string? version, string output, string fileFormat, OutputMode outputMode)`. New taint parameters MUST be additive — pass a single `TaintOptions` at the end, defaulting to `TaintOptions.Disabled`.

```csharp
// src/FennecLabs.Cli/Commands/Taint/TaintOptions.cs
internal record TaintOptions(
    bool Enabled,
    string? PolicyPath,
    int MaxDepth,
    int TimeoutSeconds,
    bool LlmHandoff,
    bool IncludeThirdParty,
    IReadOnlyList<string> SecondPartyPrefixes)
{
    public static TaintOptions Disabled { get; } = new(
        Enabled: false,
        PolicyPath: null,
        MaxDepth: 8,
        TimeoutSeconds: 120,
        LlmHandoff: false,
        IncludeThirdParty: false,
        SecondPartyPrefixes: []);
}
```

### How flags are added (`System.CommandLine` v2.0.10)

Follow the existing pattern in `Program.cs`:

```csharp
var taintOption = new Option<bool>("--taint") { Description = "Enable taint analysis" };
var taintPolicyOption = new Option<string>("--taint-policy") { Description = "Path to custom taint policy JSON" };
var taintMaxDepthOption = new Option<int>("--taint-max-depth") { Description = "Max call-chain depth (default: 8)", DefaultValueFactory = _ => 8 };
var taintTimeoutOption = new Option<int>("--taint-timeout") { Description = "Analysis timeout in seconds (default: 120)", DefaultValueFactory = _ => 120 };
var taintLlmHandoffOption = new Option<bool>("--taint-llm-handoff") { Description = "Emit LLM handoff artifact" };
var taintIncludeThirdPartyOption = new Option<bool>("--taint-include-third-party") { Description = "Walk IL of third-party NuGet assemblies" };
var taintSecondPartyPrefixOption = new Option<string[]>("--taint-second-party-prefix")
{
    Description = "Package prefix treated as second-party (repeatable)",
    AllowMultipleArgumentsPerToken = true,
};
```

Add to `instrumentCommand` only (not root). They are **not** Recursive.

### BuildGraphReader — implementation sketch

```csharp
// Returns absolute paths to resolved DLLs
internal static class BuildGraphReader
{
    public static IReadOnlyList<string> Resolve(string inputPath)
    {
        return Path.GetExtension(inputPath).ToLowerInvariant() switch
        {
            ".csproj"  => [ResolveCsproj(inputPath)],
            ".sln"     => ResolveSln(inputPath),
            ".slnx"    => ResolveSlnx(inputPath),
            _          => throw new ArgumentException($"Unsupported input: {inputPath}")
        };
    }

    private static string ResolveCsproj(string csprojPath)
    {
        // Parse XML for <AssemblyName> and <OutputPath> (fall back to bin/Debug/net*/ProjectName.dll heuristic)
        // Search for the DLL; if not found, throw BuildOutputNotFoundException
    }
}
```

The `bin/` search should try common TFM patterns (`bin/Debug/net*`, `bin/Release/net*`) and pick the newest build by LastWriteTime if multiple matches. If zero matches, throw.

### OutputCache.TaintDir

```csharp
internal static string TaintDir(string root, string scope, string runId) =>
    Path.Combine(root, "instrument", scope, "taint", runId);
```

Lives in `OutputCache.cs` alongside `ComparePath`, `ReproducePath`, etc. The `runId` will be sha256-based in Story 1.4 — pass any string for now.

### Project Structure

| File | Action |
|------|--------|
| `src/FennecLabs.Cli/Program.cs` | UPDATE — add 7 taint options to `instrumentCommand` block; thread through to handler |
| `src/FennecLabs.Cli/Commands/InstrumentCommandHandler.cs` | UPDATE — add `TaintOptions` parameter; early-return guard when `Enabled=false` |
| `src/FennecLabs.Cli/Commands/Taint/TaintOptions.cs` | NEW |
| `src/FennecLabs.Cli/Commands/Taint/BuildGraphReader.cs` | NEW |
| `src/FennecLabs.Cli/Commands/Taint/BuildOutputNotFoundException.cs` | NEW |
| `src/FennecLabs.Cli/OutputCache.cs` | UPDATE — add `TaintDir` helper |
| `test/FennecLabs.Cli.Tests/InstrumentTaintFlagTests.cs` | NEW — 4 test groups covering ACs 1, 5, 6, 2/3/4 |

Do **not** touch `FennecLabs.Instrumentation`, `FennecLabs.Contracts`, or any other project — pure CLI surface.

### Testing approach

- Tests call handlers and readers directly (not via CLI parsing) — see `CompareLocalFilesHandlerTests.cs`.
- `InternalsVisibleTo("FennecLabs.Cli.Tests")` already set.
- Use `UniqueTempDirectory()` for output isolation; clean up in `finally`.
- For AC-1 snapshot, run `InstrumentCommandHandler.ExecuteAsync` on the smallest existing fixture DLL in `test/TestProjects/`.
- For `BuildGraphReader` tests, create minimal `.csproj`/`.sln`/`.slnx` content as temp files in the test itself (no real MSBuild invocation needed — reader only parses XML/text).

### Backward-compatibility definition (AC-1)

Assert:
- Exit code = 0
- Same output file names as baseline (not content)
- Zero files under any path containing `/taint/`
- JSON output has no top-level `taint` key

### References

- [Source: epics-taint-analysis.md#Story-1.1] — authoritative ACs for this story
- [Source: ARCHITECTURE-SPINE.md#AD-1] — opt-in gate invariant
- [Source: ARCHITECTURE-SPINE.md#AD-17] — csproj/sln/slnx input scope + unbuilt-project fail behavior (OQ-12 resolved)
- [Source: ARCHITECTURE-SPINE.md#AD-9] — additive output / backward compat invariant

## Dev Agent Record

### Agent Model Used

_to be filled by dev agent_

### Debug Log References

### Completion Notes List

### File List
