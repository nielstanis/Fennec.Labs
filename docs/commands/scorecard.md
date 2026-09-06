# `fennec scorecard`

Fetches [OpenSSF Scorecard](https://securityscorecards.dev/) results for every NuGet package
referenced by a project — direct and transitive — so you can assess supply-chain risk across
your full dependency tree, not just top-level packages.

## When to use it

- Auditing a project's dependency tree for security posture before a release.
- Feeding scorecard signals into a dashboard or CI gate, keyed to the same package identity used
  by [`dependencies`](dependencies.md).
- Generating a shareable HTML/Markdown report for a security review.

## Inputs

| Option | Short | Required | Description |
|--------|-------|----------|-------------|
| `--project <path>` | `-p` | yes | Path to the `.csproj` file to analyze. |
| `--report-format <fmt>` | `-r` | no | Generate a report in `html`, `md`, or `html,md` (both). |

Plus the [global options](../README.md#global-options): `--json`, `--output`, `--no-cache`.

## Output

Written to `<output>/scorecard/<projectName>/<timestamp>/`:
- `result.json` — the canonical `DashboardArtifactEnvelope<ScorecardGraphPayload>` (see
  [output schemas](../output-schemas.md)). `--json` stdout uses the same shape.
- `report.html` / `report.md` — when `--report-format` is passed, co-located in the same directory.

Results are keyed by the same **normalized package identity** as `dependencies` output, so
scorecard signals can be joined directly to dependency graph nodes. Packages with no located
repository, or a failed lookup, are represented explicitly with `status: "unavailable"` or
`"error"` and a structured `error` object — never silently omitted from the payload.

## Examples

```bash
# Scorecard for all direct + transitive dependencies
fennec scorecard --project src/MyApp/MyApp.csproj

# Generate both HTML and Markdown reports alongside result.json
fennec scorecard --project src/MyApp/MyApp.csproj --report-format html,md

# JSON to stdout (canonical envelope shape)
fennec scorecard --project src/MyApp/MyApp.csproj --json
```

## Edge cases & troubleshooting

- `dotnet list package` failures (e.g. malformed project, restore required) surface the real
  stderr message and exit 1, instead of the misleading "No packages found in the project."
- A package with no discoverable source repository is reported with `status: "unavailable"`, not
  dropped from the results.
- An error during a specific package's lookup is reported with `status: "error"` and a structured
  `ArtifactError` (`code`, `message`, optional `target`/`details`) for that package only; other
  packages in the same run are unaffected.
