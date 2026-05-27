# FD-028: Add GNU Affero General Public License v3.0

**Status:** Pending Verification
**Priority:** Medium
**Effort:** Low (< 1 hour)
**Impact:** Establishes clear legal terms for use, modification, and distribution of the project

## Problem

The FennecLabs repository has no LICENSE file, which means the project is implicitly "all rights reserved" — contributors and users have no explicit rights to use, modify, or distribute the code. Adding AGPL-3.0-or-later establishes:

- Strong copyleft that requires derivative works (including SaaS deployments) to publish their source
- A clear signal to the open-source community about the project's intentions
- Machine-readable license metadata on the NuGet package

The `-or-later` variant is chosen over `-only` so that the project can be relicensed under future AGPL versions without requiring contributor consent.

## Dependency License Compatibility

All production dependencies were audited. Every package uses MIT or Apache-2.0 — both are AGPL-3.0-or-later compatible (FSF-confirmed: permissive licenses can be incorporated into AGPL projects without restriction).

| Package | Version | License | Compatible |
|---------|---------|---------|-----------|
| Mono.Cecil | 0.11.6 | MIT | ✅ |
| Spectre.Console | 0.55.2 | MIT | ✅ |
| Spectre.Console.Ansi | 0.55.2 | MIT | ✅ |
| System.CommandLine | 2.0.8 | MIT | ✅ |
| NuGet.Protocol | 7.6.0 | Apache-2.0 | ✅ |
| NuGet.Common | 7.6.0 | Apache-2.0 | ✅ |
| NuGet.Configuration | 7.6.0 | Apache-2.0 | ✅ |
| NuGet.Frameworks | 7.6.0 | Apache-2.0 | ✅ |
| NuGet.Packaging | 7.6.0 | Apache-2.0 | ✅ |
| NuGet.Versioning | 7.6.0 | Apache-2.0 | ✅ |
| Newtonsoft.Json | 13.0.3 | MIT | ✅ |
| System.Security.Cryptography.Pkcs | 8.0.1 | MIT | ✅ |
| System.Security.Cryptography.ProtectedData | 8.0.0 | MIT | ✅ |

Test-only packages (xunit, coverlet, Microsoft.NET.Test.Sdk) are not distributed and do not affect the shipped product's license obligations.

**No conflicts found.**

## Solution

1. Add a `LICENSE` file at the repository root containing the full AGPL-3.0 text (verbatim from https://www.gnu.org/licenses/agpl-3.0.txt)
2. Set `<PackageLicenseExpression>AGPL-3.0-or-later</PackageLicenseExpression>` in `src/FennecLabs.Cli/Fennec.csproj` so the NuGet package advertises the correct SPDX identifier
3. Remove any `<PackageLicenseUrl>` if present (deprecated in favour of the expression)
4. Update the README badge/mention if a license section exists

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `LICENSE` | CREATE | Full AGPL-3.0 license text at repo root |
| `src/FennecLabs.Cli/Fennec.csproj` | MODIFY | Add `<PackageLicenseExpression>AGPL-3.0-or-later</PackageLicenseExpression>` |
| `README.md` | MODIFY | Add license badge / section if not present |

## Verification

1. `cat LICENSE` — first line should be `GNU AFFERO GENERAL PUBLIC LICENSE`
2. `dotnet pack src/FennecLabs.Cli/Fennec.csproj` — inspect `.nupkg` with `unzip -p *.nupkg *.nuspec | grep -i license`; should show `AGPL-3.0-or-later`
3. Visit repo on GitHub — LICENSE tab should display the AGPL-3.0 text and the license badge should appear automatically

## Related

- FD-027: Rename NuGet Package ID to Fennec.Labs (established package metadata conventions)
- FD-026: README.md Content (may need a license section added)
