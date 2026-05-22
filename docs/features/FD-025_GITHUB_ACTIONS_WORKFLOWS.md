# FD-025: GitHub Actions Workflows — CI, Scanning, and Release Pipeline

**Status:** Pending Verification
**Priority:** High
**Effort:** Medium (2-4 hours)
**Impact:** Automated build/test/coverage on every push, security scanning, and a signed attested release pipeline to nuget.org

## Problem

The repo has no `.github/` configuration. There is no automated CI, no dependency hygiene, no SAST, and no release pipeline. Every test run, scan, and publish step is manual.

## Solution

Create a security-hardened `.github/` folder modelled on [blowdart/idunno.Security.Ssrf](https://github.com/blowdart/idunno.Security.Ssrf/tree/main/.github) (documented in `docs/gitworkflow.md`). All actions pinned to full commit SHAs. Least-privilege permissions declared per-job.

### Decisions made

| Concern | Choice |
|---------|--------|
| Publish target (prerelease) | feedz.io private feed |
| Publish target (release) | nuget.org (NuGet trusted publishing) |
| Signing / provenance | Sigstore via `actions/attest-build-provenance` — no Azure required |
| Release trigger | Tag (`v*.*.*`) for releases **+** manual `workflow_dispatch` for prereleases |
| CI branch scope | `main` only — push + PRs |
| harden-runner | Yes, audit mode on non-build workflows |
| Optional checks | CodeQL (SAST), zizmor (workflow scan), dependency-review |
| Docs site | Not in scope |
| Package ID | `Fennec` (dotnet tool, `PackAsTool = true`) |
| GitHub repo | `nielstanis/Fennec.Labs` |
| .NET SDK version | `10.0.x` only |

### Workflows to create

| File | Purpose | Trigger |
|------|---------|---------|
| `ci-build.yml` | Build, test, coverage report | Push/PR to `main` |
| `codeql.yml` | C# SAST → Security tab | Push to `main`, weekly, `workflow_dispatch` |
| `actions-security-analysis.yml` | zizmor scan of workflow files | Push/PR when `.github/workflows/**` changes |
| `dependency-review.yml` | Block PRs with bad licenses or CVEs | PRs only |
| `prerelease.yml` | Build → attest → publish prerelease | `workflow_dispatch` (manual) |
| `release.yml` | Build → attest → publish release | Push of `v*.*.*` tag |

### Supporting files to create

| File | Purpose |
|------|---------|
| `.github/dependabot.yml` | Daily nuget + github-actions updates with 7-day cooldown |
| `.github/CODEOWNERS` | `* @nielstanis` |

### Key design constraints (from gitworkflow.md)

- Every `uses:` pinned to full SHA with version comment — Dependabot keeps them fresh
- `permissions: contents: read` at workflow level; jobs widen only what they need
- `actions/checkout` always with `fetch-depth: 0` and `persist-credentials: false`
- `step-security/harden-runner` (egress-policy: audit) on CodeQL, zizmor, dependency-review
- CI build workflow explicitly **not** hardened (needs free network access for dotnet restore)
- Signing: `actions/attest-build-provenance` on the nupkg after pack — no code-signing cert needed
- Release publish uses NuGet trusted publishing (`NuGet/login` action, no long-lived API key stored)
- Prerelease publish uses a `FEEDZ_API_KEY` secret and `FEEDZ_FEED_URL` secret (or hardcoded URL) scoped to the `prerelease` environment — **URL TBD**
- `--skip-duplicate` on all `dotnet nuget push` calls for idempotent reruns

### CI build job shape (`ci-build.yml`)

1. Checkout (depth 0, no credential persist)
2. Setup .NET 10.0.x
3. `dotnet build --configuration Debug`
4. Upload build artifacts (nupkg, dll) — 5-day retention
5. `dotnet test --collect:"XPlat Code Coverage" --logger junit --settings coverage.runsettings`
6. `EnricoMi/publish-unit-test-result-action` — test results in PR
7. `danielpalme/ReportGenerator-GitHub-Action` — Cobertura + Markdown coverage
8. Append Markdown summary to `$GITHUB_STEP_SUMMARY`
9. Upload coverage artifact

Artifact glob: `src/**/*.nupkg`

### Release job shape (`release.yml` + `prerelease.yml`)

Two jobs each:

1. **`build`** — checkout → setup .NET → `dotnet build/test/pack --configuration Release` → `actions/attest-build-provenance` on each nupkg → upload `build-artifacts`
For `release.yml`:
2. **`publish`** (depends on build) — download artifacts → NuGet trusted publishing login (`NuGet/login`) → `dotnet nuget push` to nuget.org

For `prerelease.yml`:
2. **`publish`** (depends on build) — download artifacts → `dotnet nuget push` to feedz.io using `FEEDZ_API_KEY` secret

`prerelease.yml` adds `perform_publish: bool` dispatch input so you can dry-run without publishing.

`release.yml` adds `lfreleng-actions/tag-validate-action` requiring semver + `require_owner: nielstanis`.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `.github/workflows/ci-build.yml` | CREATE | Build, test, coverage |
| `.github/workflows/codeql.yml` | CREATE | SAST |
| `.github/workflows/actions-security-analysis.yml` | CREATE | zizmor workflow scan |
| `.github/workflows/dependency-review.yml` | CREATE | License + CVE gate on PRs |
| `.github/workflows/prerelease.yml` | CREATE | Manual prerelease pipeline |
| `.github/workflows/release.yml` | CREATE | Tag-triggered release pipeline |
| `.github/dependabot.yml` | CREATE | Automated dependency updates |
| `.github/CODEOWNERS` | CREATE | Review routing |

## Verification

1. Open a PR to `main` → `ci-build.yml` runs, test results appear in PR checks, coverage in summary
2. Open a PR to `main` → `dependency-review.yml` blocks if a bad dep is introduced
3. Edit any workflow file and push → `actions-security-analysis.yml` runs zizmor
4. Push a prerelease dispatch → dry-run with `perform_publish: false` succeeds; artifacts uploaded
5. Push a `v0.1.0` tag → `release.yml` runs, nupkg attested and pushed to nuget.org
6. Confirm `actions/attest` provenance appears in the GitHub repo's attestations page

## Out of Scope

- GitHub Pages / DocFX docs site (no docs workflow)
- Azure Trusted Signing or Key Vault code-signing (using Sigstore instead)
- MyGet feed (nuget.org + GitHub Packages only)
- `environment:` blocks for CI (not needed without secret scoping on build)

## Related

- [`docs/gitworkflow.md`](../gitworkflow.md) — reference workflow design and all design principles
- FD-021 — Code Coverage setup (`coverage.runsettings` already exists)
