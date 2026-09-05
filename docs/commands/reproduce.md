# `fennec reproduce`

Compares a locally built `.nupkg` file, or a build output directory of `.dll` files, against the
version published on NuGet.org — a reproducibility check to confirm your local build matches
what's actually published.

## When to use it

- Verifying a CI/release build produces byte-for-byte (or structurally identical) output to what
  was published.
- Diagnosing "it works locally but not from the published package" issues.
- Supply-chain verification: confirming a published artifact matches its claimed source build.

## Inputs

Exactly one of `--filename` or `--directory` is required.

| Option | Short | Required | Description |
|--------|-------|----------|-------------|
| `--filename <path>` | `-f` | one-of | Path to the local `.nupkg` file to compare. |
| `--directory <path>` | `-d` | one-of | Path to a build output directory of `.dll` files to compare instead of a `.nupkg`. |
| `--tfm <moniker>` | `-t` | no | Target framework moniker (e.g. `net8.0`); only valid with `--directory`. |
| `--nuget <id>` | `-n` | **yes** | NuGet package ID to compare against. |
| `--version <ver>` | `-v` | no | Version to compare against (optional; defaults to latest). |

Plus the [global options](../README.md#global-options): `--json`, `--output`, `--no-cache`.

### `--directory` TFM resolution

When using `--directory` without `--tfm`:
1. If the directory name itself looks like a TFM (e.g. `net8.0`), it's used directly.
2. If there's exactly one TFM subdirectory (e.g. `bin/Release/net8.0/`), it's auto-selected.
3. If multiple TFM subdirectories are found and the session is interactive, you'll be prompted
   to pick one via a selection list.
4. If multiple TFM subdirectories are found non-interactively, or in `--json` mode, or the TFM
   can't be identified at all (flat directory, no TFM hint), this is a **hard error** — no silent
   fallback.

Feed DLLs are filtered to `lib/{tfm}/` for accurate matching against the resolved TFM.

## Output

Cached to `<output>/reproduce/<packageId>/<version>/result.json`. Directory-mode JSON output
includes an additional `resolvedTfm` field showing which TFM was used.

## Examples

```bash
# Compare a local .nupkg against the published version
fennec reproduce --filename ./bin/Release/MyLib.1.0.0.nupkg --nuget MyLib

# Compare against a specific published version
fennec reproduce --filename ./bin/Release/MyLib.1.0.0.nupkg --nuget MyLib --version 1.0.0

# Compare a build output directory instead of a .nupkg
fennec reproduce --directory ./bin/Release/net8.0 --nuget MyLib

# Explicit TFM when the directory has multiple targets
fennec reproduce --directory ./bin/Release --tfm net8.0 --nuget MyLib
```

## Edge cases & troubleshooting

- `--tfm` supplied without `--directory` → `--tfm requires --directory.`, exits 1.
- Neither `--filename` nor `--directory` supplied → error and `--help` shown, exits 1.
- `--nuget` is always required regardless of which local input mode is used.
- Ambiguous/unresolvable TFM in non-interactive contexts is a hard error (see resolution rules
  above) rather than guessing.
