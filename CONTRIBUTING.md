# Contributing to FennecLabs

## Contributing Changes

### Branching

Never commit directly to `main`. All changes must go through a feature branch and pull request.

```bash
git switch -c <branch-name>   # create and switch to a new branch
# ... make changes ...
git push -u origin <branch-name>
# then open a PR to merge into main
```

Use descriptive branch names. For FD work, prefix with the FD number: `FD-013/mcp-server`.

### Commit messages

Use imperative mood, ≤72 characters. For FD work, prefix with the FD number:

```
FD-013: Add MCP server project scaffold
Fix NuGet feed timeout handling
```

### Before opening a PR

1. Run offline tests — `dotnet test --filter "Category!=Live"`
2. Confirm zero warnings (the build enforces `TreatWarningsAsErrors=true`)
3. Re-read your diff — remove dead code, check naming, no commented-out code

## Testing

Tests are split into offline unit tests and live integration tests. Live tests require network access and hit external services (NuGet.org, the OSSF Scorecard API).

### Running offline tests (CI)

Use the `Category!=Live` filter to run only tests that do not require network access. This is what CI runs:

```bash
dotnet test --filter "Category!=Live"
```

Or per project:

```bash
dotnet test test/FennecLabs.Instrumentation.Tests/ --filter "Category!=Live"
dotnet test test/FennecLabs.Scorecard.Tests/      --filter "Category!=Live"
dotnet test test/FennecLabs.AssemblyDiff.Tests/   --filter "Category!=Live"
dotnet test test/FennecLabs.NuGet.Tests/          --filter "Category!=Live"
dotnet test test/FennecLabs.DotNetCli.Tests/      --filter "Category!=Live"
```

### Running the full integration suite

Use the `Category=Live` filter to run live tests that require network:

```bash
dotnet test --filter "Category=Live"
```

### Projects with Live tests

| Project | Live tests |
|---------|-----------|
| `FennecLabs.Scorecard.Tests` | Yes — calls OSSF Scorecard API and NuGet.org |
| `FennecLabs.NuGet.Tests` | Yes — downloads packages from NuGet.org |
| `FennecLabs.DotNetCli.Tests` | Yes — runs `dotnet` CLI against real projects |
| `FennecLabs.Instrumentation.Tests` | No |
| `FennecLabs.AssemblyDiff.Tests` | No |

## Code Coverage

Coverage is collected via [Coverlet](https://github.com/coverlet-coverage/coverlet) and reported with [ReportGenerator](https://github.com/danielpalme/ReportGenerator). The local tool manifest at `dotnet-tools.json` pins the `reportgenerator` version — restore it once with `dotnet tool restore`.

### Collect coverage

```bash
dotnet test --settings coverage.runsettings \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults
```

### Generate HTML report

```bash
dotnet tool run reportgenerator \
  -reports:"TestResults/**/coverage.cobertura.xml" \
  -targetdir:coverage-report \
  -reporttypes:"Html;TextSummary;Cobertura"
```

Open `coverage-report/index.html` to browse line-level coverage. The text summary is at `coverage-report/Summary.txt`.

Test assemblies and `FennecLabs.TestUtilities` are excluded from coverage measurement via `coverage.runsettings`.
