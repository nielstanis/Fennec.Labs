---
id: SPEC-fennec-dashboard-v1
companions:
  - ../../planning-artifacts/architecture/architecture-Fennec.Labs-2026-07-22/ARCHITECTURE-SPINE.md
sources:
  - ../../planning-artifacts/briefs/brief-Fennec.Labs-2026-07-22/brief.md
  - ../../planning-artifacts/briefs/brief-Fennec.Labs-2026-07-22/addendum.md
  - ../../planning-artifacts/research/technical-fennec-dashboard-shared-json-schema-storage-research-2026-07-22.md
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability only — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# Fennec Dashboard v1 (Project-Scoped) + Shared Artifact Contract

## Why

Fennec already generates rich dependency and scorecard data, but developers and security engineers currently consume it mostly via CLI output or raw JSON, which limits fast comprehension and cross-team sharing. This work captures a new opportunity: make the data immediately explorable through a project-scoped dashboard while establishing a contract that can power hosted aggregation later, without building two incompatible systems.

## Capabilities

- **CAP-1**
  - **intent:** A project user can generate canonical dashboard artifacts from dependency + scorecard analysis with one consistent contract.
  - **success:** Running the v1 producer flow on a .NET project emits artifacts that validate against the shared envelope schema and include top-level + transitive dependency data plus per-package scorecard results.

- **CAP-2**
  - **intent:** A project user can open a local dashboard view that renders transitive dependency tree and scorecard insights directly from generated artifacts.
  - **success:** Given a valid artifact set, the local dashboard displays dependency tree structure and scorecard/check details without requiring per-command custom parsers.

- **CAP-3**
  - **intent:** A team can explicitly publish commit-worthy artifact snapshots separately from runtime cache outputs.
  - **success:** A publish/export flow writes a curated artifact set (including manifest metadata) to a configured destination while leaving `.fennec/` as runtime cache.

- **CAP-4**
  - **intent:** Maintainers can evolve artifact contracts safely without silently breaking dashboard or automation consumers.
  - **success:** Contract CI checks enforce SemVer policy (`major` for breaking, `minor` for additive, `patch` for corrective) and reject incompatible changes without appropriate version bump.

- **CAP-5**
  - **intent:** The dashboard core can consume data through mode-specific adapters while preserving one domain model across local and future hosted modes.
  - **success:** The same dashboard core behavior is validated against `LocalArtifactDataSource` now and a hosted-compatible adapter contract later.

## Constraints

- Every dashboard-consumed artifact MUST use a canonical envelope with `$schema`, `schemaVersion`, `command`, `producedAt`, `producerVersion`, `sourceContext`, and `payload`.
- Canonical contracts MUST be owned in a dedicated shared project (reserved name: `FennecLabs.Contracts`), referenced by producers and consumers.
- V1 MUST remain flat-file-first for reads; no database runtime is required for core functionality.
- `.fennec/` remains gitignored runtime cache; commit-worthy artifacts require explicit publish/export.
- Dependency-tree payloads MUST normalize upstream `dotnet package list --include-transitive --format json` output, preferring fixed output versioning where available.
- Target runtime remains .NET 10 (`net10.0`) and JSON serialization remains `System.Text.Json`.

## Non-goals

- Building the hosted centralized multi-package dashboard service in v1.
- Shipping instrumentation, compare, or reproduce dashboard views in v1.
- Introducing a mandatory DB-backed local read model in v1.
- Shipping a Copilot-canvas-specific dashboard surface in v1.

## Success signal

- In a real project, a developer runs the v1 flow, opens the local dashboard, and can identify dependency posture (including transitive package scorecard signals) without manually parsing raw JSON.
- The same generated artifact contract is accepted by contract tests and is ready to be consumed by a future hosted adapter without schema redesign.

## Assumptions

- Teams adopting v1 can access local artifact folders from the dashboard runtime.
- Introducing new projects (`FennecLabs.Contracts`, `FennecLabs.Dashboard`) is acceptable within current repository structure.

## Open Questions

- What exact command/interface name should own artifact publish/export in CLI UX?
- What is the canonical destination and retention policy for published artifact snapshots in repositories that choose to commit them?
- What measurable thresholds (artifact volume, query latency, history depth) trigger escalation from flat files to an optional read-store?
