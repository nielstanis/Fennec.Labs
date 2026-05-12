# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Add `--output`/`-o` option to `instrument` command with default `.fennec`; NuGet instrumentation now scopes output under `<packageId>/<version>/` (FD-005)
- Scorecard command now fetches scores for transitive dependencies in addition to top-level packages, surfacing the full dependency graph's security posture (FD-004)
- Add offline scorecard fixture JSON and live integration tests for PollyAwsMvcApp packages, with Category=Live tagging for CI filter support (FD-003)
- Add PollyAwsMvcApp test fixture (Polly + AWSSDK.Core) with exact transitive package assertions and TestProjectCsprojAttribute for reliable csproj path resolution (FD-002)

### Fixed

- Fix `instrument` output double-nesting (`fenneclabs/fenneclabs/`) by removing hardcoded subfolder from `FxtWriter` (FD-005)

### Changed

- Bump NuGet.Protocol to 7.3.1 and System.CommandLine to 2.0.7, resolving NU1901 vulnerability warnings (FD-001)
