---
stepsCompleted:
  - step-01-requirements-extracted
  - step-02-epics-approved
  - step-03-stories-generated
inputDocuments:
  - _bmad-output/planning-artifacts/PRD.md
  - _bmad-output/planning-artifacts/architecture/architecture-Fennec.Labs-2026-07-22/ARCHITECTURE-SPINE.md
  - _bmad-output/specs/spec-fennec-dashboard-v1/SPEC.md
  - _bmad-output/planning-artifacts/ux-designs/ux-fennec-dashboard-v1-2026-07-22/DESIGN.md
  - _bmad-output/planning-artifacts/ux-designs/ux-fennec-dashboard-v1-2026-07-22/EXPERIENCE.md
---

# Fennec.Labs - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for Fennec.Labs, decomposing the requirements from the PRD, UX Design if it exists, and Architecture requirements into implementable stories.

## Requirements Inventory

### Functional Requirements

FR1: Project user can run the v1 producer flow and obtain artifacts wrapped in a canonical envelope with required metadata fields.
FR2: Project user can generate artifacts that include top-level and transitive dependency graph data and package-level scorecard outputs.
FR3: Project user can open the local dashboard and inspect full transitive dependency hierarchy from canonical artifacts.
FR4: Project user can view scorecard/check details for packages represented in the dependency tree, including explicit missing-data states.
FR5: Maintainer can execute an explicit publish/export flow that writes curated commit-worthy artifact snapshots separate from runtime cache.
FR6: Maintainer can evolve contracts under SemVer governance with CI guardrails that reject incompatible changes without proper version bumps.
FR7: Maintainer can preserve a mode-agnostic dashboard core by consuming data through adapter contracts (local now, hosted-compatible later).

### NonFunctional Requirements

NFR1: All dashboard-consumed artifacts must validate against shared canonical schema definitions.
NFR2: Schema evolution must follow SemVer policy and be enforced in CI.
NFR3: v1 read path must remain flat-file-first; no database is required for core dashboard behavior.
NFR4: `.fennec/` remains gitignored runtime cache; durable snapshots require explicit publish/export.
NFR5: Target runtime remains .NET 10 (`net10.0`) with `System.Text.Json`.
NFR6: Dependency ingestion normalizes `dotnet package list --include-transitive --format json` and prefers fixed upstream output versioning when available.

### Additional Requirements

- No starter template requirement was specified in Architecture; Epic 1 Story 1 should focus on shared contract/project scaffolding instead.
- Canonical contracts must be owned in a dedicated shared project (`FennecLabs.Contracts`) and referenced by CLI producers and dashboard consumers.
- Canonical envelope metadata fields are mandatory in all dashboard-consumed artifacts; command-specific data stays in payload.
- Artifact lifecycle must enforce cache vs published boundary, including manifest metadata for curated snapshots.
- Dashboard core must remain mode-agnostic with adapter interfaces (`*DataSource`) and a required local adapter implementation for v1.
- Artifacts are immutable once written; regeneration creates new artifact instances.
- Error payloads must be structured typed objects (no plain string-only failure shapes).
- New project structure should support `FennecLabs.Contracts` and `FennecLabs.Dashboard` as first-class implementation units.

### UX Design Requirements

UX-DR1: Implement shared design tokens for color, spacing, and typography, and reference them from all dashboard components.
UX-DR2: Implement `SummaryKpiCard` to present package count, critical/unknown risk counts, and artifact generated timestamp.
UX-DR3: Implement `DependencyTreePanel` + `DependencyTreeNode` with expand/collapse interactions and severity indicators.
UX-DR4: Implement `ScorecardDetailPanel` bound to selected tree node with explicit handling for unavailable scorecard data.
UX-DR5: Implement `FilterBar` with package search, risk threshold filter, and transitive-only toggle; preserve expanded paths where possible.
UX-DR6: Implement `ProvenanceBanner` that surfaces schemaVersion, producerVersion, source context, and artifact timestamp.
UX-DR7: Implement explicit loading, empty, partial-data, and structured error states using `StateMessage`.
UX-DR8: Support responsive layout behavior for desktop (3-region view), tablet (collapsible details), and mobile (stacked/drill-in flow).
UX-DR9: Meet accessibility baseline: keyboard navigation for tree interactions, ARIA expanded/collapsed semantics, visible focus states, and contrast targets.
UX-DR10: Provide non-color risk indicators and screen-reader text that includes package name + severity meaning.

