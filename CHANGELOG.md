# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- New `FennecLabs.TaintAnalysis` project with `TaintPolicyLoader` for the built-in `fennec.taint.policy.v1` taint policy (source categories: `network-input`, `file-input`, `environment`, `deserialization`, `database-read`; sink categories: `sql-injection`, `command-injection`, `path-traversal`, `xss`, `ssrf`, `log-injection`) plus `sanitizer`/`propagator` rules; `--taint-policy <path>` merges a user policy on top of the built-in set (new `id` appends, existing `id` overrides in place), with clear validation errors reporting file path and field name (Epic 2 / Story 2.1)
- `instrument --taint` now accepts `.dll`, `.csproj`, `.sln`, and `.slnx` inputs (`FennecLabs.Cli.Commands.Taint.BuildGraphReader` resolves project/solution inputs to their built output DLL(s)) and writes a taint findings artifact to `.fennec/instrument/<scope>/taint/<run-id>/result.json` containing `sourcesInventory`/`sinksInventory` derived from the loaded policy; `findings` stays empty pending the CFG/call-graph propagation engine (later Epic 2 stories); new `--taint`, `--taint-policy`, `--taint-max-depth`, `--taint-timeout`, `--taint-llm-handoff`, `--taint-include-third-party`, and `--taint-second-party-prefix` flags are additive — `instrument` without `--taint` is unchanged (Epic 2 / Story 2.1)
- `scorecard` command now emits its `result.json` artifact (and `--json` stdout) in the canonical `DashboardArtifactEnvelope<ScorecardGraphPayload>` shape (`FennecLabs.Contracts.ScorecardGraphPayload`/`ScorecardPackageResult`/`ScorecardCheckResult`/`ScorecardStatus`, `FennecLabs.Scorecard.ScorecardGraphNormalizer`); results are keyed by the same normalized package identity used by `dependencies`, and packages with no located repository or a failed lookup are represented explicitly via `status: "unavailable"`/`"error"` plus a structured `ArtifactError` rather than being silently omitted (Epic 1 / Story 1.3)
- New `dependencies` command normalizes `dotnet package list --include-transitive --format json` output into the canonical `DashboardArtifactEnvelope<DependencyGraphPayload>` shape (`FennecLabs.Contracts.DependencyGraphPayload`/`DependencyNode`, `FennecLabs.DotNetCli.DependencyGraphNormalizer`); package identity is normalized to lowercase invariant culture and deduplicated across top-level/transitive lists, preferring top-level; artifacts are written to `.fennec/dependencies/<project>/<timestamp>/result.json` (Epic 1 / Story 1.2)
- Capture `Ldftn`, `Ldvirtftn`, and `Jmp` opcodes in `AssemblyAnalyzer.Analyze` so delegate/event-handler construction and tail calls show up as invocations instead of being silently dropped (FD-009)

### Fixed

- Surface `dotnet list package` failures in the `scorecard` command: `GetPackageListAsync` now throws `InvalidOperationException` with the trimmed stderr when `dotnet` exits non-zero, and `ScorecardCommandHandler` prints the real error and exits 1 instead of misleadingly reporting "No packages found in the project." (FD-029)
- Fix zip-slip path traversal in `NupkgHelper.ExtractAsync`: validate each archive entry resolves within the extraction root before writing; throw `InvalidOperationException` on traversal attempts from untrusted `.nupkg` files (FD-033)

### Changed

- Commands now print full help after a missing-argument error: `instrument` and `compare` invoke subcommand help on mutual-exclusion failures; `reproduce`, `feeds add`, and `feeds remove` use `Required = true` for automatic System.CommandLine validation (FD-030)
- Add short aliases for all options that previously had only long forms: `-C`/`--no-cache` (global), `-r`/`--report-format` (scorecard), `-f`/`--file` (compare), `-d`/`--default` (feeds add); all subcommand effective alias sets verified clash-free (FD-031)
- Rename NuGet package ID from `FennecLabs` to `Fennec.Labs`; install command updated to `dotnet tool install --global Fennec.Labs` (FD-027)
- Replace `--format human|json` string option with boolean `--json` / `-j` flag across all commands; human-readable output remains the default (FD-024)

### Added

