# `fennec instrument`

Extracts IL-level method invocation data from a `.dll` assembly, either given directly or
downloaded from NuGet. Useful for understanding what a library actually calls at runtime
(e.g. reflection, P/Invoke, delegate construction) beyond what's visible in its public API.

## When to use it

- You want a machine-readable inventory of every method invocation inside an assembly.
- You're auditing a third-party package for suspicious API usage (network, file, process calls).
- You need input data for downstream tooling (e.g. call-graph analysis) in `fxt` or `json` form.

## Inputs

Exactly one of `--filename` or `--nuget` is required.

| Option | Short | Description |
|--------|-------|-------------|
| `--filename <path>` | `-f` | Path to a local assembly file (`.dll`) to instrument. |
| `--nuget <id>` | `-n` | NuGet package ID to download and instrument. All library DLLs inside the package are analyzed. |
| `--version <ver>` | `-v` | Package version to use with `--nuget` (optional; defaults to latest). |
| `--file-format <fmt>` | `-F` | Output file format: `fxt` (default) or `json`. |

Plus the [global options](../README.md#global-options): `--json`, `--output`, `--no-cache`.

## Output

- **Local file mode**: writes to `<output>/instrument/` using the writer for `--file-format`.
- **NuGet mode**: writes to `<output>/instrument/<packageId>/<resolvedVersion>/`, one output file
  per DLL found in the package (only files under `lib/` are analyzed).
- **`--json`**: prints a flat JSON array of invocations to stdout instead of writing files; no
  files are created in this mode.

Each invocation record contains: `type`, `method`, `parameters`, `invocation`, `returnType`,
`sequence`.

## Examples

```bash
# Instrument a local assembly, default fxt output
fennec instrument --filename path/to/MyLib.dll

# Instrument a NuGet package (latest version), all DLLs inside it
fennec instrument --nuget Newtonsoft.Json

# Specific version, JSON file output instead of fxt
fennec instrument --nuget Newtonsoft.Json --version 13.0.3 --file-format json

# JSON to stdout for scripting/piping
fennec instrument --filename path/to/MyLib.dll --json
```

## Edge cases & troubleshooting

- Neither `--filename` nor `--nuget` supplied → command prints an error and shows `--help`, exits 1.
- `--filename` path does not exist → `Assembly file not found: <path>`, exits 1.
- NuGet package has no library DLLs (e.g. metapackage) → `No DLL files found in the package.`, exits 0.
- Analysis failures on individual assemblies/DLLs are reported per-file and don't stop processing
  of the remaining DLLs in a package; the command exits 1 if any DLL failed.
