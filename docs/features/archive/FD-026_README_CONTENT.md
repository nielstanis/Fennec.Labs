# FD-026: README.md Content — CLI Usage, SECURITY.md, CONTRIBUTING.md

**Status:** Complete
**Completed:** 2026-05-25
**Priority:** Medium
**Effort:** Low (< 1 hour)
**Impact:** Gives new users and contributors a clear entry point: how to use the CLI, how to report security issues, and how to contribute

## Problem

The current `README.md` is minimal and does not explain how to use the CLI, where to report security vulnerabilities, or how to contribute. New users have no starting point and contributors have no guidance at a glance.

## Solution

1. **CLI usage section** — Add a `## Usage` section with the top-level commands and representative examples for each (`compare`, `reproduce`, `instrument`, `scorecard`, `feeds`). Include `--json` flag usage and the `.fennec/` output cache convention.

2. **SECURITY.md** — Create a new `SECURITY.md` file following the GitHub standard (supported versions table, how to report a vulnerability, expected response timeline). Reference it from `README.md` with a `## Security` section linking to the file.

3. **CONTRIBUTING.md reference** — Add a `## Contributing` section to `README.md` that briefly summarises the workflow and points to `CONTRIBUTING.md` for full details.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `README.md` | MODIFY | Add `## Usage`, `## Security`, `## Contributing` sections |
| `SECURITY.md` | CREATE | Supported versions, vulnerability reporting process |

## Verification

- `README.md` renders correctly on GitHub (check headings, code blocks, links)
- All links resolve: `SECURITY.md`, `CONTRIBUTING.md`
- `SECURITY.md` appears in the GitHub Security tab as the repo's security policy

## Related

- [FD-025](archive/FD-025_GITHUB_ACTIONS_WORKFLOWS.md) — GitHub Actions and repo hygiene groundwork
