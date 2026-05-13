# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Extract all command handlers to `Commands/` folder: `InstrumentCommandHandler`, `ScorecardCommandHandler`, `CompareCommandHandler`, `ReproduceCommandHandler`, `FeedsCommandHandler`; `Program.cs` reduced to a thin wiring layer (FD-008)
- Add `FennecLabs.AssemblyDiff.Tests` project with 13 in-memory Cecil tests covering added/removed types, visibility changes, IL operand diffs, nested types, and report truncation (FD-008)
- Add `CONTRIBUTING.md` documenting `dotnet test --filter "Category!=Live"` for CI and `--filter "Category=Live"` for full integration suite (FD-008)
- Add `--format fxt|json` option to `instrument` command, making JSON output reachable (FD-007)
- Add `feeds list/add/remove` subcommands wired to `FeedService` and `ConfigurationManager` (FD-007)
- Add `Directory.Build.props` enforcing `TreatWarningsAsErrors` across all projects (FD-007)
- Add `--output`/`-o` option to `instrument` command with default `.fennec`; NuGet instrumentation now scopes output under `<packageId>/<version>/` (FD-005)
- Scorecard command now fetches scores for transitive dependencies in addition to top-level packages, surfacing the full dependency graph's security posture (FD-004)
- Add offline scorecard fixture JSON and live integration tests for PollyAwsMvcApp packages, with Category=Live tagging for CI filter support (FD-003)
- Add PollyAwsMvcApp test fixture (Polly + AWSSDK.Core) with exact transitive package assertions and TestProjectCsprojAttribute for reliable csproj path resolution (FD-002)

### Fixed

- All command handlers now return non-zero exit codes on failure, enabling CI integration (FD-007)
- Route all error messages to `Console.Error` for reliable stdout piping (FD-007)
- Remove debug `Console.WriteLine` noise leaking from `FxtWriter` and `Writer` into user output (FD-007)
- `JsonWriter` no longer swallows IO exceptions silently; `FxtWriter` dead `//try` skeleton removed (FD-007)
- Fix `instrument` output double-nesting (`fenneclabs/fenneclabs/`) by removing hardcoded subfolder from `FxtWriter` (FD-005)

### Changed

- `NuGetService` download pipeline deduplicated into a single `ResolveAndDownloadAsync<T>` primitive; removed unused `feedName` parameter from all public methods; consistent `SourceCacheContext` and `DownloadResourceResult` disposal (FD-008)
- `ScorecardClient` now accepts an injected `HttpClient` (optional; creates default when null) and implements `IDisposable`, disposing only the client it owns (FD-008)
- `ScorecardClient` returns `null` instead of throwing `ArgumentException` when no repository URL found in package metadata (FD-007)
- Bump NuGet.Protocol to 7.3.1 and System.CommandLine to 2.0.7, resolving NU1901 vulnerability warnings (FD-001)
