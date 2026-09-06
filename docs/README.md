# Fennec.Labs Documentation

Comprehensive reference for every `fennec` CLI command. Start with the top-level
[README.md](../README.md) for installation and a quick-start overview; use the pages
below when you need full option coverage, output artifact details, or troubleshooting.

## Commands

| Command | Purpose |
|---------|---------|
| [`instrument`](commands/instrument.md) | Extract IL-level method invocations from an assembly or NuGet package |
| [`compare`](commands/compare.md) | Diff assemblies between two NuGet package versions or two local files |
| [`reproduce`](commands/reproduce.md) | Verify a local build/`.nupkg` matches the published NuGet.org artifact |
| [`scorecard`](commands/scorecard.md) | Fetch OpenSSF Scorecard results for a project's dependencies |
| [`dependencies`](commands/dependencies.md) | Emit a normalized dependency graph artifact for a project |
| [`feeds`](commands/feeds.md) | Manage configured NuGet feed sources |

## Reference

- [Output artifacts & JSON schemas](output-schemas.md) — the canonical `DashboardArtifactEnvelope`
  shape used by `scorecard` and `dependencies`, plus the `.fennec/` cache layout for every command.

## Global options

Every command accepts these options (place them before or after the subcommand):

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--json` | `-j` | off | Write output as JSON instead of the human-readable console view. Progress/status output is suppressed. |
| `--output <path>` | `-o` | `.fennec` | Root folder for all file output (cached results, reports, instrumentation dumps). |
| `--no-cache` | `-C` | off | Bypass any cached `result.json` for this input and force a fresh run. |

Run `fennec --help` or `fennec <command> --help` at any time for the authoritative,
version-matched option list.
