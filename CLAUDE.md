# FennecLabs

`Fennec.Labs` is a .NET 10 global CLI tool (command: `fennec`) for analysing .NET projects — assembly diffing, NuGet inspection, IL instrumentation, OpenSSF Scorecard checks, and NuGet feed management.

---

## Application

### Project layout

```
src/
  FennecLabs.Cli/             # CLI entry point; published as Fennec.Labs dotnet tool
  FennecLabs.AssemblyDiff/    # Mono.Cecil-based assembly comparison engine
  FennecLabs.Instrumentation/ # IL method invocation extractor
  FennecLabs.NuGet/           # NuGet feed management and package download
  FennecLabs.Scorecard/       # OpenSSF Scorecard HTTP client
  FennecLabs.DotNetCli/       # dotnet CLI wrapper

test/
  FennecLabs.*.Tests/         # One test project per src library
  FennecLabs.TestUtilities/   # Shared helpers and TestProjectRefs.targets
  TestProjects/               # Real .csproj fixtures used by integration tests
```

No `.sln` file — reference individual projects with `-p`.

### Commands

| Command | What it does |
|---------|-------------|
| `fennec instrument` | Extract IL method invocations from a local `.dll` or NuGet package |
| `fennec compare` | Diff assemblies between two NuGet versions or two local `.dll`/`.nupkg` files |
| `fennec reproduce` | Compare a local `.nupkg` or build output directory against the published NuGet feed version |
| `fennec scorecard` | Fetch OpenSSF Scorecard for all deps in a `.csproj` (direct + transitive) |
| `fennec feeds` | Manage NuGet feed sources (`list` / `add` / `remove`) |

Global options on every command: `--json`/`-j`, `--output`/`-o` (default `.fennec`), `--no-cache`/`-C`.

### Build and test

```bash
dotnet build                                          # build everything
dotnet test                                           # run all tests
dotnet test test/FennecLabs.Cli.Tests/               # single project
dotnet run --project src/FennecLabs.Cli -- <args>    # run from source
```

### Conventions

- Output files land under `.fennec/` (gitignored); subfolders per command (`instrument/`, `scorecard/`, etc.)
- Results are cached by package + version; `--no-cache` bypasses the cache
- `InternalsVisibleTo("FennecLabs.Cli.Tests")` is set on `Fennec.csproj` — internal members are directly testable
- Live/network tests are tagged `Category=Live`; exclude from offline runs with `--filter "Category!=Live"`
- `reproduce --directory` supports TFM auto-derivation from directory name, single-subdir auto-select, and interactive `SelectionPrompt` when multiple TFM subdirs are found

---

## Feature Design (FD) Management

Features are tracked in `docs/features/`. Each FD has a dedicated file (`FD-XXX_TITLE.md`) and is indexed in `FEATURE_INDEX.md`.

### FD Lifecycle

| Stage | Description |
|-------|-------------|
| **Planned** | Identified but not yet designed |
| **Design** | Actively designing (exploring code, writing plan) |
| **Open** | Designed and ready for implementation |
| **In Progress** | Currently being implemented |
| **Pending Verification** | Code complete, awaiting verification |
| **Complete** | Verified working, ready to archive |
| **Deferred** | Postponed (low priority or blocked) |
| **Closed** | Won't implement (superseded or not needed) |

### Slash Commands

| Command | Purpose |
|---------|---------|
| `/fd-new` | Create a new feature design |
| `/fd-explore` | Explore project - overview, FD history, recent activity |
| `/fd-deep` | Deep parallel analysis — 4 agents explore a hard problem from different angles, verify claims, synthesize |
| `/fd-status` | Show active FDs with status and grooming |
| `/fd-verify` | Post-implementation: commit, proofread, verify |
| `/fd-close` | Complete/close an FD, archive file, update index, update changelog |

### Conventions

- **FD files**: `docs/features/FD-XXX_TITLE.md` (XXX = zero-padded number)
- **Commit format**: `FD-XXX: Brief description`
- **Numbering**: Next number = highest across all index sections + 1
- **Source of truth**: FD file status > index (if discrepancy, file wins)
- **Archive**: Completed FDs move to `docs/features/archive/`

### Managing the Index

The `FEATURE_INDEX.md` file has four sections:

1. **Active Features** — All non-complete FDs, sorted by FD number
2. **Completed** — Completed FDs, newest first
3. **Deferred / Closed** — Items that won't be done
4. **Backlog** — Low-priority or blocked items parked for later

### Inline Annotations (`%%`)

Lines starting with `%%` in any file are **inline annotations from the user**. When you encounter them:
- Treat each `%%` annotation as a direct instruction — answer questions, develop further, provide feedback, or make changes as requested
- Address **every** `%%` annotation in the file; do not skip any
- After acting on an annotation, remove the `%%` line from the file
- If an annotation is ambiguous, ask for clarification before acting

This enables a precise review workflow: the engineer annotates FD files or plan docs directly in the editor, then asks Claude to address all annotations — tighter than conversational back-and-forth for complex designs.

### Changelog

- **Format**: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) with [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
- **Updated by**: `/fd-close` (complete disposition only) adds entries under `[Unreleased]`
- **FD references**: Entries end with `(FD-XXX)` for traceability
- **Subsections**: Added, Changed, Fixed, Removed
- **Releasing**: Rename `[Unreleased]` to `[X.Y.Z] - YYYY-MM-DD`, add fresh `[Unreleased]` header
