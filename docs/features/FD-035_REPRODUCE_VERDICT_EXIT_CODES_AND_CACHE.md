# FD-035: Reproduce Verdict Exit Codes and Artifact-Aware Cache

**Status:** In Progress
**Priority:** High (launch blocker)
**Effort:** Low (2-4 hours)
**Impact:** `fennec reproduce` becomes usable as a CI gate: a non-reproducible build fails the job, and a
rebuilt `.nupkg` never gets a stale cached verdict.

## Problem

`reproduce` is the flagship supply-chain check ("does the published package match what I built?"), but its
verdict never reaches the exit code:

1. **Differences exit 0.** Every success path returns `errorCount > 0 ? 1 : 0`
   (`ReproduceCommandHandler.cs`). A build where every DLL differs, or where the local build is missing a DLL
   that the feed package ships, exits 0. A CI step running `fennec reproduce` can never fail on the thing it
   exists to detect.
2. **"No matching DLL files found to compare." exits 0.** Nothing was verified, yet the command reports success.
3. **Cache hits always exit 0**, regardless of the cached summary.
4. **Stale cached verdicts.** The cache key is `reproduce/<packageId>/<version>/result.json` — it ignores the
   local artifact. Rebuild the `.nupkg`, rerun, and the old verdict is served (with exit 0).
5. **"latest" is never resolved.** Without `--version` the result says `feedVersion: "latest"` instead of the
   version actually compared, and the run is never cached.

## Solution

### Exit-code contract for `reproduce`

| Exit | Meaning |
|------|---------|
| `0` | Reproducible: every matched DLL is identical and the local and feed DLL sets are the same |
| `1` | Error: invalid input, network/feed failure, a DLL that could not be compared, or no matching DLLs at all |
| `2` | Not reproducible: at least one DLL differs, or a DLL exists only locally or only in the feed package |

`2` for "differs" (rather than `1`) keeps "the tool failed" and "the tool worked and found a difference"
distinguishable in CI, mirroring the `diff`/`cmp` convention of separating "different" from "trouble".
Errors take precedence over differences.

In `--directory` mode, DLLs only present locally are ignored for the verdict: a build output directory
normally also contains dependency assemblies that are not part of the package. DLLs the package ships but
the local build lacks still yield `2`.

A single helper `DllPipeline.ReproduceExitCode(identical, different, errors, onlyInLocal, onlyInFeed)` computes
the code for both `--filename` and `--directory` modes and for cache hits (by reading the cached `summary`,
`onlyInLocal`, `onlyInFeed`).

### Artifact-aware cache (`--filename` mode)

- Resolve the feed version up front (explicit `--version`, else latest including prereleases — matching what
  `NuGetService` downloads) via a new `NuGetService.ResolveVersionAsync`. The result reports the concrete
  `feedVersion`, and the run is always cacheable.
- Cache path becomes `reproduce/<packageId>/<version>/<sha256-prefix>/result.json`, where the prefix is the first
  16 hex chars of the local `.nupkg` SHA-256. A rebuilt package gets a new key automatically.
- Add `localSha256` (full hash) to the result JSON so the verdict is traceable to an exact artifact.
- `--directory` mode stays uncached (unchanged).

### Out of scope

- `compare` exit codes: differences between two published versions are the expected *output* of `compare`,
  not a failure. Its behaviour is unchanged except that cache hits now return the same code a fresh run would
  (`1` when the cached summary has errors).

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Commands/DllPipeline.cs` | MODIFY | `ReproduceExitCode` helper + `ExitCodeFromCachedResult` |
| `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` | MODIFY | Use exit-code contract, resolve version, hash-keyed cache, `localSha256` |
| `src/FennecLabs.Cli/Commands/CompareCommandHandler.cs` | MODIFY | Cache hit returns the same exit code as a fresh run |
| `src/FennecLabs.Cli/OutputCache.cs` | MODIFY | `ReproducePath` takes the artifact hash |
| `src/FennecLabs.NuGet/NuGetService.cs` | MODIFY | `ResolveVersionAsync` |
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Help text documents exit codes |
| `docs/commands/reproduce.md`, `docs/output-schemas.md` | MODIFY | Exit codes, cache path, `localSha256` |
| `test/FennecLabs.Cli.Tests/` | MODIFY | Exit-code helper tests, cache path tests |

## Verification

1. `ReproduceExitCode` unit tests: all identical → 0; any different → 2; only-in-local/only-in-feed → 2;
   errors → 1 (even with differences); zero matched → 1.
2. Cache path includes the hash; two different files for the same package/version get different paths.
3. Cached result with `different > 0` yields exit 2.
4. `dotnet test` green (offline: `--filter "Category!=Live"`).

### Results (2026-10-07)

- `dotnet build FennecLabs.slnx`: 0 warnings, 0 errors. Offline tests: all 213 in the `.slnx` pass; Scorecard.Tests 24/24.
  NuGet.Tests has 2 failures, identical before this change: untagged network tests that hit the NuGet search
  endpoint, which the sandbox proxy blocks (see FD-038, Live tagging).
- Smoke against nuget.org with `Humanizer.Core`:
  - local 2.14.1 vs feed 2.14.1 → `✓ Reproducible`, exit 0; rerun served from
    `reproduce/Humanizer.Core/2.14.1/117be88dd74fbbef/result.json`, exit 0
  - same file, no `--version` → resolved `feedVersion: "3.0.10"`, `✗ Not reproducible`, exit 2
  - corrupt `.nupkg` → error, exit 1

## Related

- FD-032, FD-034 — `reproduce --directory` and TFM resolution
- FD-037 — remaining `reproduce`/`compare` correctness fixes (Windows path matching, `compare --version` wording)

### `/fd-verify` results (2026-10-08, after proofread fix `4dc6fd1`)

| # | Scenario | Result | Exit |
|---|----------|--------|------|
| 1 | Offline test suites | All pass except the 2 known NuGet search-endpoint tests (sandbox network) | — |
| 2 | 2.14.1 vs 2.14.1, fresh then cached | `✓ Reproducible`; cached run shows summary + verdict | 0 / 0 |
| 3 | Repacked nupkg with extra `lib/net6.0/Extra.dll` | New cache folder `3af708f4051be0f4`; "Only in local", `✗ Not reproducible` | 2 |
| 4 | 2.13.14 local vs feed 2.14.1, fresh then cached | 3 different, `✗ Not reproducible` | 2 / 2 |
| 5a | `--directory` with an extra dependency DLL (Spectre.Console.dll) | Local-only DLL listed but ignored, `✓ Reproducible` | 0 |
| 5b | `--directory` with `Humanizer.dll` removed | "No matching DLL files found to compare." | 1 |
| 6 | `-v 99.0.0` / `-v abc` / missing file | One-line error each | 1 / 1 / 1 |
| 7 | `compare -n Humanizer.Core`, fresh then cached | 7 different (expected for compare) | 0 / 0 |
