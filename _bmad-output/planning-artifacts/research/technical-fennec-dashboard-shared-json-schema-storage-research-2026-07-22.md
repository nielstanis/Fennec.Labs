---
stepsCompleted: [1, 2, 3, 4, 5, 6]
inputDocuments: []
workflowType: 'research'
lastStep: 6
research_type: 'technical'
research_topic: 'Fennec dashboard shared JSON schema and storage model (project-scoped now, hosted later)'
research_goals: 'Define a reusable versioned result schema, storage strategy, and migration path that works for local project dashboards first and hosted aggregation later.'
user_name: 'Niels'
date: '2026-07-22'
web_research_enabled: true
source_verification: true
---

# Research Report: Technical

**Date:** 2026-07-22
**Author:** Niels
**Research Type:** Technical

---

## Research Overview

This research evaluates how Fennec.Labs should evolve from command-specific JSON outputs into a shared, versioned data contract that powers both a project-scoped dashboard now and a hosted portfolio dashboard later. The central question was whether v1 needs a database at all, or whether disciplined flat-file artifacts can satisfy current requirements while preserving a clean migration path.

Findings support a **flat-file-first v1**: Fennec already emits useful JSON artifacts, but output shapes are inconsistent across commands and lack a shared envelope (`schemaVersion`, producer metadata, source context). Standardizing that contract first delivers the biggest value for dashboard usability, compatibility, and future hosted readiness, without introducing unnecessary operational complexity.

Methodology combined repository-level code inspection (actual JSON surfaces in current command handlers) with current public references for JSON Schema dialecting, .NET package/vulnerability outputs, SBOM interoperability (SPDX/CycloneDX), API architecture patterns, and repository size/operations constraints. The result is a staged implementation strategy that minimizes risk while keeping long-term options open.

---

<!-- Content will be appended sequentially through research workflow steps -->

## Technical Research Scope Confirmation

**Research Topic:** Fennec dashboard shared JSON schema and storage model (project-scoped now, hosted later)  
**Research Goals:** Define a reusable versioned result schema, storage strategy, and migration path that works for local project dashboards first and hosted aggregation later.

**Technical Research Scope:**

- Architecture Analysis - design patterns, frameworks, system architecture
- Implementation Approaches - development methodologies, coding patterns
- Technology Stack - languages, frameworks, tools, platforms
- Integration Patterns - APIs, protocols, interoperability
- Performance Considerations - scalability, optimization, patterns

**Research Methodology:**

- Current web data with rigorous source verification
- Multi-source validation for critical technical claims
- Confidence level framework for uncertain information
- Comprehensive technical coverage with architecture-specific insights

**Scope Confirmed:** 2026-07-22

## Technology Stack Analysis

### Programming Languages

For v1 (project-scoped dashboard), C#/.NET is the natural primary implementation language because Fennec already runs as a .NET CLI and can produce machine-readable JSON outputs from core dependency commands. The practical language split for this initiative is:
- **C#** for schema production/validation, storage writers, and dashboard backend surfaces.
- **TypeScript/JavaScript (optional)** for web UI shells later, while keeping data contracts language-neutral through JSON Schema.

_Popular Languages: C# for producer and backend paths in this repo; JavaScript/TypeScript likely for browser UI layers._  
_Emerging Languages: not a central decision driver for this initiative; contract-first JSON keeps language choice open._  
_Language Evolution: .NET command surfaces now provide JSON output contracts and versioning knobs, supporting long-lived machine interfaces._  
_Performance Characteristics: `System.Text.Json` emphasizes high performance and low memory allocation with UTF-8-first processing, which fits repeated CLI/result serialization workloads._  
_Source: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list_  
_Source: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview_

### Development Frameworks and Libraries

The critical framework decision is to formalize output contracts using **JSON Schema dialects** instead of ad hoc per-command JSON. JSON Schema explicitly supports dialect declaration with `$schema`, meta-schemas, and validation vocabulary separation, which maps well to "shared schema now, evolution later" requirements.

_Major Frameworks: JSON Schema 2020-12 + .NET `System.Text.Json` serialization stack._  
_Micro-frameworks: schema validation libraries can be swapped as long as they implement the selected JSON Schema dialect._  
_Evolution Trends: JSON Schema moved to date-based dialect identifiers; explicit dialect declaration is recommended for tooling reliability._  
_Ecosystem Maturity: JSON Schema has stable core/validation specs and self-describing meta-schema mechanics._  
_Source: https://json-schema.org/specification_  
_Source: https://json-schema.org/understanding-json-schema/reference/schema_  
_Source: https://json-schema.org/draft/2020-12/json-schema-core_

