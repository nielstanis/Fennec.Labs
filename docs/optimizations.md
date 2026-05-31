# FennecLabs — Optimization & Improvement Analysis

Last updated: 2026-05-31 (original: 2026-05-13; revised after 4-agent parallel re-audit).

---

## What Changed Since the Original Doc

Everything in the original Tier 1 (8 items) and most of Tier 2 (4 of 5 items) is **done**.
The codebase now has: extracted command handlers, de-duplicated NuGetService,
HttpClient-injected ScorecardClient, AssemblyDiff test coverage, feeds CLI, consistent
error routing, TreatWarningsAsErrors, and structured JSON output on all commands.

The gaps that remain are new issues introduced by that refactoring, plus the surviving
Tier 2 and Tier 3 work.

---

## Surviving Issues from the Original Doc

### Tier 2 — Partially Done

**Live-test traits + CI filter (originally Tier 2 #4)**

| What | Status |
|------|--------|
| `[Trait("Category", "Live")]` on ScorecardClient tests | Done |
| `[Trait("Category", "Live")]` on NuGetService tests | **Missing** — `NuGetServiceTests.cs` hits `api.nuget.org` with no trait |
| CI test filter `--filter "Category!=Live"` | **Missing** — `.github/workflows/ci-build.yml:55` runs `dotnet test FennecLabs.slnx` with no filter |

**Fix:** Add `[Trait("Category", "Live")]` to `NuGetServiceTests.cs` (and confirm
`FeedServiceTests.cs`/`ConfigurationManagerTests.cs`), then add
`--filter "Category!=Live"` to the `dotnet test` step in CI.

---

## New Issues (introduced after 2026-05-13)

### High Priority

**1. Zip-slip / path traversal in `NupkgHelper.ExtractAsync`** — `NupkgHelper.cs:18`

`entryPath = Path.Combine(extractPath, entry.FullName)` with no path-containment check.
A `.nupkg` with an entry name like `../../evil.dll` writes outside the extraction directory.
Reachable from user-supplied files in `reproduce` and `compare --file`.

Fix: Validate `Path.GetFullPath(entryPath).StartsWith(Path.GetFullPath(extractPath) + Path.DirectorySeparatorChar)` before writing.

**2. `ScorecardCommandHandler` always returns exit code 0** — `ScorecardCommandHandler.cs`

Every return path is `return 0`, including total-failure paths (network down, deserialization
failed, all packages errored). `compare`/`reproduce`/`instrument` return non-zero on exceptions;
scorecard does not. CI consumers cannot detect a fully-failed scorecard run.

Fix: Return `1` when the package list could not be loaded (FD-029) or when all package
scorecard fetches failed.

### Medium Priority

**3. `FeedsCommandHandler` inconsistent error contract** — `FeedsCommandHandler.cs:67,88`

`ExecuteAddAsync` catches `Exception` (line 67); `ExecuteRemoveAsync` catches only
`ArgumentException` (line 88). `RemoveFeedAsync` also calls `LoadSettingsAsync` /
`SaveSettingsAsync`, which can throw `IOException`/`JsonException` — these escape as
unhandled exceptions with a stack trace.

Fix: Widen the `remove` catch to `Exception` to match `add`.

**4. Markdown report silently drops error packages** — `ScorecardReportBuilder.cs:92-128`

`BuildMarkdown` counts `withErrors` in the summary but renders no error section.
`BuildHtml` renders errors correctly. A user requesting `--report-format md` sees
"Errors: 3" with no details about which packages failed or why.

Fix: Add an error section to `BuildMarkdown` mirroring `BuildHtml`.

**5. Multi-project / multi-TFM silently truncated** — `ScorecardCommandHandler.cs:38,45`

Only `packageList.Projects[0]` and `project.Frameworks[0]` are analyzed. A solution
with multiple projects or a multi-targeted project silently drops all but the first.

Fix: Iterate all projects and all frameworks, or emit a warning when more than one is found.

**6. `FormatDllResult` inlined in two handlers** — `CompareCommandHandler.cs:144`,
`CompareLocalFilesCommandHandler.cs:155`

The identical anonymous-object JSON projection is copy-pasted in these two handlers.
`ReproduceCommandHandler` already extracted it to a private static helper. The other
two handlers predate that refactoring.

Fix: Extract to a shared `DiffResultProjection` static helper or move to `DiffRenderer`.

### Low Priority

**7. `ParseFileFormat` silently falls back to Fxt** — `InstrumentCommandHandler.cs:192-195`

An unrecognized `--file-format` string silently produces Fxt output with no warning.
Fix: Throw / return an error for unknown format strings.

---

## Tier 3 — Feature Additions

Items from the original doc that are still open, plus new opportunities.

### Still Open (from original doc)

| # | Feature | Effort | Notes |
|---|---------|--------|-------|
| 1 | `--fail-on-diff` flag for `compare`/`reproduce` | Low | `differentCount` already computed; add option, return non-zero when `> 0` |
| 2 | SARIF output on `compare`/`reproduce` | Medium | Precursor: serialize `DiffEvent` fields directly (item below) |
| 3 | `fennec vulnerable` — `dotnet list package --vulnerable` | Low | `DotnetCliExecutor` now exists; one more argument string + model |
| 4 | Breaking-change severity on diffs (`Added`/`Removed`/`ApiBreak`) | Medium | `DiffEvent` records carry enough info; needs a `Severity` mapping layer |
| 5 | `--fail-below <score>` threshold on `scorecard` | Low | Trivial — average score already computed in `ScorecardReportBuilder` |
| 6 | Private feed authentication (ApiKey/PAT) | Medium | `FeedConfiguration` has `Name`/`Source`/`IsDefault` only; needs credential field and NuGet.Protocol provider |
| 7 | Parallel scorecard fetching with `--max-parallel N` | Low-Medium | Per-package try/catch already isolates failures; main wrinkle is concurrent `AnsiConsole` progress output |
| 8 | SBOM emission (CycloneDX) | Medium | `ScorecardReport.DependencyTree` is a ready input model; transform + serializer is the work |

### New Opportunities (not in the original doc)

| # | Feature | Effort | Enabler |
|---|---------|--------|---------|
| A | Structured `DiffEvent` JSON output | Low | Currently only `FormatMessage()` strings are emitted; serialize the underlying typed fields for downstream tooling / SARIF; **prerequisite multiplier** for items 2 and 4 above |
| B | `fennec search` / `versions` / `info` | Low | `NuGetService.SearchPackagesAsync`, `GetPackageVersionsAsync`, `GetPackageMetadataAsync` exist and are wired to nothing; trivial CLI surface |
| C | `dotnet` health family: `outdated`, `deprecated` | Low each | `DotnetCliExecutor` is a generic runner; same JSON shape as `vulnerable`; natural product family alongside FD-029 fix |
| D | `--report-format html|md` on `compare`/`reproduce` | Medium | `ScorecardReportBuilder` proves the pattern; needs a `DiffReportBuilder` counterpart |
| E | Scorecard trend / regression comparison | Medium | Each run writes to a timestamped dir under `.fennec/scorecard/<project>/`; nothing reads prior runs yet |
| F | `--fail-on-diff` + `--fail-below` as a "CI gate" feature | Low | Items 1 and 5 above share the same exit-code policy concept; worth designing together rather than as separate options |

---

## Test Coverage Gaps

The doc's original complaint (AssemblyComparer untested) is resolved. The new gaps:

| Gap | Risk | Suggested test |
|-----|------|---------------|
| FD-029: no test for `GetPackageListAsync` stderr throw (FD is open) | High | Drive against unrestored `.csproj`; assert `InvalidOperationException` with stderr content; assert empty-stderr fall-through still returns `null` |
| `ScorecardCommandHandler`, `CompareCommandHandler`, `ReproduceCommandHandler`, `InstrumentCommandHandler`, `FeedsCommandHandler` all untested | High | At minimum: cache hit/miss, exit-code contract, JSON-vs-Human branching |
| `ScorecardClient` / `NuGetService` — no offline/mocked tests | Medium | Inject stubbed `HttpMessageHandler`; cover 404, non-200, malformed JSON paths |
| Cache hit/miss behavior in handlers | Medium | Pre-seed `OutputCache`, run handler, assert recompute skipped; assert miss triggers write |

---

## Recommended Next Steps (ranked)

1. **Fix zip-slip in `NupkgHelper`** — security bug, 5-line fix
2. **FD-029: surface `dotnet list` stderr** — correctness bug, defined test contract
3. **Fix scorecard exit code** — silent failure on total errors
4. **Live-trait + CI filter** — prevents live tests hitting nuget.org/scorecard API in CI
5. **Structured `DiffEvent` JSON** — Low effort, unlocks SARIF and severity classification
6. **`fennec search` / `versions` / `info`** — dormant APIs, Low effort, rounds out the CLI
