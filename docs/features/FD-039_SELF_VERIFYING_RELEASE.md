# FD-039: Self-Verifying Release — SBOM, Scorecard, Reproducible Build

**Status:** Open
**Priority:** Medium-High
**Effort:** Medium (2-3 days)
**Impact:** Fennec meets the supply-chain bar it measures others against. "Fennec verifies its own release"
becomes the launch story.

## Problem

Fennec is a supply-chain tool, so reviewers will check its own posture first. Today:

- Provenance attestation (`attest-build-provenance`) and SHA-pinned actions are in place — good.
- **No SBOM** (no CycloneDX/SPDX step anywhere).
- **No OpenSSF Scorecard workflow or badge** for this repo, although `fennec scorecard` is a headline command.
- **Not reproducible by construction:** no `global.json` (SDK floats as `10.0.x`), no lock files, no
  `ContinuousIntegrationBuild`/path-map settings. Fennec likely fails its own `reproduce`.
- **No GitHub Release** objects; `SECURITY.md` names `security@fennec.dev` (unverified mailbox) and does not mention
  GitHub private vulnerability reporting.

## Solution

1. `global.json` pinning the SDK feature band; `RestorePackagesWithLockFile=true` + committed `packages.lock.json`;
   `ContinuousIntegrationBuild`/`Deterministic` in CI (shared with FD-038).
2. Generate a CycloneDX SBOM in `release.yml`, attest it (`actions/attest-sbom`) and attach it to the GitHub Release.
3. Add `ossf/scorecard-action` workflow + README badge.
4. Dogfood: a release job that rebuilds from the tag and runs
   `fennec reproduce -f <rebuilt>.nupkg -n Fennec.Labs -v <tag>` against the just-published package
   (depends on FD-035 exit codes). Publish the result as a release asset.
5. `SECURITY.md`: enable and reference GitHub private vulnerability reporting; verify or replace the mailbox.

## Verification

- Release run produces: nupkg, SBOM, provenance + SBOM attestations, reproduce report with exit 0.
- Scorecard badge renders on README.

## Risks

- .NET builds may not be bit-for-bit reproducible across runners even with determinism flags; if so, document the
  residual differences and use them to tune `reproduce` noise filtering (valuable launch content either way).