### Database and Storage Technologies

For v1, a **flat-file-first** strategy is credible and likely preferable: Fennec already writes command outputs as JSON under `.fennec/` and the dashboard can consume those files directly with a stable schema/version envelope. A database may be unnecessary until hosted aggregation or heavier cross-run analytics appear.

Escalation path if/when flat files stop being enough:
- **SQLite**: minimal-operational, embedded local index/cache over JSON artifacts.
- **DuckDB**: local analytical querying over many result files.
- **PostgreSQL**: hosted central service storage when multi-project ingestion/querying is introduced.

_Relational Databases: likely a phase-2 concern, not a v1 prerequisite._  
_NoSQL Databases: not required to solve current requirements._  
_Flat Files: strongest v1 fit (simple, inspectable, Git-friendly if desired, zero runtime service dependency)._  
_Data Warehousing: defer until hosted mode requires portfolio-scale analytics._  
_Source: https://www.sqlite.org/whentouse.html_  
_Source: https://duckdb.org/why_duckdb_  
_Source: https://www.postgresql.org/about/_

### Development Tools and Platforms

Current .NET CLI capabilities already provide key producer inputs for this dashboard data model:
- `dotnet package list --include-transitive` for transitive package capture.
- `dotnet package list --format json --output-version` for machine-readable and versioned upstream input.
- `--vulnerable` support in modern SDK versions, backed by NuGet's vulnerability information resources.

_IDE and Editors: unchanged from current .NET workflows; no specialized IDE requirement introduced by this architecture._  
_Version Control: Git-managed artifacts remain viable, but generated data commit policy must be explicit._  
_Build Systems: existing `dotnet` CLI workflows remain the producer pipeline._  
_Testing Frameworks: schema validation and contract tests become first-class for data compatibility._  
_Source: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list_  
_Source: https://learn.microsoft.com/en-us/nuget/api/vulnerability-info_

### Current Fennec CLI JSON Formats (As-Is Baseline)

Repository inspection shows the existing JSON surfaces are useful but inconsistent, which is exactly why a shared schema is needed:

- **Scorecard** writes `result.json` under `.fennec/scorecard/<project>/<timestamp>/` with:
  - `project`, `framework`, `generatedAt`
  - `dependencyTree.topLevel[]` and `dependencyTree.transitive[]`
  - `packages[]` including score/check details and per-package errors.
- **Compare** writes cached `result.json` under `.fennec/compare/<package>/<current>-vs-<previous>/` with:
  - package/version metadata
  - `perDll[]`, `onlyInCurrent[]`, `onlyInPrevious[]`, `summary`.
- **Reproduce** writes cached `result.json` under `.fennec/reproduce/<package>/<version>/` (file mode), with:
  - `localSource`, `feedVersion`, per-DLL diffs, summary.
- **Instrument** currently has **two JSON shapes**:
  - `--json` prints a flattened invocation list to stdout.
  - `--file-format json` writes full `AssemblyResult` object graphs to files.
- None of these payloads currently carry a cross-command envelope like `schemaVersion`, `command`, `producedAt`, `producerVersion`, or `sourceContext`.

This strongly supports a flat-file v1 design where each artifact keeps command-specific payloads but is wrapped in one shared versioned envelope for consistent dashboard ingestion.

_Source (repo code): `src/FennecLabs.Cli/Commands/ScorecardCommandHandler.cs`_  
_Source (repo code): `src/FennecLabs.Cli/Commands/CompareCommandHandler.cs`_  
_Source (repo code): `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs`_  
_Source (repo code): `src/FennecLabs.Cli/Commands/InstrumentCommandHandler.cs`_  
_Source (repo code): `src/FennecLabs.Cli/OutputCache.cs`_  
_Source (repo code): `src/FennecLabs.Instrumentation/Output/JsonWriter.cs`_

### Cloud Infrastructure and Deployment

Hosted mode is out of v1 scope, but technology choices should preserve a clean path:
- OpenSSF Scorecard provides precomputed/public data channels (REST API and BigQuery datasets) that can serve as reference for ingestion patterns when centralized mode arrives.
- GitHub can export dependency graph data as SBOM, using SPDX, which aligns with future federation/import patterns and interoperable supply-chain reporting.

_Major Cloud Providers: not a v1 blocker; data contract quality matters more than provider choice at this stage._  
_Container Technologies: likely relevant only once hosted mode starts._  
_Serverless Platforms: possible future hosting option; currently secondary to schema/storage design._  
_CDN and Edge Computing: relevant later for hosted read scaling, not for v1 local mode._  
_Source: https://github.com/ossf/scorecard/blob/main/README.md_  
_Source: https://api.scorecard.dev_  
_Source: https://docs.github.com/en/code-security/how-tos/secure-your-supply-chain/establish-provenance-and-integrity/export-dependencies-as-sbom_