- `reproduce --directory` with multiple TFM subdirs now shows an interactive `SelectionPrompt` instead of erroring; non-interactive and `--json` modes retain the existing error message; unidentifiable TFM (flat dir, no TFM hint) is now a hard error instead of a silent fallback; `resolvedTfm` field added to directory-mode JSON output (FD-034)
- Add `--directory`/`-d` option to `reproduce` command accepting a build output directory instead of a `.nupkg`; add `--tfm`/`-t` for target framework selection with automatic derivation from directory name, single-subdir auto-select, and multi-TFM disambiguation; feed DLLs filtered to `lib/{tfm}/` for accurate matching; `localFile` renamed `localSource` in JSON output (FD-032)
- Add AGPL-3.0-or-later `LICENSE` file; set `PackageLicenseExpression` in `Fennec.csproj`; all 13 production dependencies audited as MIT/Apache-2.0 compatible; license section and badge added to README (FD-028)
- Add `README.md` usage section covering all five commands with representative examples, `--json` flag, and `.fennec/` output cache convention; add `SECURITY.md` with vulnerability reporting process; add Contributing section linking to `CONTRIBUTING.md` (FD-026)
- Add `.github/` with 6 SHA-pinned, zizmor-clean workflows: CI build with TRX test results and Cobertura coverage summary, CodeQL SAST (weekly + on push), zizmor workflow scan, dependency-review license/CVE gate, manual prerelease pipeline (build → attest → feedz.io), and tag-triggered release pipeline (build → attest → nuget.org via trusted publishing); dependabot daily updates with 7-day cooldown; CODEOWNERS (FD-025)
- Add `DiffEventFormatMessageTests.cs` (28 pure `FormatMessage` unit tests) and 27 `AssemblyComparer` integration tests covering assembly name/version/attribute, type base/interface/flag, method flag/body-presence/locals/exception-handlers, field, property accessor, and event diffs; `FennecLabs.AssemblyDiff` line coverage 86.8% (FD-023)
- Add `FennecLabs.Cli.Tests` project with 24 tests covering `OutputCache` path methods and read/write, `ColorTheme.ForScore` boundaries, `NupkgHelper.GetDlls` and `ExtractAsync`, and `CompareLocalFilesCommandHandler` validation and DLL comparison paths (FD-022)
- Add `ScorecardReportBuilder` with `BuildHtml` and `BuildMarkdown`; `result.json` enriched with `project`, `framework`, `generatedAt`, and `dependencyTree` (top-level + transitive); `--report-format html|md|html,md` replaces `--report` bool flag (FD-020)
- Add `coverage.runsettings` (Coverlet/Cobertura config), `dotnet-reportgenerator-globaltool` local tool manifest, and `CONTRIBUTING.md` coverage workflow — single-command collect + HTML report generation (FD-021)
- Extract `NupkgHelper` static class with `ExtractAsync` and `GetDlls` — eliminates duplicated nupkg extraction logic from `ReproduceCommandHandler` and `CompareLocalFilesCommandHandler` (FD-019)
- Add `compare --file a b` — compare two local `.dll` or `.nupkg` files without NuGet; supports human and JSON output with same schema as NuGet compare path (FD-018)
- Replace `List<string> Differences` in `AssemblyComparisonResult` with `List<DiffEvent>` — 29 typed record subtypes covering assembly, type, method, field, property, and event diffs; derived views `TypesOnlyInAssembly1/2` and `MethodBodyChanges` computed via LINQ; CLI JSON output updated to `events` and `methodBodyChanges` fields (FD-017)
- Add `AssemblyComparisonResult.cs` and `DiffEvent.cs` as separate files split from the `AssemblyComparer` monolith (FD-017)
- Add `CompareNestedTypes` recursion in `AssemblyComparer` — nested types now detected added/removed/modified at any depth (FD-017)
- Add `MethodPInvokeInfoDiff` — P/Invoke metadata differences now detected and reported (FD-017)
- Add `TypeSecurityDeclarationDiff` and `MethodSecurityDeclarationDiff` — security declaration presence now compared for both types and methods (FD-017)
- Add structured output directory: all commands write results under `.fennec/<command>/` subfolders; `compare` and `reproduce` cache results to disk and serve on repeat runs; global `--no-cache` forces a fresh run; scorecard HTML report co-located with `result.json` under a timestamped dir (FD-016)
- Add global `--format human|json` option (recursive across all subcommands); all five commands emit structured JSON to stdout when `--format json` is passed, with progress suppressed (FD-015)
- Add `Rendering/ScorecardRenderer` rendering a Spectre.Console table with score thresholds (green ≥7, yellow 4–6.9, red <4) and failing checks inline (FD-014)
- Add `Rendering/DiffRenderer` rendering per-DLL diff results with `+`/`−`/`~` color semantics for added/removed/changed types and methods (FD-014)
- Add download progress spinners via `AnsiConsole.Status()` on compare, reproduce, and instrument NuGet operations (FD-014)
- Add Spectre.Console 0.55.2 as CLI rendering foundation (FD-006)
- Add `OutputMode` enum (`Human`, `Json`) and `Json.Options` shared serializer settings used by all command handlers (FD-015)
- Add `OutputFormat` enum (`Fxt`, `Json`) replacing the stringly-typed `WriterFactory.CreateWriter(string, ...)` overload; valid formats are now statically verifiable (FD-012)
- Add `CancellationToken` support to `AssemblyAnalyzer.Analyze()` and all `Writer.WriteOutputAsync` overloads; token is threaded through to `JsonSerializer.SerializeAsync` and `StreamWriter.WriteLineAsync` (FD-012)
- Add 3 edge-case tests for `AssemblyAnalyzer`: empty assembly, method with no call instructions, deeply nested namespace (FD-012)
- Extract all command handlers to `Commands/` folder: `InstrumentCommandHandler`, `ScorecardCommandHandler`, `CompareCommandHandler`, `ReproduceCommandHandler`, `FeedsCommandHandler`; `Program.cs` reduced to a thin wiring layer (FD-008)
- Add `FennecLabs.AssemblyDiff.Tests` project with 13 in-memory Cecil tests covering added/removed types, visibility changes, IL operand diffs, nested types, and report truncation (FD-008)
- Add `CONTRIBUTING.md` documenting `dotnet test --filter "Category!=Live"` for CI and `--filter "Category=Live"` for full integration suite (FD-008)
- Add `--file-format fxt|json` option to `instrument` command (renamed from `--format`), making JSON file output reachable (FD-007)
- Add `feeds list/add/remove` subcommands wired to `FeedService` and `ConfigurationManager` (FD-007)
- Add `Directory.Build.props` enforcing `TreatWarningsAsErrors` across all projects (FD-007)
- Add `--output`/`-o` option to `instrument` command with default `.fennec`; NuGet instrumentation now scopes output under `<packageId>/<version>/` (FD-005)
- Scorecard command now fetches scores for transitive dependencies in addition to top-level packages, surfacing the full dependency graph's security posture (FD-004)
- Add offline scorecard fixture JSON and live integration tests for PollyAwsMvcApp packages, with Category=Live tagging for CI filter support (FD-003)
- Add PollyAwsMvcApp test fixture (Polly + AWSSDK.Core) with exact transitive package assertions and TestProjectCsprojAttribute for reliable csproj path resolution (FD-002)

