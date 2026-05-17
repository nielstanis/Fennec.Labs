# FD-021: Code Coverage with Coverlet and ReportGenerator

**Status:** Complete
**Completed:** 2026-05-17
**Priority:** Medium
**Effort:** Low (< 1 hour)
**Impact:** Gives the team a concrete coverage number per project; HTML drill-down for
uncovered lines; runnable locally with a single command.

## Problem

`coverlet.collector` 6.0.4 is already referenced in all 5 test projects and
`Microsoft.NET.Test.Sdk` is present in each, so the data collection infrastructure is in
place. However:

- No `.runsettings` file exists — Coverlet runs with defaults (no exclusions, no merge config).
- No report generator is installed — the raw Cobertura XML is never turned into anything
  readable.
- There is no documented workflow for producing a coverage report.

Running `dotnet test --collect:"XPlat Code Coverage"` today would generate one
`coverage.cobertura.xml` per test project under scattered `TestResults/<guid>/` directories
with no easy way to view the results.

## Solution

### Step 1 — `.runsettings` file

Create `coverage.runsettings` at the repo root to configure Coverlet consistently across
all test projects:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <Exclude>[*.Tests]*,[FennecLabs.TestUtilities]*</Exclude>
          <ExcludeByAttribute>GeneratedCodeAttribute,ExcludeFromCodeCoverageAttribute</ExcludeByAttribute>
          <SingleHit>false</SingleHit>
          <IncludeTestAssembly>false</IncludeTestAssembly>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

### Step 2 — Local tool manifest with `reportgenerator`

Add a local tool manifest so coverage report generation is reproducible without requiring a
global install:

```
dotnet new tool-manifest          # creates .config/dotnet-tools.json
dotnet tool install dotnet-reportgenerator-globaltool
```

`reportgenerator` merges multiple Cobertura XML files and generates HTML, a text summary,
and a Cobertura merge file.

### Step 3 — Coverage workflow

Run tests with coverage collection:

```bash
dotnet test --settings coverage.runsettings \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults
```

Generate the report:

```bash
dotnet tool run reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:coverage-report \
  -reporttypes:"Html;TextSummary;Cobertura"
```

Open `coverage-report/index.html` to browse line-level coverage.

Document both commands in `CONTRIBUTING.md` under a new **Code Coverage** section.

### What gets measured

| Test project | Source project measured |
|---|---|
| `FennecLabs.AssemblyDiff.Tests` | `FennecLabs.AssemblyDiff` |
| `FennecLabs.Instrumentation.Tests` | `FennecLabs.Instrumentation` |
| `FennecLabs.DotNetCli.Tests` | `FennecLabs.DotNetCli` |
| `FennecLabs.NuGet.Tests` | `FennecLabs.NuGet` |
| `FennecLabs.Scorecard.Tests` | `FennecLabs.Scorecard` |

`FennecLabs.Cli` (the command handlers) is not currently tested — it will show 0% coverage,
which is accurate and useful signal.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `coverage.runsettings` | CREATE | Coverlet config — format, exclusions |
| `.config/dotnet-tools.json` | CREATE | Local tool manifest pinning `reportgenerator` |
| `CONTRIBUTING.md` | MODIFY | Document the coverage workflow |
| `.gitignore` | MODIFY | Ignore `TestResults/` and `coverage-report/` |

## Verification

1. `dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults` — all tests pass; `TestResults/**/coverage.cobertura.xml` files exist
2. `dotnet tool run reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:coverage-report -reporttypes:"Html;TextSummary"` — runs without error
3. `coverage-report/index.html` opens; shows per-project coverage breakdown
4. `coverage-report/Summary.txt` prints aggregate line/branch coverage numbers
5. Test project code itself does not appear in the coverage source (excluded by runsettings)

## Related

- `CONTRIBUTING.md` — CI filter docs added in FD-008