### Technology Adoption Trends

The strongest trend signal for this problem space is standardization of machine-readable supply-chain artifacts:
- SPDX is an ISO-recognized standard with current v3 line availability.
- CycloneDX has explicit first-class dependency graph modeling (including transitive relationships, services, and vulnerabilities).
- JSON contract versioning discipline (e.g., SemVer and explicit schema dialect/version declaration) is increasingly required for long-lived automation interfaces.

_Migration Patterns: ad hoc JSON -> versioned schema contracts + contract tests + compatibility policy._  
_Emerging Technologies: richer SBOM/attestation ecosystems increase pressure for interoperable data models early._  
_Legacy Technology: unversioned generated JSON becomes a drag as consumers multiply (CLI, dashboard, hosted service, MCP tools)._  
_Community Trends: ecosystem momentum is toward standardized software supply-chain artifacts and machine validation._  
_Source: https://spdx.dev/use/specifications/_  
_Source: https://cyclonedx.org/specification/overview/_  
_Source: https://semver.org/_

## Integration Patterns Analysis

### API Design Patterns

For this initiative, integration should be staged:
1. **v1 local/project-scoped:** file-contract integration (dashboard reads versioned JSON artifacts directly from `.fennec` or a committed artifact folder).
2. **v2 hosted:** HTTP API ingestion/query endpoints with the same envelope schema, avoiding dual contracts.

Minimal APIs are a practical fit for future hosted ingestion/read endpoints due to low ceremony and high performance in ASP.NET Core.

_RESTful APIs: strong fit for hosted ingestion/query interfaces once centralized mode starts._  
_GraphQL APIs: optional later for rich cross-project filtering; not necessary to prove v1._  
_RPC and gRPC: viable for high-throughput service-to-service transport later, but likely unnecessary early._  
_Webhook Patterns: useful for event-driven hosted refresh after CI runs complete._  
_Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0_  
_Source: https://grpc.io/docs/what-is-grpc/introduction/_

### Communication Protocols

Current producer/consumer integration is effectively filesystem + JSON. For hosted evolution, HTTP JSON should be first protocol; gRPC can remain a later optimization option if throughput or strict IDL requirements demand it.

_HTTP/HTTPS Protocols: recommended primary protocol for hosted mode compatibility and tooling ubiquity._  
_WebSocket Protocols: optional if near-real-time dashboard refresh is needed later._  
_Message Queue Protocols: defer unless hosted ingestion volume requires decoupled buffering/retry semantics._  
_grpc and Protocol Buffers: strong for internal high-throughput service links, optional for this roadmap._  
_Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0_  
_Source: https://grpc.io/docs/what-is-grpc/introduction/_

### Data Formats and Standards

Data-format interoperability should combine:
- **Primary contract:** JSON + explicit JSON Schema dialect/version (`$schema`).
- **External exchange compatibility:** SPDX/CycloneDX alignment for dependency and supply-chain ecosystems.

_JSON and XML: JSON is the practical primary format in existing CLI flows and dashboard consumers._  
_Protobuf and MessagePack: optional transport formats for future internal services, not required for local file contracts._  
_CSV and Flat Files: flat-file JSON is directly viable for v1 and can remain long-term for local mode._  
_Custom Data Formats: avoid ad hoc custom formats; prefer explicit schema + versioning instead._  
_Source: https://json-schema.org/understanding-json-schema/reference/schema_  
_Source: https://docs.github.com/en/rest/dependency-graph/sboms_  
_Source: https://cyclonedx.org/specification/overview/_

### System Interoperability Approaches

The key interoperability decision is to keep one semantic model across local and hosted modes:
- **Local:** read files directly.
- **Hosted:** ingest the same envelope payloads over HTTP, store/index as needed.

This minimizes translation code and prevents divergence between project-scoped and centralized views.

_Point-to-Point Integration: v1 local dashboard <-> local result files is direct and low-risk._  
_API Gateway Patterns: relevant only when multiple hosted services emerge._  
_Service Mesh: not justified at current scope._  
_Enterprise Service Bus: overkill for expected scale/complexity._  
_Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0_

### Microservices Integration Patterns

Hosted mode should start as a single service boundary first, then split only when needed. Premature service decomposition would add contract and ops overhead without helping v1.

