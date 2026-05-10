# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Add PollyAwsMvcApp test fixture (Polly + AWSSDK.Core) with exact transitive package assertions and TestProjectCsprojAttribute for reliable csproj path resolution (FD-002)

### Changed

- Bump NuGet.Protocol to 7.3.1 and System.CommandLine to 2.0.7, resolving NU1901 vulnerability warnings (FD-001)
