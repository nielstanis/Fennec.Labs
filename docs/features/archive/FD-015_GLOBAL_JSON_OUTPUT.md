# FD-015: Global JSON Output — All Commands

**Status:** Complete
**Completed:** 2026-05-14
**Priority:** High
**Effort:** Medium (2–4 hours)
**Impact:** Every command emits structured JSON to stdout when `--format json` is passed.
Defines the shared JSON schema that FD-013 (MCP server) will reuse — validates the contract
before the MCP project is built.

## Problem

No command except `instrument` supports machine-readable output, and even there `--format json`
writes to a file rather than stdout. Scripting, CI pipelines, and the upcoming MCP server
(FD-013) all need structured data on stdout. Progress chatter and result data are currently
interleaved on the same stream, making output unparseable.

## Solution

### Global `--format` option

Add `--format` as a root-command option with two values:

| Value | Behaviour |
|-------|-----------|
| `human` (default) | Rich Spectre.Console output (FD-014) or plain text when not a TTY |
| `json` | Single JSON object on stdout; all progress/status messages suppressed on stdout; errors go to stderr |

Wire this into all five command handlers. Pass the resolved `OutputMode` enum (not a raw string)
down to each handler so the branch is explicit.

### Rename instrument's file-format flag

The instrument command's existing `--format fxt|json` controls the *file* output format, which
conflicts with the new global `--format`. Rename it:

- Old: `--format fxt|json` (instrument only)
- New: `--file-format fxt|json` (instrument only, default: `fxt`)

Update help text and `ParseFormat` in `InstrumentCommandHandler`.

### stdout / stderr split in JSON mode

In `json` mode:
- **stdout** — one JSON document, written once at the end of the command
- **stderr** — errors only (existing `Console.Error.WriteLine` calls stay as-is)
- **suppressed** — all progress messages (`Downloading…`, `Found N DLL(s)…`, spinner output)

In `human` mode behaviour is unchanged (FD-014 styling applies).

### JSON schema (shared with FD-013)

All shapes are serialized from the existing library-layer result types. No new domain types;
only the CLI serialization path changes.

#### `fennec scorecard`
```json
{
  "packages": [
    {
      "packageId": "Polly",
      "packageVersion": "8.4.1",
      "score": 8.4,
      "checks": [{ "name": "Code-Review", "score": 10, "reason": "..." }],
      "error": null
    }
  ]
}
```

#### `fennec compare` / `fennec reproduce`
```json
{
  "packageId": "Polly",
  "currentVersion": "8.4.1",
  "previousVersion": "8.3.0",
  "perDll": [
    {
      "dllPath": "lib/net8.0/Polly.dll",
      "areEqual": true,
      "differences": [],
      "typesAdded": [],
      "typesRemoved": [],
      "methodBodyDifferences": []
    }
  ],
  "summary": { "identical": 1, "different": 0, "errors": 0 }
}
```

`reproduce` adds two extra fields at the top level:
```json
"onlyInLocal": ["lib/net8.0/Extra.dll"],
"onlyInFeed":  []
```

#### `fennec feeds list`
```json
{
  "feeds": [
    { "name": "nuget.org", "source": "https://api.nuget.org/v3/index.json", "isDefault": true }
  ]
}
```

#### `fennec instrument` (stdout mode, replaces file write)
When `--format json` is active, instrument prints the flat invocation array to stdout instead
of writing to disk. `--file-format` is ignored when `--format json` is set.
```json
[
  {
    "type": "MyNamespace.MyClass",
    "method": "DoWork",
    "parameters": "string input",
    "invocation": "System.IO.File.ReadAllText",
    "returnType": "string",
    "sequence": 0
  }
]
```

### OutputMode enum

Add to `FennecLabs.Cli`:

```csharp
public enum OutputMode { Human, Json }
```

Each handler receives `OutputMode outputMode` and branches:

```csharp
if (outputMode == OutputMode.Json)
    Console.WriteLine(JsonSerializer.Serialize(result, JsonOptions.Default));
else
    renderer.Render(result); // Spectre.Console path (FD-014)
```

`JsonOptions.Default` uses `camelCase` naming and `JsonIgnoreCondition.WhenWritingNull`.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/OutputMode.cs` | CREATE | `enum OutputMode { Human, Json }` |
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Add `--format` root option; rename instrument's flag to `--file-format`; pass `OutputMode` to all handlers |
| `src/FennecLabs.Cli/Commands/ScorecardCommandHandler.cs` | MODIFY | JSON branch → scorecard schema above |
| `src/FennecLabs.Cli/Commands/CompareCommandHandler.cs` | MODIFY | JSON branch → compare schema; suppress progress |
| `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` | MODIFY | JSON branch → reproduce schema; suppress progress |
| `src/FennecLabs.Cli/Commands/FeedsCommandHandler.cs` | MODIFY | JSON branch → feeds schema |
| `src/FennecLabs.Cli/Commands/InstrumentCommandHandler.cs` | MODIFY | JSON branch → stdout array; rename `--file-format` |

## Verification

1. `dotnet build` — 0 errors, 0 warnings.
2. `fennec scorecard -p <project> --format json | jq .packages[0].score` — returns a number.
3. `fennec compare --nuget Polly --format json | jq .summary` — returns `{identical, different, errors}`.
4. `fennec feeds list --format json | jq .feeds` — returns array.
5. `fennec instrument --nuget Polly --format json | jq length` — returns invocation count as a number.
6. `fennec instrument --filename foo.dll --file-format fxt` — still writes `.fxt` file (renamed flag works).
7. `fennec scorecard -p <project> --format json 2>/dev/null` — stdout is valid JSON with no progress text mixed in.
8. Existing human-mode output is unchanged (FD-014 or plain text).

## Related

- FD-013 — MCP server will reuse the JSON shapes defined here; implement FD-015 first to validate the schema
- FD-014 — Human output path; `OutputMode.Human` routes to Spectre renderers
- FD-006 — Spectre.Console dependency (needed by FD-014, not this FD directly)