_API Gateway Pattern: optional later once external clients diversify._  
_Service Discovery: unnecessary until multi-service topology exists._  
_Circuit Breaker Pattern: relevant for external dependency fetchers (Scorecard/GitHub/NuGet) in hosted mode._  
_Saga Pattern: not required unless multi-write distributed workflows are introduced._  
_Source: https://github.com/ossf/scorecard/blob/main/README.md_

### Event-Driven Integration

A practical event pattern for v1 local mode is **file-change driven refresh**: when new result artifacts arrive, dashboard views reload/refresh. .NET `FileSystemWatcher` provides a native path for this.

_Publish-Subscribe Patterns: local file events are sufficient for v1; brokered pub/sub can come with hosted scale._  
_Event Sourcing: not needed for initial dashboard architecture._  
_Message Broker Patterns: defer until hosted ingestion throughput/decoupling pressures require it._  
_CQRS Patterns: optional for hosted read-optimized projections later._  
_Source: https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-10.0_

### Integration Security Patterns

Security posture differs by mode:
- **Local mode:** file integrity and provenance discipline (what generated the artifact, when, from which input).
- **Hosted mode:** authenticated ingestion, authorization, and transport security become mandatory.

SBOM/Scorecard ecosystems reinforce the value of provenance-aware machine artifacts.

_OAuth 2.0 and JWT: hosted ingestion/query APIs should plan for token-based auth._  
_API Key Management: minimal hosted bootstrap option; rotate and scope keys carefully._  
_Mutual TLS: optional for internal service links in later hosted deployments._  
_Data Encryption: HTTPS in transit + storage controls at rest for hosted stores._  
_Source: https://docs.github.com/en/rest/dependency-graph/sboms_  
_Source: https://securityscorecards.dev/_

## Architectural Patterns and Design

### System Architecture Patterns

Recommended architectural style is a **strangler-style evolution** from today's command-specific JSON outputs to a unified envelope contract, without breaking existing CLI behavior. Treat current outputs as the legacy surface and progressively route new dashboard consumers through the shared schema layer.

For v1, keep architecture simple:
- Producer: existing CLI commands.
- Contract layer: versioned shared envelope + command payload.
- Consumer: local project-scoped dashboard reading flat files.

_Source: https://learn.microsoft.com/en-us/azure/architecture/guide/architecture-styles/_  
_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig_

### Design Principles and Best Practices

Pattern selection should stay constraint-driven: pick the smallest architecture that solves the real problem (contract inconsistency and discoverability), then evolve. This argues against introducing hosted-service complexity or event stores in v1.

Design principles for this effort:
- **Contract-first** (`$schema`, schema version, producer metadata).
- **Backward-compatible evolution** by default; explicit major breaks only.
- **Single semantic model** reused by CLI, dashboard, and later hosted mode.

_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/_  
_Source: https://json-schema.org/understanding-json-schema/reference/schema_  
_Source: https://semver.org/_

### Scalability and Performance Patterns

For project-scoped v1, flat files avoid database runtime overhead and are usually sufficient. If read/query load grows, introduce optional read models (index files, SQLite, or DuckDB) without changing producer payload contracts.

CQRS-style separation is useful as the system scales: commands produce immutable artifacts, while read models can be optimized independently for dashboard queries.

_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs_  
_Source: https://www.sqlite.org/whentouse.html_  
_Source: https://duckdb.org/why_duckdb_

### Integration and Communication Patterns

Integration architecture should stay dual-mode compatible from day one:
- **Local mode:** filesystem artifact integration.
- **Hosted mode later:** HTTP ingestion + query APIs over the exact same schema.

That enables gradual mode expansion without payload rewrites and keeps interop high with ecosystem standards (SPDX/CycloneDX imports/exports where needed).

_Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0_  
_Source: https://docs.github.com/en/rest/dependency-graph/sboms_  
_Source: https://cyclonedx.org/specification/overview/_

### Security Architecture Patterns

When APIs are introduced (hosted mode), API-security risks become first-class (authorization, authentication, resource consumption controls, and configuration hardening). Security design should be built into hosted architecture upfront, not retrofitted.

For local mode, prioritize artifact provenance fields (producer version, source project, generated timestamp, command context) so consumers can trust and audit artifacts.

_Source: https://owasp.org/www-project-api-security/_  
_Source: https://securityscorecards.dev/_

### Data Architecture Patterns

Event sourcing is explicitly not recommended for v1: while append-only history is attractive for auditability, pattern complexity and migration cost are high relative to current needs. Flat-file snapshots + optional append-only run history are a better fit initially.

Data architecture recommendation:
- Canonical artifact = versioned JSON envelope file.
- Optional index/manifest files for fast browsing and "latest" resolution.
- Optional local read-store only when query complexity justifies it.