### FR Coverage Map

FR1: Epic 1 - Canonical envelope production for dashboard artifacts.
FR2: Epic 1 - Dependency + scorecard payload production in canonical artifacts.
FR3: Epic 2 - Local dashboard dependency tree rendering from canonical artifacts.
FR4: Epic 2 - Package-level scorecard insight rendering and missing-data handling.
FR5: Epic 3 - Explicit publish/export flow for curated artifact snapshots.
FR6: Epic 3 - Contract SemVer governance and CI compatibility enforcement.
FR7: Epic 2 - Mode-agnostic dashboard core via adapter contract and local data source.

## Epic List

### Epic 1: Generate Trusted Dashboard Artifacts
Project users can generate canonical, schema-valid dependency and scorecard artifacts through one consistent producer flow.
**FRs covered:** FR1, FR2

### Epic 2: Explore Dependency Risk in Local Dashboard
Users can open a local dashboard to inspect transitive dependency posture and package scorecard signals with accessible, responsive interactions.
**FRs covered:** FR3, FR4, FR7

### Epic 3: Publish Durable Snapshots and Enforce Contract Evolution
Maintainers can publish curated artifact snapshots and safely evolve artifact contracts under CI-enforced versioning rules.
**FRs covered:** FR5, FR6

## Epic 1: Generate Trusted Dashboard Artifacts

Project users can generate canonical, schema-valid dependency and scorecard artifacts through one consistent producer flow.

### Story 1.1: Establish canonical contract package

As a maintainer,
I want a dedicated contracts project with canonical envelope and payload schema definitions,
So that all producers and consumers share one source of truth.

**Implements:** FR1

**Acceptance Criteria:**

**Given** the repository builds on .NET 10
**When** the shared contracts foundation is added
**Then** a dedicated `FennecLabs.Contracts` project defines canonical envelope contracts including `$schema`, `schemaVersion`, `command`, `producedAt`, `producerVersion`, `sourceContext`, and `payload`
**And** contract types/schemas are referenceable by CLI producers and dashboard consumers without duplicating models

### Story 1.2: Normalize and emit dependency graph artifacts

As a project user,
I want normalized transitive dependency artifacts emitted in the canonical envelope,
So that dashboard consumers can rely on stable graph semantics.

**Implements:** FR2

**Acceptance Criteria:**

**Given** a project dependency tree source from `dotnet package list --include-transitive --format json`
**When** the dependency artifact is produced
**Then** upstream output is normalized into a canonical payload shape compatible with the shared contract
**And** emitted artifacts are serialized with canonical envelope metadata and stable package identity fields

### Story 1.3: Emit scorecard artifacts linked to dependency nodes

As a project user,
I want package scorecard results emitted in canonical payloads linked to dependency identities,
So that risk signals map directly to dependency tree nodes.

**Implements:** FR2

**Acceptance Criteria:**

**Given** scorecard analysis results for packages in the dependency graph
**When** scorecard artifacts are produced
**Then** package scorecard/check details are emitted in canonical payloads keyed by normalized package identity
**And** missing scorecard data is represented explicitly in structured payload state rather than being silently omitted

### Story 1.4: Enforce producer-side schema validity

As a maintainer,
I want automated producer validation tests for generated artifacts,
So that invalid schema output is blocked before release.

**Implements:** FR1

**Acceptance Criteria:**

**Given** canonical artifact generation for dependency and scorecard flows
**When** validation tests execute
**Then** generated artifacts are checked against shared contract expectations and fail fast on schema incompatibility
**And** regression coverage prevents producers from shipping envelope/payload breaking changes unnoticed

## Epic 2: Explore Dependency Risk in Local Dashboard

Users can open a local dashboard to inspect transitive dependency posture and package scorecard signals with accessible, responsive interactions.

### Story 2.1: Build dashboard shell and state framework

As a project user,
I want a dashboard shell with provenance banner, summary KPI cards, and consistent loading/empty/error states,
So that I can trust artifact context and understand system status immediately.

**Implements:** FR3
**UX Coverage:** UX-DR1, UX-DR2, UX-DR6, UX-DR7

**Acceptance Criteria:**

**Given** a valid local artifact set path
**When** the dashboard opens
**Then** the shell renders `ProvenanceBanner`, summary KPI cards, and standard `StateMessage` patterns for loading, empty, partial, and error states
**And** provenance information surfaces schema and producer metadata without requiring raw JSON inspection

