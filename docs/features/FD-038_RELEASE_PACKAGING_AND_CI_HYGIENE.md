# FD-038: Release Packaging and CI Hygiene

**Status:** Open
**Priority:** High
**Effort:** Low-Medium (1 day)
**Impact:** The nuget.org listing, version numbers and CI gates look and behave like a 1.0 product.

## Problem

- **Bare nuget.org listing.** The published 0.7.5 nuspec has `authors: Fennec`, `description: Package Description`,
  no project/repository URL, icon, readme or tags. `Fennec.csproj` sets only PackAsTool/PackageId/License/ToolCommandName.
- **README says the tool is unreleased** and points at a feedz.io `0.7.5-preview.2`, although
  `dotnet tool install -g Fennec.Labs` works from nuget.org (0.7.5, tag `v0.7.5`, 2026-07-22).
- **Previews sort below stable.** `VersionPrefix` is still `0.7.5`, so every `0.7.5-preview.N` from `prerelease.yml`
  is SemVer-older than the released `0.7.5`.
- **Two test projects never run.** `FennecLabs.NuGet.Tests` and `FennecLabs.Scorecard.Tests` are not in
  `FennecLabs.slnx`; all workflows run `dotnet test FennecLabs.slnx`.
- **Live tests run in CI.** No workflow passes `--filter "Category!=Live"`, contrary to `CONTRIBUTING.md`.
- **Packaging is untested before a tag.** `ci-build.yml` never packs; its artifact upload globs files that never exist.
- **`docs/` is gitignored** (`.gitignore` line 7) while 33 files under it are tracked — new docs get silently ignored.
- Library projects are packable by default; `RollForward` is not set (net10-only machines only).

## Solution

1. Centralise package metadata in `Directory.Build.props` (Authors, Description, PackageProjectUrl,
   RepositoryUrl, PublishRepositoryUrl, PackageTags, Copyright, `ContinuousIntegrationBuild` on CI,
   `IsPackable=false` by default) and in `Fennec.csproj` (`IsPackable=true`, `PackageReadmeFile`, `PackageIcon`
   = `FennecLabs.png`, `RollForward=Major`).
2. Versioning: bump `VersionPrefix` to `1.0.0` for the launch (or adopt MinVer so tags drive the version).
3. Add the two missing test projects to the `.slnx`; add `--filter "Category!=Live"` to ci/prerelease/release.
4. CI: `dotnet pack` + `dotnet tool install --add-source` + `fennec --help` smoke step.
5. Remove `docs/` from `.gitignore`.
6. README install: `dotnet tool install --global Fennec.Labs`; previews from feedz as a secondary option.
7. `release.yml`: create a GitHub Release with the nupkg and CHANGELOG section as notes.
8. Check what `tag-validate-action` `reject_development` does to `v1.0.0-rc.1` tags before planning an RC.

## Verification

- `dotnet pack src/FennecLabs.Cli` → nuspec contains description, authors, repository url, icon, readme, tags.
- `dotnet pack FennecLabs.slnx` produces only `Fennec.Labs`.
- CI runs all 7 test projects offline and passes.