_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/event-sourcing_  
_Source: https://json-schema.org/draft/2020-12/json-schema-core_

### Deployment and Operations Architecture

Operationally, v1 should avoid service operations burden:
- no required database daemon,
- no required network service,
- predictable local artifact generation via existing CLI flows.

For future hosted mode, ASP.NET Core APIs plus standard cloud design patterns provide a clear path without invalidating local-mode artifacts.

_Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0_  
_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/_

## Implementation Approaches and Technology Adoption

### Technology Adoption Strategies

Adopt in thin vertical increments instead of a one-shot rewrite:
1. Define shared envelope schema and version policy.
2. Wrap one command output first (Scorecard, because it is v1 dashboard scope).
3. Add compatibility adapters for existing files during transition.
4. Expand schema coverage command-by-command (reproduce, compare, instrument).

This is effectively a controlled strangler migration on data contracts.

_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig_

### Development Workflows and Tooling

Implementation should stay inside existing developer workflows:
- Existing `dotnet`/Fennec CLI commands remain producers.
- Add contract tests that validate emitted artifacts against selected JSON Schema dialect.
- Keep generated artifacts inspectable in PRs where appropriate (or publish as build artifacts where repository growth is a concern).

_Source: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list_  
_Source: https://json-schema.org/specification_  
_Source: https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts_

### Testing and Quality Assurance

Quality should focus on contract stability and consumer safety:
- Schema validation tests for every emitted artifact type.
- Golden-file compatibility tests across historical samples.
- Backward-compatibility gates tied to schema version bumps (SemVer discipline).

_Source: https://json-schema.org/specification_  
_Source: https://semver.org/_

### Deployment and Operations Practices

v1 operations should remain simple:
- no required runtime database,
- no mandatory hosted service,
- deterministic local generation + rendering.

For teams needing central visibility before hosted mode, CI-produced artifacts can be stored as workflow artifacts rather than committed blobs.

_Source: https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts_

### Team Organization and Skills

A small cross-functional slice is sufficient:
- CLI/data-contract engineer,
- dashboard consumer engineer,
- security/data-quality reviewer.

Skill emphasis is schema governance and compatibility thinking, not infrastructure operations.

_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/_

### Cost Optimization and Resource Management

Flat-file-first minimizes operational cost and complexity. Repository growth is the main cost/risk if generated data is committed aggressively; mitigation options include selective commits, retention policies, and artifact offloading.

GitHub guidance reinforces that repository/file sizes have practical limits and performance impact.

_Source: https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github_  
_Source: https://git-lfs.com/_

### Risk Assessment and Mitigation

Major implementation risks and mitigations:
- **Schema drift across commands** -> enforce shared envelope and CI contract checks.
- **Consumer breakage** -> SemVer-based schema versioning + compatibility test matrix.
- **Repository bloat** -> default to local cache + optional curated commit policy/artifact storage.
- **Security posture gaps in hosted mode** -> apply OWASP API controls from first hosted endpoint.

_Source: https://semver.org/_  
_Source: https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github_  
_Source: https://owasp.org/www-project-api-security/_

## Technical Research Recommendations

### Implementation Roadmap

1. Define canonical envelope (`schemaVersion`, `command`, `producedAt`, `producerVersion`, `sourceContext`, `payload`).
2. Apply it to Scorecard output first and adapt dashboard reader.
3. Add compare/reproduce/instrument wrappers with command-specific payload schemas.
4. Add optional manifest/index files for multi-run browsing.
5. Defer database introduction until query/load requirements prove need.

### Technology Stack Recommendations

- Producer/consumer core: C# + `System.Text.Json`.
- Contract: JSON Schema 2020-12 + explicit `$schema`.
- Storage: flat JSON files for v1; optional SQLite/DuckDB read-store later.
- Hosted evolution: ASP.NET Core Minimal APIs over identical payload schema.

### Skill Development Requirements

- JSON Schema authoring and evolution governance.
- Contract testing (golden files + schema validation).
- Secure API design for future hosted endpoints.

### Success Metrics and KPIs

- % of v1 dashboard reads served from shared-schema artifacts without custom per-command parsers.
- Number of breaking schema changes (target: zero without major version bump).
- Median time to produce and render project dependency+scorecard view after CLI run.

## Research Synthesis

# Fennec Dashboard Data Contract Architecture: Comprehensive Technical Research

## Executive Summary

