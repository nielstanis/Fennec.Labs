---
title: Fennec Dashboard v1
created: 2026-07-22
updated: 2026-07-22
status: draft
---

# PRD: Fennec Dashboard v1

## 0. Document Purpose

This PRD defines the implementation requirements for the project-scoped Fennec dashboard v1 and provides stable functional/non-functional requirement IDs for downstream planning (epics, stories, and implementation tasks). It is the primary requirements source for this phase and is aligned with `_bmad-output/specs/spec-fennec-dashboard-v1/SPEC.md` and `_bmad-output/planning-artifacts/architecture/architecture-Fennec.Labs-2026-07-22/ARCHITECTURE-SPINE.md`.

## 1. Vision

Fennec already produces useful dependency and scorecard analysis output, but most users currently inspect that output as JSON or CLI text. Dashboard v1 turns that data into a project-scoped visual workflow so developers and security engineers can inspect dependency posture quickly.

This first version focuses on one high-value slice: a transitive dependency tree with package-level Scorecard signals. It establishes a canonical artifact contract and read path that can later be reused by hosted mode without redesigning core models.

## 2. Target User

### 2.1 Jobs To Be Done

- As a project developer, quickly understand transitive dependency risk without manually parsing raw output.
- As an application security or platform engineer, identify risky packages and prioritize investigation.
- As a maintainer, evolve contracts safely so dashboard consumers do not break unexpectedly.

### 2.2 Non-Users (v1)

- Hosted multi-package portfolio viewers (deferred to later phase).

### 2.3 Key User Journeys

- **UJ-1. Developer generates canonical dashboard artifacts for a local project.**
  - Runs the dashboard artifact producer flow.
  - Receives a schema-valid artifact set for dependency + scorecard data.
  - Opens the local dashboard using those artifacts.
- **UJ-2. Security engineer inspects transitive dependency risk in local dashboard view.**
  - Opens the dependency tree view.
  - Expands transitive packages and reviews scorecard signals.
  - Identifies package(s) to investigate without raw JSON inspection.
- **UJ-3. Maintainer publishes commit-worthy artifacts distinct from cache output.**
  - Runs explicit publish/export flow.
  - Produces curated snapshot + manifest metadata for repository-friendly tracking.

## 3. Glossary

- **Canonical Envelope** — Shared top-level artifact shape containing schema and provenance metadata.
- **Payload** — Command-specific structured data nested inside Canonical Envelope.
- **Runtime Cache** — Ephemeral output under `.fennec/`, not treated as commit-worthy history.
- **Published Artifact Set** — Curated snapshot produced by explicit export/publish flow.
- **Local Artifact Data Source** — Dashboard adapter that reads canonical artifacts from filesystem.

## 4. Features

### 4.1 Canonical Artifact Production

**Description:** Project users generate dashboard-consumable artifacts through one consistent contract that includes dependency tree and scorecard results. Realizes UJ-1.

**Functional Requirements:**

#### FR-1: Produce canonical dashboard artifact envelope

A project user can run the v1 producer flow and obtain artifacts wrapped in a canonical envelope with required metadata fields. Realizes UJ-1.

**Consequences (testable):**
- Artifact includes `$schema`, `schemaVersion`, `command`, `producedAt`, `producerVersion`, `sourceContext`, and `payload`.
- Artifacts validate against shared contract definitions.

#### FR-2: Include transitive dependency + scorecard payload data

A project user can generate artifacts that include top-level and transitive dependency graph data and package-level scorecard outputs. Realizes UJ-1.

**Consequences (testable):**
- Payload includes dependency tree relationships sufficient for tree rendering.
- Payload includes package scorecard/check details aligned to dependency nodes.

### 4.2 Local Dashboard Consumption

**Description:** Project users open local dashboard views that read canonical artifacts without command-specific parser forks. Realizes UJ-2.

**Functional Requirements:**

#### FR-3: Render local transitive dependency tree view

A project user can open the local dashboard and inspect the full transitive dependency hierarchy from canonical artifacts. Realizes UJ-2.

**Consequences (testable):**
- Dashboard renders hierarchical dependency relationships from artifact payload.
- User can navigate between top-level and transitive package nodes.

