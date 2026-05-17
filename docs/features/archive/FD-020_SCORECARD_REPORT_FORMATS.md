# FD-020: Scorecard Report — JSON-Driven Generation and MD/HTML Format Support

**Status:** Complete
**Completed:** 2026-05-17
**Priority:** Medium
**Effort:** Medium (3–5 hours)
**Impact:** Persists the full dependency tree in `result.json` so reports can be regenerated
from cache; extracts `ScorecardReportBuilder` as a testable rendering layer; adds Markdown
as a second report format alongside HTML.

## Problem

### 1. Report not derived from cached JSON

`ScorecardCommandHandler` builds `List<PackageScorecardResult>` in memory, serializes it to
`result.json`, then passes the same in-memory list to `BuildHtmlReport`. The HTML report is
a parallel consumer of the live data — it does **not** read from the JSON.

`--report` therefore only works during a live run; there is no way to regenerate a report
from a previously-saved `result.json` without re-fetching all scorecards.

### 2. `result.json` is missing structural data

The JSON schema currently stores a flat package list with scores. The HTML report's
**Dependency Tree** section — which splits packages into Top-Level vs Transitive and shows
the source project and framework — uses `PackageListResult` from the live `dotnet list
package` call. This data is **not persisted**, so a rebuilt report would silently lose the
tree view.

### 3. Only HTML is supported

`--report` always generates `report.html`. Markdown would be useful for GitHub PRs,
READMEs, and CI summaries.

## Solution

### Phase 1 — Enrich `result.json` schema

Store the full dependency tree alongside the scorecard results. Add two top-level fields:

**`project`** — path to the `.csproj` or solution file used (or `"."` for CWD).

**`dependencyTree`** — mirrors `PackageListResult` structure from `dotnet list package`:

```json
{
  "project": "src/MyApp/MyApp.csproj",
  "framework": "net10.0",
  "generatedAt": "2026-05-17T14:30:00",
  "dependencyTree": {
    "topLevel": [
      { "id": "Newtonsoft.Json", "requestedVersion": "13.0.*", "resolvedVersion": "13.0.1" },
      { "id": "Polly", "requestedVersion": "8.0.0", "resolvedVersion": "8.0.0" }
    ],
    "transitive": [
      { "id": "Microsoft.Extensions.Logging", "resolvedVersion": "8.0.0" }
    ]
  },
  "packages": [
    {
      "packageId": "Newtonsoft.Json",
      "packageVersion": "13.0.1",
      "score": 7.5,
      "checks": [{ "name": "Maintained", "score": 10, "reason": "..." }],
      "error": null
    }
  ]
}
```

`packages` stays as a flat list (order: top-level first, then transitive). The
`dependencyTree` gives the report builder everything it needs to reconstruct the tree view.

Source mapping:
- `project` ← `projectPath ?? "."`
- `framework` ← `project.Frameworks[0].FrameworkName`
- `dependencyTree.topLevel` ← `framework.TopLevelPackages`
- `dependencyTree.transitive` ← `framework.TransitivePackages`

**Backward compatibility**: old cached `result.json` files lack `dependencyTree`. The
builder treats a missing or null tree as "tree unavailable" and omits the tree section from
the report rather than erroring.

### Phase 2 — `ScorecardReportBuilder`

Extract all report generation into
`src/FennecLabs.Cli/Rendering/ScorecardReportBuilder.cs`. The builder operates on a thin
internal model deserialized from `result.json`, not on `PackageScorecardResult`:

```csharp
internal record ScorecardReport(
    string Project,
    string? Framework,
    DateTime GeneratedAt,
    ScorecardDependencyTree? DependencyTree,
    IReadOnlyList<ScorecardReportPackage> Packages);

internal record ScorecardDependencyTree(
    IReadOnlyList<ScorecardPackageRef> TopLevel,
    IReadOnlyList<ScorecardPackageRef> Transitive);

internal record ScorecardPackageRef(string Id, string? RequestedVersion, string? ResolvedVersion);

internal record ScorecardReportPackage(
    string PackageId,
    string PackageVersion,
    decimal? Score,
    IReadOnlyList<ScorecardReportCheck> Checks,
    string? Error);

internal record ScorecardReportCheck(string Name, int Score, string? Reason);
```

Public surface:

```csharp
internal static class ScorecardReportBuilder
{
    internal static string BuildHtml(ScorecardReport report) { ... }
    internal static string BuildMarkdown(ScorecardReport report) { ... }
}
```

`ScorecardCommandHandler` maps its live data into `ScorecardReport` before calling the
builder. The builder can also be driven directly from a deserialized `result.json`.

### Phase 3 — Markdown format

`BuildMarkdown` produces a self-contained `.md` file:

```markdown
# Security Scorecard Report

**Project:** src/MyApp/MyApp.csproj | **Framework:** net10.0 | **Generated:** 2026-05-17

## Summary

| Total | With Scorecard | Avg Score | Errors |
|-------|---------------|-----------|--------|
| 12    | 10            | 7.2/10    | 0      |

## Dependency Tree

### Top-Level Packages

| Package | Version | Score |
|---------|---------|-------|
| Newtonsoft.Json | 13.0.1 | 7.50 ✅ |

### Transitive Packages

| Package | Version | Score |
|---------|---------|-------|
| Microsoft.Extensions.Logging | 8.0.0 | — |

## Detailed Results

### Newtonsoft.Json 13.0.1 — 7.50/10

| Check | Score | Reason |
|-------|-------|--------|
| Maintained | 10/10 | 30 commits in the last 90 days... |
```

Score emoji: ✅ ≥7, ⚠️ 4–6.9, ❌ <4, — no scorecard.

### Phase 4 — CLI changes

Replace `--report` (bool) with `--report-format <format>`:

```
--report-format html         → report.html
--report-format md           → report.md
--report-format html,md      → both files
```

Old `--report` / `-r` flag is removed (no external contract). `--report-format` is optional;
omitting it skips report generation.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Rendering/ScorecardReportBuilder.cs` | CREATE | `ScorecardReport` model + `BuildHtml` + `BuildMarkdown` |
| `src/FennecLabs.Cli/Commands/ScorecardCommandHandler.cs` | MODIFY | Enrich JSON schema; map to `ScorecardReport`; delegate to builder; update `--report-format` dispatch |
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Replace `--report` bool with `--report-format` string option |

## Verification

1. `fennec scorecard --project Foo.csproj` — `result.json` contains `project`, `framework`,
   `generatedAt`, `dependencyTree` with `topLevel` / `transitive` arrays
2. `fennec scorecard --project Foo.csproj --report-format html` — `report.html` generated;
   Dependency Tree section shows top-level and transitive split; scores match console output
3. `fennec scorecard --project Foo.csproj --report-format md` — `report.md` generated with
   correct Markdown tables and score emojis
4. `fennec scorecard --project Foo.csproj --report-format html,md` — both files generated
   in the same timestamped output dir
5. Old cached `result.json` (no `dependencyTree`) — report generates without the tree section,
   no error
6. `fennec scorecard --report` — parse error (flag removed)
7. `ScorecardReportBuilder.BuildHtml` / `BuildMarkdown` are unit-testable with a fixture
   `ScorecardReport` object (no live API or file I/O)

## Related

- FD-016: Established `result.json` cache path (`OutputCache.ScorecardDir`)
- FD-015: Established `OutputMode` and JSON output flag
