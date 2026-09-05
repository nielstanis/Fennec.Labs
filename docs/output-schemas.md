# Output artifacts & JSON schemas

Fennec.Labs writes structured, cacheable artifacts under the `--output` root (default `.fennec/`)
for most commands, and can emit the same data as JSON to stdout via `--json`.

## `.fennec/` layout

| Command | Path |
|---------|------|
| `instrument` (local file) | `<output>/instrument/` |
| `instrument` (NuGet) | `<output>/instrument/<packageId>/<resolvedVersion>/` |
| `compare` (`--nuget`) | `<output>/compare/<packageId>/<current>-vs-<previous>/result.json` |
| `compare` (`--file`) | not cached — stdout only |
| `reproduce` | `<output>/reproduce/<packageId>/<version>/result.json` |
| `scorecard` | `<output>/scorecard/<projectName>/<timestamp>/result.json` (+ `report.html`/`report.md`) |
| `dependencies` | `<output>/dependencies/<projectName>/<timestamp>/result.json` |
| `feeds` | none — feed config is stored separately from `.fennec/` |

Pass `--no-cache` to force a fresh run and overwrite the cached `result.json` for `compare`
(`--nuget` path) and `reproduce`.

## The canonical dashboard artifact envelope

`scorecard` and `dependencies` emit their `result.json` (and matching `--json` stdout) wrapped in
a single canonical envelope, so downstream tooling (dashboards, CI gates) can consume any command's
output with one deserialization path:

```jsonc
{
  "$schema": "fennec.envelope.v1",
  "schemaVersion": "1.0.0",
  "command": "dependencies",           // or "scorecard"
  "producedAt": "2026-09-05T12:34:56Z",
  "producerVersion": "0.7.5",
  "sourceContext": {
    "projectPath": "src/MyApp/MyApp.csproj",
    "workingDirectory": "/repo",
    "targetFramework": "net10.0",
    "gitCommit": "abc123..."            // optional
  },
  "payload": { /* command-specific — see below */ }
}
```

Schema identifiers follow `fennec.<command>.v{major}` for payloads and `fennec.envelope.v{major}`
for the envelope itself (see `FennecLabs.Contracts.SchemaIds`).

### `dependencies` payload — `DependencyGraphPayload`

```jsonc
{
  "targetFramework": "net10.0",
  "nodes": [
    {
      "id": "newtonsoft.json",          // lowercase invariant-culture package id
      "resolvedVersion": "13.0.3",
      "requestedVersion": "13.0.*",      // optional
      "isTopLevel": true
    }
  ]
}
```

### `scorecard` payload — `ScorecardGraphPayload`

```jsonc
{
  "targetFramework": "net10.0",
  "results": [
    {
      "packageId": "newtonsoft.json",   // matches DependencyNode.id for joining
      "packageVersion": "13.0.3",
      "status": "Available",             // "Available" | "Unavailable" | "Error"
      "score": 8.4,                       // present only when status == Available
      "repoName": "JamesNK/Newtonsoft.Json",
      "repoCommit": "...",
      "scorecardDate": "2026-08-01",
      "scorecardVersion": "4.13.0",
      "checks": [
        { "name": "Maintained", "score": 10, "reason": "..." }
      ],
      "error": null                        // ArtifactError, present when status != Available
    }
  ]
}
```

`ArtifactError` shape (used for both `scorecard` per-package errors and other commands):

```jsonc
{
  "code": "scorecard.unavailable",
  "message": "No repository could be located for this package.",
  "target": "some.package",            // optional
  "details": { "key": "value" }         // optional
}
```

## `instrument` and `compare` output

`instrument` and `compare` predate the canonical envelope and use command-specific shapes:

- `instrument` writes `fxt` (default) or `json` files per DLL analyzed, or a flat JSON array of
  invocation records (`type`, `method`, `parameters`, `invocation`, `returnType`, `sequence`) to
  stdout with `--json`.
- `compare` writes/returns a result with a typed `events` array (29 diff-event subtypes) plus
  derived `typesOnlyInAssembly1`/`typesOnlyInAssembly2` and `methodBodyChanges` views.

See [`commands/instrument.md`](commands/instrument.md) and [`commands/compare.md`](commands/compare.md)
for full details.