### Fixed

- `AssemblyComparer` now detects differing `MethodImplAttributes` (`AggressiveInlining`, `NoInlining`, etc.) between matched methods (FD-011)
- `WriterFactory.CreateWriter` now throws `ArgumentException` for unknown format strings instead of silently falling back to FXT (FD-010)
- All command handlers now return non-zero exit codes on failure, enabling CI integration (FD-007)
- Route all error messages to `Console.Error` for reliable stdout piping (FD-007)
- Remove debug `Console.WriteLine` noise leaking from `FxtWriter` and `Writer` into user output (FD-007)
- `JsonWriter` no longer swallows IO exceptions silently; `FxtWriter` dead `//try` skeleton removed (FD-007)
- Fix `instrument` output double-nesting (`fenneclabs/fenneclabs/`) by removing hardcoded subfolder from `FxtWriter` (FD-005)

### Changed

- Rename `instrument --format` to `--file-format` (controls fxt/json file output); global `--format` now exclusively means human vs json output mode (FD-015)
- `feeds list` renders a Spectre.Console table with default feed highlighted; `feeds add/remove` use styled confirmation output (FD-014)
- Rename `AssemblyAnalyzer.Analyse()` → `Analyze()` for US English consistency across the codebase (FD-012)
- `AssemblyComparer` compares custom attribute constructor arguments and named parameters for attributes present in both assemblies, not just attribute presence (FD-011)
- `AssemblyComparer` includes `ParameterAttributes` (`In`, `Out`, `Optional`) in the method signature key so methods differing only in parameter attributes are treated as distinct (FD-011)
- Extract duplicated output path logic from `FxtWriter` and `JsonWriter` into a single `ResolveOutputPath` method on the `Writer` base class (FD-010)
- Remove unused `_module` field from `ClassTypeResult`; constructor now takes only `classtype` (FD-010)
- Replace `.ToLower().Trim()` with `StringComparison.OrdinalIgnoreCase` in `WriterFactory` (FD-010)
- `NuGetService` download pipeline deduplicated into a single `ResolveAndDownloadAsync<T>` primitive; removed unused `feedName` parameter from all public methods; consistent `SourceCacheContext` and `DownloadResourceResult` disposal (FD-008)
- `ScorecardClient` now accepts an injected `HttpClient` (optional; creates default when null) and implements `IDisposable`, disposing only the client it owns (FD-008)
- `ScorecardClient` returns `null` instead of throwing `ArgumentException` when no repository URL found in package metadata (FD-007)
- Bump NuGet.Protocol to 7.3.1 and System.CommandLine to 2.0.7, resolving NU1901 vulnerability warnings (FD-001)
