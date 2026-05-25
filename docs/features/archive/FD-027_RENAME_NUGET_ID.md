# FD-027: Rename NuGet Package ID to Fennec.Labs

**Status:** Complete
**Completed:** 2026-05-25
**Priority:** Medium
**Effort:** Low (< 1 hour)
**Impact:** Aligns the published NuGet package ID with the dotted naming convention (`Fennec.Labs`) used by the library projects (`FennecLabs.AssemblyDiff`, `FennecLabs.NuGet`, etc.)

## Problem

The CLI tool's NuGet package ID is currently `FennecLabs` (no dot), while all library projects use the dotted convention `FennecLabs.*`. The canonical dotted form `Fennec.Labs` better reflects the product name and is consistent with how .NET packages are conventionally named.

## Solution

Update the `<PackageId>` (and any related metadata) in the CLI project file from `FennecLabs` to `Fennec.Labs`. Also update any references to the old ID in docs, release workflows, and install instructions.

1. Set `<PackageId>Fennec.Labs</PackageId>` in `src/FennecLabs.Cli/Fennec.csproj`
2. Update the `README.md` install command (`dotnet tool install --global Fennec.Labs`)
3. Update any GitHub Actions workflow steps that reference the package ID (e.g. release/prerelease publish steps)
4. Verify `dotnet pack` produces `Fennec.Labs.<version>.nupkg`

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Fennec.csproj` | MODIFY | Set `<PackageId>Fennec.Labs</PackageId>` |
| `README.md` | MODIFY | Update install command to use new package ID |
| `.github/workflows/release.yml` | MODIFY | Update any hardcoded package ID references |
| `.github/workflows/prerelease.yml` | MODIFY | Update any hardcoded package ID references |

## Verification

- `dotnet pack src/FennecLabs.Cli` produces `Fennec.Labs.<version>.nupkg`
- `README.md` install command uses `Fennec.Labs`
- Release workflow publishes under the new ID

## Related

- [FD-026](FD-026_README_CONTENT.md) — README.md that includes the install command
- [FD-025](archive/FD-025_GITHUB_ACTIONS_WORKFLOWS.md) — Release pipeline that publishes the package