Fennec's v1 dashboard goal (project-scoped dependency tree + scorecard view) does **not** require a database-first architecture. Current CLI outputs already provide much of the needed content, but the payloads are fragmented by command and lack a common envelope for reliable consumer behavior. The highest-leverage technical move is to standardize a versioned JSON contract across commands while preserving command-specific payload detail.

The recommended architecture is an incremental contract migration: define one canonical envelope, apply it to scorecard output first, then extend to compare/reproduce/instrument. This delivers immediate value for dashboard readability and consistency while preserving compatibility with terminal/LLM workflows and creating a direct path to hosted ingestion later.

Strategically, this approach minimizes operational burden in v1 (no mandatory DB/service runtime), reduces migration risk through strangler-style evolution, and supports long-term interoperability through JSON Schema discipline plus optional SBOM alignment (SPDX/CycloneDX) where external exchange is needed.

**Key Technical Findings:**

- Current Fennec JSON outputs are useful but structurally inconsistent across commands.
- Flat-file artifacts are sufficient for v1 if governed by a shared envelope + schema version policy.
- JSON Schema dialect declaration (`$schema`) and SemVer-style compatibility rules are critical to prevent consumer breakage.
- Hosted mode can be introduced later through HTTP ingestion/query APIs over the same payload model.

**Technical Recommendations:**

1. Implement a shared envelope contract immediately.
2. Migrate scorecard output first (v1 dashboard scope).
3. Add compatibility tests and schema governance before broad rollout.
4. Defer database introduction until query/load evidence justifies it.
5. Design hosted APIs as transport over the same canonical payload.

## Table of Contents

1. Technical Research Introduction and Methodology  
2. Technical Landscape and Architecture Analysis  
3. Implementation Approaches and Best Practices  
4. Technology Stack Evolution and Current Trends  
5. Integration and Interoperability Patterns  
6. Performance and Scalability Analysis  
7. Security and Compliance Considerations  
8. Strategic Technical Recommendations  
9. Implementation Roadmap and Risk Assessment  
10. Future Technical Outlook and Innovation Opportunities  
11. Source Verification and Research Quality  
12. Appendices and Reference Materials

## 1. Technical Research Introduction and Methodology

### Technical Research Significance

Fennec is crossing a product boundary: from CLI-first output to human-oriented dashboard consumption. The risk is not data absence but **contract entropy** as consumers multiply (CLI, local dashboard, hosted service, MCP tools).

_Technical Importance: durable machine contracts are required for multi-consumer evolution._  
_Business Impact: faster dependency-risk understanding without re-implementing views per mode._  
_Source: https://json-schema.org/understanding-json-schema/reference/schema_

### Technical Research Methodology

- **Technical scope:** current output inventory, contract design, storage options, integration and evolution patterns.
- **Data sources:** repository code + Microsoft/GitHub/OpenSSF/JSON Schema/SPDX/CycloneDX documentation.
- **Analysis framework:** local-first feasibility, compatibility risk, operational cost, hosted migration readiness.
- **Time period:** current published docs (2025-2026 updates where available).
- **Technical depth:** architecture and implementation-level recommendations.

### Technical Research Goals and Objectives

**Original Technical Goals:** Define a reusable versioned result schema, storage strategy, and migration path that works for local project dashboards first and hosted aggregation later.

**Achieved Technical Objectives:**

- Validated flat-file-first feasibility for v1.
- Produced concrete envelope and migration recommendations.
- Identified current JSON format inconsistencies requiring normalization.

## 2. Technical Landscape and Architecture Analysis

### Current Technical Architecture Patterns

Current architecture is command-centric output generation with per-command JSON structures and cache paths. This is effective for isolated command usage, but weak for unified dashboard consumption.

_Dominant Patterns: command-local serialization and caching under `.fennec/`._  
_Architectural Evolution: move to command payload + shared envelope._  
_Architectural Trade-offs: keep CLI behavior stable while introducing consumer consistency._  
_Source (repo code): `src/FennecLabs.Cli/OutputCache.cs`_

### System Design Principles and Best Practices

- Keep one semantic model across modes.
- Enforce explicit schema dialect/version declarations.
- Prefer additive evolution; gate breaking changes through explicit major bumps.

_Source: https://json-schema.org/specification_  
_Source: https://semver.org/_

## 3. Implementation Approaches and Best Practices

### Current Implementation Methodologies

Best-fit implementation pattern is incremental migration with compatibility adapters, not hard cutover.

_Development Approaches: strangler-style contract adoption by command._  
_Code Organization Patterns: shared envelope library + per-command payload schemas._  
_Quality Assurance Practices: schema validation + golden artifact tests._  
_Deployment Strategies: local generation first; hosted ingestion later._  
_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig_

