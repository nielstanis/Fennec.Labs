# Contributing to FennecLabs

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
