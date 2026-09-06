# Fennec.Labs

<img src="https://github.com/nielstanis/Fennec.Labs/blob/main/FennecLabs.png?raw=true" alt="Fennec.Labs" style="width:50%; height:auto;">

A .NET CLI tool for analyzing .NET projects — assembly diffing, NuGet inspection, IL instrumentation, OpenSSF Scorecard checks, and NuGet feed management.

## Installation

Fennec Labs has not been released to NuGet at this point (yet). You can either add the following nuget.config on your system and get the tool installed on your system.

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
 <packageSources>
    <add key="fennec-feedz" value="https://f.feedz.io/fennec/labs/nuget/index.json" /> 
</packageSources>
</configuration>
```

```bash
dotnet tool install --global fennec.labs --version 0.7.5-preview.2
```

Or run from source:

```bash
dotnet run --project src/FennecLabs.Cli -- <command> [options]
```

## Documentation

Full command reference — every option, output artifact, JSON schema, and edge case — lives in
[`docs/`](docs/README.md):

| Command | Docs |
|---------|------|
| `instrument` | [docs/commands/instrument.md](docs/commands/instrument.md) |
| `compare` | [docs/commands/compare.md](docs/commands/compare.md) |
| `reproduce` | [docs/commands/reproduce.md](docs/commands/reproduce.md) |
| `scorecard` | [docs/commands/scorecard.md](docs/commands/scorecard.md) |
| `dependencies` | [docs/commands/dependencies.md](docs/commands/dependencies.md) |
| `feeds` | [docs/commands/feeds.md](docs/commands/feeds.md) |

Output artifact layout and JSON schemas (including the canonical dashboard envelope used by
`scorecard`/`dependencies`) are documented in [docs/output-schemas.md](docs/output-schemas.md).

Run `fennec --help` or `fennec <command> --help` at any time for the authoritative,
version-matched option list.

## Usage

<a id="global-options"></a>All commands accept these global options:

| Option | Short | Description |
|--------|-------|-------------|
| `--json` | `-j` | Write output as JSON |
| `--output <path>` | `-o` | Root folder for file output (default: `.fennec`) |
| `--no-cache` | | Bypass cached results and force a fresh run |

### compare

Compare assemblies between two NuGet package versions, or between two local `.dll`/`.nupkg` files.

```bash
# Compare latest two published versions of a NuGet package
fennec compare --nuget Newtonsoft.Json

# Compare a specific version against the latest
fennec compare --nuget Newtonsoft.Json --version 12.0.3

# Compare two local files directly
fennec compare --file old.dll new.dll
fennec compare --file old.nupkg new.nupkg

# JSON output
fennec compare --nuget Newtonsoft.Json --json
```

Results are cached under `.fennec/compare/`. Use `--no-cache` to force a fresh run.

### reproduce

Compare a locally built `.nupkg` against the version published on NuGet.org to verify reproducibility.

```bash
fennec reproduce --filename ./bin/Release/MyLib.1.0.0.nupkg --nuget MyLib
fennec reproduce --filename ./bin/Release/MyLib.1.0.0.nupkg --nuget MyLib --version 1.0.0
```

### instrument

Extract IL-level method invocations from an assembly or NuGet package.

```bash
# From a local assembly
fennec instrument --filename path/to/MyLib.dll

# From a NuGet package (latest version)
fennec instrument --nuget Newtonsoft.Json

# Specific version, JSON output format
fennec instrument --nuget Newtonsoft.Json --version 13.0.3 --file-format json
```

Output is written to `.fennec/instrument/`. The default file format is `fxt`; use `--file-format json` for structured JSON.

### scorecard

Fetch OpenSSF Scorecard results for all NuGet packages (direct and transitive) in a `.csproj`.

```bash
fennec scorecard --project src/MyApp/MyApp.csproj

# Generate HTML and Markdown reports
fennec scorecard --project src/MyApp/MyApp.csproj --report-format html,md

# JSON output
fennec scorecard --project src/MyApp/MyApp.csproj --json
```

`--json` output and the cached `result.json` use the canonical dashboard artifact envelope.
Results are keyed by normalized package identity (matching `dependencies` command output) so
scorecard signals can be joined to dependency graph nodes; packages with no located repository or
a failed lookup are represented explicitly with `status: "unavailable"`/`"error"` and a structured
`error` object rather than being omitted from the payload.

### dependencies

Emit a normalized, canonical dependency graph artifact for a project's transitive dependency tree.

```bash
fennec dependencies --project src/MyApp/MyApp.csproj

# JSON output
fennec dependencies --project src/MyApp/MyApp.csproj --json
```

Package identity is normalized to lowercase invariant culture and deduplicated across top-level
and transitive package lists (top-level wins). Artifacts are written to
`.fennec/dependencies/<project>/<timestamp>/result.json` using the canonical dashboard artifact
envelope (`$schema`, `schemaVersion`, `command`, `producedAt`, `producerVersion`, `sourceContext`, `payload`).

### feeds

Manage NuGet feed sources used by the tool.

```bash
# List configured feeds
fennec feeds list

# Add a feed
fennec feeds add --name MyFeed --source https://my.feed/v3/index.json
fennec feeds add --name MyFeed --source https://my.feed/v3/index.json --default

# Remove a feed
fennec feeds remove --name MyFeed
```

## Security

Please read [SECURITY.md](SECURITY.md) for how to report vulnerabilities.

## Contributing

All contributions go through a feature branch and pull request — never commit directly to `main`. See [CONTRIBUTING.md](CONTRIBUTING.md) for the full workflow, test categories, and code coverage setup.

## License

This project is licensed under the [GNU Affero General Public License v3.0 or later](LICENSE) (AGPL-3.0-or-later).

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)