### Implementation Framework and Tooling

_Development Frameworks: .NET/C# with `System.Text.Json` as core serializer._  
_Tool Ecosystem: existing CLI plus contract validation tooling._  
_Build and Deployment Systems: existing `dotnet` flow + optional CI artifact publication._  
_Source: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview_  
_Source: https://docs.github.com/en/actions/how-tos/manage-workflow-runs/download-workflow-artifacts_

## 4. Technology Stack Evolution and Current Trends

### Current Technology Stack Landscape

- JSON Schema 2020-12 provides stable dialect/meta-schema mechanics.
- .NET package tooling now exposes machine-readable, versioned outputs (`--format json`, `--output-version`).
- SBOM ecosystems (SPDX/CycloneDX) strengthen interoperability expectations.

_Source: https://json-schema.org/specification_  
_Source: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list_  
_Source: https://docs.github.com/en/rest/dependency-graph/sboms_

### Technology Adoption Patterns

_Adoption Trends: standard contract governance increasingly required in dependency-heavy systems._  
_Migration Patterns: ad hoc payloads -> versioned envelopes + compatibility tests._  
_Emerging Technologies: richer SBOM and supply-chain provenance ecosystems._  
_Source: https://cyclonedx.org/specification/overview/_  
_Source: https://spdx.dev/use/specifications/_

## 5. Integration and Interoperability Patterns

### Current Integration Approaches

Recommended integration layering:
- local files as source-of-truth for v1,
- HTTP transport over same payload in hosted phase,
- optional external exchange through SPDX/CycloneDX mapping.

_API Design Patterns: local file contracts first, hosted REST later._  
_Service Integration: single-service hosted start to limit complexity._  
_Data Integration: schema-stable payload handoff across producers/consumers._  
_Source: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/apis?view=aspnetcore-10.0_

### Interoperability Standards and Protocols

_Standards Compliance: JSON Schema dialect declaration and SBOM compatibility surfaces._  
_Protocol Selection: HTTP/JSON default; gRPC optional optimization path._  
_Integration Challenges: contract drift and multi-shape instrumentation payloads._  
_Source: https://json-schema.org/understanding-json-schema/reference/schema_  
_Source: https://grpc.io/docs/what-is-grpc/introduction/_

## 6. Performance and Scalability Analysis

### Performance Characteristics and Optimization

Flat-file local reads are fast enough for v1 scale and avoid service overhead. Introduce manifest/index files before introducing DB runtimes.

_Performance Benchmarks: not yet measured; implement baseline timings as part of rollout._  
_Optimization Strategies: envelope normalization + lightweight index files + selective loading._  
_Monitoring and Measurement: capture generation-to-render latency in CI and local runs._

### Scalability Patterns and Approaches

If scale pressure appears, apply CQRS-like read model optimization without changing producer contracts.

_Scalability Patterns: optional read-store escalation (SQLite/DuckDB) behind same payload contract._  
_Capacity Planning: watch artifact count/size growth and repo health metrics._  
_Elasticity and Auto-scaling: hosted concern only; local mode remains file-based._  
_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs_  
_Source: https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github_

## 7. Security and Compliance Considerations

### Security Best Practices and Frameworks

Hosted APIs should apply OWASP API Security guidance from the first endpoint. Local artifacts should carry provenance metadata to support auditability and trust.

_Security Frameworks: OWASP API Security Top 10 focus areas._  
_Threat Landscape: authz/authn gaps, overexposed data, resource abuse in APIs._  
_Secure Development Practices: contract validation + least-privilege endpoint design._  
_Source: https://owasp.org/www-project-api-security/_

### Compliance and Regulatory Considerations

SBOM interoperability supports downstream compliance use cases, even if Fennec's native model remains dashboard-focused.

_Industry Standards: SPDX/CycloneDX for external dependency disclosure._  
_Regulatory Compliance: improves readiness for software supply-chain reporting controls._  
_Audit and Governance: versioned artifacts + provenance fields enable traceability._  
_Source: https://docs.github.com/en/rest/dependency-graph/sboms_

## 8. Strategic Technical Recommendations

### Technical Strategy and Decision Framework

1. Prioritize shared contract governance over infrastructure complexity.
2. Keep local mode file-native through v1.
3. Introduce hosted mode as transport/storage extension, not schema fork.
4. Govern schema evolution with compatibility policy and CI gates.

_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/_

### Competitive Technical Advantage

The differentiator is not data acquisition; it's coherent reuse of existing data across interfaces and modes with minimal duplication.

