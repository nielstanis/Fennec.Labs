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
| `fennec dependencies` | Emit a normalized, canonical dependency graph artifact for a `.csproj` |
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

## Feature Design (FD) Management — Legacy / Archived

FD-001 through FD-034 were tracked in `docs/features/` (`FD-XXX_TITLE.md`, indexed in `FEATURE_INDEX.md`). **This process is retired for new work** — planning now goes through BMAD-METHOD (see below). `docs/features/` is kept as-is for historical reference only; do not create new `FD-XXX` files or run `/fd-*` slash commands for new features. Existing content there (lifecycle stages, `%%` inline annotations, changelog conventions) still applies if closing out something already in flight.

## Planning & Feature Management (BMAD-METHOD)

This project uses [BMAD-METHOD](https://docs.bmad-method.org) (`bmm` module, v6) for planning and implementation of new features — including the hosted dashboard initiative. It's installed under `_bmad/` and its skills/agents are committed to the repo like any other project tooling.

### Where things live

| Path | Purpose |
|------|---------|
| `_bmad/` | Installed BMAD core + `bmm` module, config (`config.toml`, `config.user.toml`) |
| `_bmad-output/planning-artifacts/` | PRDs, architecture docs, epics/stories — committed, living documentation |
| `_bmad-output/implementation-artifacts/` | Dev-loop outputs (story context, review notes) |
| `.agents/skills/` | 46 BMAD skills (agent personas + workflows), invoked via the `skill` tool |
| `.github/agents/` | BMAD agent personas exposed as custom agents (Analyst, PM, Architect, Dev, UX Designer, Tech Writer) |

### Getting started / key skills

- `bmad-help` — ask anytime for guidance on what to do next
- `bmad-agent-analyst` / `bmad-agent-pm` / `bmad-agent-architect` / `bmad-agent-ux-designer` / `bmad-agent-dev` / `bmad-agent-tech-writer` — domain-expert personas
- `bmad-create-prd`, `bmad-create-architecture`, `bmad-create-epics-and-stories` — planning workflows
- `bmad-dev-story`, `bmad-dev-auto`, `bmad-code-review` — implementation workflows
- `bmad-party-mode` — bring multiple personas into one session to collaborate

### Conventions

- Run `dotnet build` / `dotnet test` as usual for validation — BMAD governs planning/process, not the build.
- Keep hosted (multi-package) and project-scoped (single `.csproj`) concerns explicit in PRDs/architecture docs — this repo's dashboard work needs shared data structures/storage that both modes can reuse; capture that design in the architecture doc before implementation starts.

### Inline Annotations (`%%`)

Lines starting with `%%` in any file are **inline annotations from the user**. When you encounter them:
- Treat each `%%` annotation as a direct instruction — answer questions, develop further, provide feedback, or make changes as requested
- Address **every** `%%` annotation in the file; do not skip any
- After acting on an annotation, remove the `%%` line from the file
- If an annotation is ambiguous, ask for clarification before acting

This enables a precise review workflow: the engineer annotates FD/PRD/architecture files directly in the editor, then asks Claude to address all annotations — tighter than conversational back-and-forth for complex designs.

### Changelog

- **Format**: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) with [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
- **Updated by**: completed BMAD stories/epics (or legacy `/fd-close`) add entries under `[Unreleased]`
- **Traceability**: Entries end with `(FD-XXX)` for legacy items, or the relevant epic/story ID for BMAD-tracked work
- **Subsections**: Added, Changed, Fixed, Removed
- **Releasing**: Rename `[Unreleased]` to `[X.Y.Z] - YYYY-MM-DD`, add fresh `[Unreleased]` header
