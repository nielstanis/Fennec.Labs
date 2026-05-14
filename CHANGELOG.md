# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

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