_Technology Differentiation: one model, multiple consumers._  
_Innovation Opportunities: add instrumentation/compare/reproduce views atop same envelope._  
_Strategic Technology Investments: schema governance + contract tests before platform expansion._

## 9. Implementation Roadmap and Risk Assessment

### Technical Implementation Framework

**Phase 1 (now):** envelope spec + scorecard migration.  
**Phase 2:** compare/reproduce/instrument contract normalization.  
**Phase 3:** optional indexes/read-store for richer local queries.  
**Phase 4:** hosted ingestion/query APIs using unchanged payload schema.

_Source: https://learn.microsoft.com/en-us/azure/architecture/patterns/strangler-fig_

### Technical Risk Management

- **Risk:** breaking consumers via silent payload changes.  
  **Mitigation:** explicit `schemaVersion` + SemVer policy + CI contract matrix.
- **Risk:** repository bloat from committed artifacts.  
  **Mitigation:** curated commit policy, retention strategy, CI artifact publishing.
- **Risk:** overengineering v1 with unnecessary infrastructure.  
  **Mitigation:** flat-file-first architecture with evidence-based escalation triggers.

_Source: https://semver.org/_  
_Source: https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github_

## 10. Future Technical Outlook and Innovation Opportunities

### Emerging Technology Trends

_Near-term (1-2 years):_ stronger supply-chain artifact standardization and API-driven security posture automation.  
_Medium-term (3-5 years):_ deeper convergence between SBOM, attestations, and dependency risk scoring.  
_Long-term (5+ years):_ broader policy-as-code and continuous compliance integration across developer tooling.

_Source: https://cyclonedx.org/specification/overview/_  
_Source: https://spdx.dev/use/specifications/_

### Innovation and Research Opportunities

- Schema mapping between Fennec native payloads and SBOM/attestation formats.
- Cross-run risk trend models (local history + hosted aggregation).
- Native Copilot/AI surfaces over same contract for conversational dependency analysis.

## 11. Source Verification and Research Quality

### Primary Sources

- Repository source inspection (current Fennec JSON producers and cache paths).
- Microsoft Learn (.NET CLI, ASP.NET APIs, architecture patterns).
- JSON Schema specification resources.
- GitHub Docs (SBOM endpoints, artifact/repo practices).
- OpenSSF Scorecard documentation.
- OWASP API Security guidance.

### Quality and Limitations

- Claims tied to architecture strategy were cross-validated across multiple sources where possible.
- Some hosted-mode choices remain intentionally provisional because v1 explicitly defers hosted implementation.
- Quantitative performance benchmarks are not yet measured and should be added during implementation.

## 12. Appendices and Reference Materials

### Appendix A: Current Fennec JSON Output Surfaces (Code-Verified)

- `ScorecardCommandHandler` -> scorecard `result.json` with dependency tree + package checks.
- `CompareCommandHandler` -> compare `result.json` with per-DLL diff summary.
- `ReproduceCommandHandler` -> reproduce `result.json` (file mode cache path).
- `InstrumentCommandHandler` -> stdout flattened JSON (`--json`) and file JSON (`--file-format json`) with different shapes.
- `OutputCache` -> current `.fennec/<command>/.../result.json` cache conventions.

### Appendix B: Recommended Canonical Envelope (Draft)

```json
{
  "$schema": "https://example.dev/fennec/schema/envelope-1.0.0.json",
  "schemaVersion": "1.0.0",
  "command": "scorecard",
  "producedAt": "2026-07-22T19:00:00Z",
  "producerVersion": "fennec/1.x",
  "sourceContext": {
    "project": "path-or-id",
    "framework": "net8.0"
  },
  "payload": {}
}
```

---

## Technical Research Conclusion

### Summary of Key Technical Findings

The highest-value decision is to standardize a shared, versioned JSON envelope across existing CLI outputs and keep v1 storage flat-file-first. This directly addresses developer usability, dashboard consistency, and future hosted readiness with minimal new operational burden.

### Strategic Technical Impact Assessment

This approach reduces rework risk, avoids premature infrastructure commitments, and creates a durable compatibility boundary across human UI, CLI automation, and future service consumers.

### Next Steps Technical Recommendations

1. Finalize envelope schema and compatibility policy.
2. Implement scorecard output migration and dashboard reader update.
3. Add contract validation/golden tests.
4. Define artifact commit policy (local cache vs curated committed artifacts).
5. Reassess DB need only after measured query/load evidence.

---

**Technical Research Completion Date:** 2026-07-22  
**Research Period:** current comprehensive technical analysis  
**Source Verification:** repository code + current public documentation  
**Technical Confidence Level:** High for v1 local architecture; Medium for deferred hosted operational details
