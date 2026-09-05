# `fennec compare`

Compares assemblies structurally (types, members, IL bodies, attributes, security declarations,
etc.) between two NuGet package versions, or between two local `.dll`/`.nupkg` files directly —
no NuGet feed required for the local-file path.

## When to use it

- You want to see exactly what changed between two versions of a dependency before upgrading.
- You're validating that a build produces the expected assembly-level changes.
- You need a full structural diff (not just a version bump) for security or compliance review.

## Inputs

Exactly one of `--nuget` or `--file` is required; they are mutually exclusive.

| Option | Short | Description |
|--------|-------|-------------|
| `--nuget <id>` | `-n` | NuGet package ID to compare. |
| `--version <ver>` | `-v` | Version to compare against latest (optional; if omitted, compares the two most recent published versions). |
| `--file <a> <b>` | `-f` | Two local `.dll` or `.nupkg` files to compare directly (exactly two values). |

Plus the [global options](../README.md#global-options): `--json`, `--output`, `--no-cache`.
Note: `--output`/`--no-cache` only apply to the `--nuget` path; `--file` comparisons are not cached.

## Output

- **`--nuget` path**: cached to `<output>/compare/<packageId>/<current>-vs-<previous>/result.json`.
  Re-running the same comparison serves the cached result unless `--no-cache` is passed.
- **`--file` path**: not cached; result is written to stdout (human table or JSON, per `--json`).
- The result contains a typed `events` list (29 diff-event subtypes: assembly, type, method,
  field, property, event, nested-type, P/Invoke, security declaration diffs, etc.) plus derived
  views `typesOnlyInAssembly1`/`typesOnlyInAssembly2` and `methodBodyChanges`.

## Examples

```bash
# Compare the latest two published versions of a NuGet package
fennec compare --nuget Newtonsoft.Json

# Compare a specific version against latest
fennec compare --nuget Newtonsoft.Json --version 12.0.3

# Compare two local files directly (no NuGet lookup)
fennec compare --file old.dll new.dll
fennec compare --file old.nupkg new.nupkg

# JSON output
fennec compare --nuget Newtonsoft.Json --json

# Force a fresh comparison, bypassing the cache
fennec compare --nuget Newtonsoft.Json --no-cache
```

## Edge cases & troubleshooting

- Both `--nuget` and `--file` supplied → `--file and --nuget are mutually exclusive.`, exits 1.
- Neither supplied → error and `--help` shown, exits 1.
- `--file` requires exactly two paths; System.CommandLine rejects any other count before the
  handler runs.