### Story 2.2: Implement local artifact data source adapter

As a maintainer,
I want a `LocalArtifactDataSource` that maps canonical artifacts into one dashboard domain model,
So that dashboard core remains mode-agnostic and hosted-compatible.

**Implements:** FR7

**Acceptance Criteria:**

**Given** canonical dependency and scorecard artifacts
**When** the local adapter reads artifacts from filesystem storage
**Then** it materializes a unified dashboard domain model with deterministic package identities and relationships
**And** dashboard core interfaces consume the adapter contract without local-mode-specific branching logic

### Story 2.3: Render interactive transitive dependency tree

As a project user,
I want to expand/collapse and filter the transitive dependency tree,
So that I can quickly isolate risky package paths.

**Implements:** FR3, FR4
**UX Coverage:** UX-DR3, UX-DR5

**Acceptance Criteria:**

**Given** a populated dashboard domain model
**When** I interact with the tree and filters (package search, risk threshold, transitive-only)
**Then** the dependency tree updates predictably with preserved navigation context where possible
**And** each node displays package identity plus risk indicator using shared visual token mappings

### Story 2.4: Render package scorecard detail panel

As a project user,
I want package scorecard details tied to selected tree nodes,
So that I can evaluate risk rationale per dependency.

**Implements:** FR4
**UX Coverage:** UX-DR4

**Acceptance Criteria:**

**Given** a selected dependency node in the tree
**When** the details panel loads scorecard data
**Then** it shows package-scoped scorecard/check signals with clear association to the selected node
**And** unavailable scorecard data is rendered as an explicit, actionable state instead of disappearing silently

### Story 2.5: Deliver accessibility and responsive behavior baseline

As a project user,
I want keyboard/screen-reader accessible and responsive interactions,
So that the dashboard works reliably across devices and assistive workflows.

**Implements:** FR3, FR4
**UX Coverage:** UX-DR8, UX-DR9, UX-DR10

**Acceptance Criteria:**

**Given** dashboard interactions across supported viewport sizes
**When** users navigate by keyboard or assistive technology
**Then** tree controls expose ARIA expanded/collapsed semantics, visible focus states, and non-color severity cues
**And** layout behavior adapts for desktop, tablet, and mobile according to the UX contract without losing core task flow

## Epic 3: Publish Durable Snapshots and Enforce Contract Evolution

Maintainers can publish curated artifact snapshots and safely evolve artifact contracts under CI-enforced versioning rules.

### Story 3.1: Implement curated publish/export command flow

As a maintainer,
I want an explicit publish/export command that writes commit-worthy snapshot artifacts to a configured destination,
So that runtime cache and durable artifacts stay separated.

**Implements:** FR5

**Acceptance Criteria:**

**Given** runtime artifacts produced for a project
**When** I execute the publish/export flow
**Then** a curated artifact set is written to the configured destination without reclassifying `.fennec/` runtime cache as canonical history
**And** export behavior fails with explicit errors when destination/configuration constraints are invalid

### Story 3.2: Generate snapshot manifest and retention metadata

As a maintainer,
I want each published snapshot to include provenance and lifecycle metadata,
So that teams can audit origin and retention decisions.

**Implements:** FR5

**Acceptance Criteria:**

**Given** a successful publish/export operation
**When** snapshot artifacts are written
**Then** a manifest includes schema version, producer version, source context, timestamp, and artifact inventory metadata
**And** retention metadata fields are present to support repository policy decisions

### Story 3.3: Enforce contract SemVer compatibility in CI

As a maintainer,
I want CI checks that detect schema compatibility class and required version bump behavior,
So that breaking contract changes cannot merge silently.

**Implements:** FR6

**Acceptance Criteria:**

**Given** a proposed change to canonical envelope or payload contracts
**When** CI compatibility checks run
**Then** detected breaking changes require major version increment and fail otherwise
**And** additive/corrective changes are validated against minor/patch expectations

### Story 3.4: Document and operationalize publish/versioning workflow

As a maintainer,
I want contributor guidance for publish/export and contract evolution policy,
So that teams can apply the process consistently.

**Implements:** FR5, FR6

**Acceptance Criteria:**

**Given** contributor-facing project documentation
**When** maintainers follow the documented workflow
**Then** they can run publish/export and interpret schema versioning rules without relying on tribal knowledge
**And** documentation explicitly differentiates runtime cache outputs from commit-worthy published artifacts
