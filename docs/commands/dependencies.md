# `fennec dependencies`

Emits a normalized, canonical dependency graph artifact for a project's full transitive
dependency tree — a stable foundation other tooling (e.g. `scorecard`, dashboards) can join
against by package identity.

## When to use it

- You need a stable, deduplicated view of every package a project depends on (direct + transitive).
- You're building or feeding a dashboard that needs to join dependency data with other signals
  (e.g. scorecard results) by a consistent package identity.
- You want a JSON artifact of the dependency tree for auditing or CI consumption.

## Inputs

| Option | Short | Required | Description |
|--------|-------|----------|-------------|
| `--project <path>` | `-p` | yes | Path to the `.csproj` file to analyze. |

Plus the [global options](../README.md#global-options): `--json`, `--output`, `--no-cache`.

## Output

Written to `<output>/dependencies/<projectName>/<timestamp>/result.json`: the canonical
`DashboardArtifactEnvelope<DependencyGraphPayload>` (see [output schemas](../output-schemas.md)).
`--json` stdout uses the same shape.

Normalization rules:
- Package identity (`id`) is lowercased with invariant culture, so casing differences never
  produce duplicate nodes.
- Packages appearing in both the top-level and transitive `dotnet package list` output are
  deduplicated, preferring the top-level entry (`isTopLevel: true`).

## Examples

```bash
# Full dependency graph for a project
fennec dependencies --project src/MyApp/MyApp.csproj

# JSON to stdout (canonical envelope shape)
fennec dependencies --project src/MyApp/MyApp.csproj --json
```

## Edge cases & troubleshooting

- Underlying data comes from `dotnet package list --include-transitive --format json`; the
  project must already be restorable (`dotnet restore`) for accurate results.
- A project targeting multiple frameworks produces one payload per invocation for the resolved
  target framework — re-run per TFM if you need multiple.