#### FR-4: Render scorecard insights for dependency packages

A project user can view scorecard/check details for packages represented in the dependency tree. Realizes UJ-2.

**Consequences (testable):**
- Scorecard data is shown for relevant packages with clear package association.
- Missing scorecard data states are surfaced explicitly rather than silently omitted.

### 4.3 Artifact Lifecycle and Compatibility

**Description:** Teams explicitly publish durable artifact snapshots and evolve contracts safely for future consumers. Realizes UJ-3.

**Functional Requirements:**

#### FR-5: Publish/export curated artifact snapshots

A maintainer can execute an explicit publish/export flow that writes commit-worthy artifact snapshots separately from runtime cache. Realizes UJ-3.

**Consequences (testable):**
- Publish/export writes artifacts to a configured curated destination.
- Snapshot includes manifest metadata describing provenance/context.

#### FR-6: Enforce contract versioning and compatibility policy

A maintainer can evolve contracts under explicit SemVer governance with CI guardrails that block incompatible changes without proper version bump. Realizes UJ-3.

**Consequences (testable):**
- CI fails when breaking schema changes do not include major version bump.
- Additive/corrective changes follow minor/patch policy.

#### FR-7: Preserve mode-agnostic dashboard core via adapter contracts

A maintainer can keep one dashboard domain core while swapping data source adapters for local and future hosted modes. Realizes UJ-1, UJ-2.

**Consequences (testable):**
- Core dashboard behavior validates against local adapter.
- Hosted-compatible adapter contract can be introduced without changing core model semantics.

## 5. Cross-Cutting Non-Functional Requirements

- **NFR-1 (Contract Integrity):** All dashboard-consumed artifacts must validate against shared canonical schema definitions.
- **NFR-2 (Evolution Safety):** Schema changes must follow SemVer governance and be enforced in CI.
- **NFR-3 (Storage Simplicity v1):** v1 read path must remain flat-file-first; no database is required for core dashboard behavior.
- **NFR-4 (Boundary Clarity):** `.fennec/` remains gitignored runtime cache; durable snapshots require explicit publish/export.
- **NFR-5 (Runtime and Serialization):** Implementation remains on .NET 10 (`net10.0`) and `System.Text.Json`.
- **NFR-6 (Upstream Normalization):** Dependency ingestion normalizes `dotnet package list --include-transitive --format json` and prefers fixed upstream output versioning where available.

## 6. Non-Goals (Explicit)

- Building hosted multi-package dashboard service in v1.
- Shipping instrumentation/compare/reproduce dashboard views in v1.
- Introducing mandatory DB-backed local read model in v1.
- Shipping Copilot canvas-specific dashboard UI in v1.

## 7. MVP Scope

### 7.1 In Scope

- Project-scoped dashboard v1.
- Canonical artifact contract for dashboard-consumed data.
- Transitive dependency tree + scorecard local dashboard view.
- Explicit publish/export path for curated snapshots.
- Contract versioning governance + compatibility enforcement.

### 7.2 Out of Scope for MVP

- Hosted ingestion/API/auth model.
- Multi-tenant operational concerns.
- Non-scorecard dashboard views.
- Optional read-store/database optimization track.

## 8. Success Metrics

**Primary**
- **SM-1:** Users can identify risky transitive dependencies from the dashboard without manual raw JSON parsing. Validates FR-3, FR-4.
- **SM-2:** Producer flow emits schema-valid artifacts consistently across target projects. Validates FR-1, FR-2.

**Secondary**
- **SM-3:** Teams can store curated artifact snapshots without polluting runtime cache paths. Validates FR-5.
- **SM-4:** Contract CI catches incompatible changes before merge. Validates FR-6.

## 9. Open Questions

1. What final CLI command name should own publish/export?
2. What exact destination and retention policy should be default for published snapshots?
3. Which measurable thresholds should trigger optional read-store introduction?

## 10. Assumptions Index

- `[ASSUMPTION]` Access to project-local artifact folders is available to dashboard runtime.
- `[ASSUMPTION]` New shared projects for contracts/dashboard are acceptable in repository structure.
