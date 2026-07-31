---
stepsCompleted:
  - step-01-requirements-extracted
  - step-02-epics-approved
  - step-03-stories-generated
inputDocuments:
  - _bmad-output/planning-artifacts/architecture/architecture-Fennec.Labs-taint-analysis-2026-07-30/ARCHITECTURE-SPINE.md
  - _bmad-output/planning-artifacts/architecture/architecture-Fennec.Labs-taint-analysis-2026-07-30/TAINT-ANALYSIS-COMPANION.md
resolvedDecisions:
  OQ-1: "All 11 categories ship: 5 sources + 6 sinks as drafted"
  OQ-2: "Hybrid severity: sink rule owns base severity; context/capability downgrades max 1 level; confidence is a separate display field"
  OQ-3: "fidelity=unresolved findings shown by default"
  OQ-7: "Unknown context falls back to full catalog at uniform priority"
  OQ-8: "unmatchedRelevantExposures shown in terminal output by default"
  OQ-9: "Per-package context detection for multi-DLL NuGet packages"
  OQ-10: "Factory lambda DI deferred — fall back to type-hierarchy in v1"
  OQ-11: "Conservative: no prefix = all NuGet is third-party"
  OQ-12: "Fail with clear hint when build outputs are absent"
---

# Fennec.Labs — Taint Analysis v1 Epic Breakdown

## Overview

This document decomposes the taint-analysis architecture (AD-1..AD-17) into implementable epics and stories. Four epics map directly to the four implementation phases in the companion document.

---

## Functional Requirements

FR-1: Taint analysis executes ONLY when opt-in flags are provided; baseline `fennec instrument` output is byte-identical when no taint flags are present. *(AD-1, AD-9)*
FR-2: Source, sink, propagator, and sanitizer definitions come from a versioned policy contract; unmatched calls produce explicit `unknown` classification. *(AD-2)*
FR-3: Every analyzable method body yields a normalized CFG (basic blocks + directed edges + call-site anchors); non-analyzable methods carry reason codes. *(AD-3)*
FR-4: Inter-procedural call graph records resolved and unresolved edges separately; unresolved edges propagate as explicit `uncertain=true` markers. *(AD-4)*
FR-5: Taint propagation uses four canonical states (`untainted`, `tainted`, `sanitized`, `unknown`) across all flow types; sanitizer transitions require explicit policy entries. *(AD-5)*
FR-6: Finding endpoints carry symbol mapping with fidelity (`exact`, `approximate`, `unresolved`) from PDB/portable PDB; unresolved is a first-class result with `metadataToken` fallback — never silently dropped. *(AD-6)*
FR-7: LLM handoff is a separate, schema-governed, bounded-context artifact; it MUST NOT contain full IL dumps. *(AD-7)*
FR-8: Taint artifacts write under `.fennec/instrument/.../taint/<run-id>/`; cache keys include assembly identity + policy version hash + options fingerprint; `--no-cache` bypasses reuse. *(AD-8)*
FR-9: Existing `instrument` JSON contracts remain backward-compatible; taint outputs are additive in dedicated artifacts. *(AD-9)*
FR-10: Traversal caps at configurable `maxDepth` (default 8); assemblies > 10K methods emit a warning; analysis is cancellation-token-aware; performance diagnostics are always emitted. *(AD-10)*
FR-11: Both `result.json` and `llm-handoff.json` MUST carry `sourcesInventory`, `sinksInventory`, and `unmatchedRelevantExposures` regardless of matched-finding count. *(AD-11)*
FR-12: Context classifier detects app type (web-aspnet / worker-hosted-service / console / library / unknown) via assembly-reference scan + type scan + entry-point check; result recorded as `detectedContext` + `detectedContextConfidence`. Unknown context falls back to full catalog at uniform priority. *(AD-12)*
FR-13: Sink severity is adjusted per detected context and capability fingerprint; severity downgrades by maximum one level; sinks are never suppressed, minimum severity is `informational`. *(AD-13)*
FR-14: Capability fingerprinter emits 7 boolean dimensions; absent capability multiplies finding confidence by 0.25 with `confidenceAdjusted=true`. *(AD-14)*
FR-15: DI resolution attempts to map interface/abstract call edges to concrete types via five-tier priority (di-registration ≥0.90, known-hosting ≈0.85, type-hierarchy-unique 0.75, type-hierarchy-ambiguous 0.40, none 0.0); unresolvable edges retain `uncertain=true` and taint is suspended. Factory lambda registrations fall back to type-hierarchy in v1. *(AD-15)*
FR-16: Every assembly is classified as first-party, second-party, or third-party; only first-party and second-party IL is walked by default; third-party requires `--taint-include-third-party`; no prefix supplied = all NuGet is third-party. *(AD-16)*
FR-17: Analyzer accepts `.dll`, `.nupkg`, `.csproj`, `.sln`, and `.slnx` as input; when build outputs are absent for project inputs the tool fails with a clear hint. *(AD-17)*
FR-18: CLI exposes opt-in flags: `--taint`, `--taint-policy`, `--taint-max-depth`, `--taint-timeout`, `--taint-llm-handoff`, `--taint-include-third-party`, `--taint-second-party-prefix`. *(Companion — CLI surface)*

**Total FRs: 18**

---

## Non-Functional Requirements

NFR-1: For a fixed assembly + policy version + options, classification output is deterministic (byte-identical across reruns). *(AC-SS-1)*
NFR-2: Analysis on a 10K-method assembly completes or emits a structured warning within the configured timeout; never hangs silently. *(AC-PERF-1, AD-10)*
NFR-3: When `maxDepth` is hit, every affected finding records `depthCapHit=true`; the diagnostics block tallies the count. *(AC-PERF-2)*
NFR-4: `instrument` without `--taint` snapshot tests remain green; no output change. *(P1-1)*
NFR-5: `result.json` validates against `fennec.taint.result.v1` JSON Schema definition. *(P1-4)*
NFR-6: Taint artifact schema evolution follows SemVer; CI rejects breaking changes without a major version bump. *(Inherited AD-5)*
NFR-7: Taint contract shape is mode-agnostic and reusable for future hosted ingestion without schema changes. *(Inherited AD-6, AD-7)*
NFR-8: `unmatchedRelevantExposures` are shown in default terminal output. *(OQ-8 resolved)*
NFR-9: `fidelity=unresolved` findings are shown by default (no `--taint-verbose` gate). *(OQ-3 resolved)*
NFR-10: Severity model is hybrid: sink rule sets base severity; context/capability adjustment downgrades by max one level; confidence is a separate display field. *(OQ-2 resolved)*

**Total NFRs: 10**

---

## FR Coverage Map

| FR | Epic | Story |
|----|------|-------|
| FR-1 | Epic 1 | 1.1, 1.2 |
| FR-2 | Epic 1 | 1.1 |
| FR-8 | Epic 1 | 1.2 |
| FR-9 | Epic 1 | 1.2 |
| FR-17 | Epic 1 | 1.3 |
| FR-18 | Epic 1 | 1.1, 1.3 |
| NFR-4 | Epic 1 | 1.2 |
| NFR-5 | Epic 1 | 1.2 |
| FR-3 | Epic 2 | 2.1 |
| FR-4 | Epic 2 | 2.1 |
| FR-5 | Epic 2 | 2.2 |
| FR-2 | Epic 2 | 2.2 |
| FR-12 | Epic 2 | 2.3 |
| FR-13 | Epic 2 | 2.3 |
| FR-14 | Epic 2 | 2.3 |
| FR-15 | Epic 2 | 2.4 |
| FR-16 | Epic 2 | 2.4 |
| FR-11 | Epic 2 | 2.2 |
| NFR-1 | Epic 2 | 2.2 |
| NFR-10 | Epic 2 | 2.3 |
| FR-6 | Epic 3 | 3.1 |
| FR-7 | Epic 3 | 3.2 |
| NFR-8 | Epic 3 | 3.2 |
| NFR-9 | Epic 3 | 3.1 |
| FR-10 | Epic 4 | 4.1 |
| FR-8 | Epic 4 | 4.1 |
| NFR-2 | Epic 4 | 4.1 |
| NFR-3 | Epic 4 | 4.1 |
| NFR-6 | Epic 4 | 4.2 |
| NFR-7 | Epic 4 | 4.2 |

---

## Epic List

### Epic 1: Establish Taint Contracts and CLI Plumbing
Add opt-in taint CLI surface to `fennec instrument`, emit schema-valid empty taint artifacts behind the gate, and establish output/cache path conventions — all without changing existing instrument behavior.
**FRs covered:** FR-1, FR-2, FR-8, FR-9, FR-17, FR-18 | **NFRs:** NFR-4, NFR-5

### Epic 2: Core Taint Engine
Build the IL analysis engine: CFG extraction, call-graph construction, taint propagation with policy matching, context classification, capability fingerprinting, and DI resolution — producing deterministic findings.
**FRs covered:** FR-2..FR-5, FR-11..FR-16 | **NFRs:** NFR-1, NFR-10

### Epic 3: Symbol Correlation and LLM Handoff
Enrich findings with PDB-derived source location metadata and produce bounded LLM handoff artifacts.
**FRs covered:** FR-6, FR-7, FR-11 | **NFRs:** NFR-8, NFR-9

### Epic 4: Hardening and Hosted-Readiness
Add performance guardrails (depth caps, timeout, truncation), schema evolution CI gate, and ensure contract continuity for future hosted ingestion.
**FRs covered:** FR-8, FR-10 | **NFRs:** NFR-2, NFR-3, NFR-6, NFR-7

---

## Epic 1: Establish Taint Contracts and CLI Plumbing

### Story 1.1: Add taint opt-in CLI flags to instrument command

As a security engineer,
I want to run `fennec instrument --taint` without changing existing instrument output,
So that I can opt into taint analysis on any assembly without affecting current CI or automation.

**Implements:** FR-1, FR-9, FR-18, NFR-4

**Acceptance Criteria:**

**Given** an existing project using `fennec instrument` today
**When** the command runs without any `--taint` flags
**Then** stdout, JSON output, and `.fennec/instrument/` file layout are byte-identical to the pre-taint baseline

**Given** the CLI is updated with taint flags
**When** `fennec instrument --help` is run
**Then** the following flags are listed: `--taint`, `--taint-policy`, `--taint-max-depth`, `--taint-timeout`, `--taint-llm-handoff`, `--taint-include-third-party`, `--taint-second-party-prefix`

**Given** taint flags are provided but `--taint` is absent
**When** the command runs
**Then** taint analysis does not execute and no taint artifacts are written

### Story 1.2: Emit schema-valid taint artifact envelope on opt-in

As a maintainer,
I want `fennec instrument --taint` to produce a valid `taint/<run-id>/result.json` even before any analysis rules fire,
So that downstream consumers and CI can depend on a stable artifact shape from day one.

**Implements:** FR-1, FR-8, FR-9, NFR-4, NFR-5

**Acceptance Criteria:**

**Given** a fixture assembly and `fennec instrument --taint` is run
**When** the command completes
**Then** a `result.json` file exists under `.fennec/instrument/<scope>/taint/<run-id>/`
**And** the file validates against `fennec.taint.result.v1` JSON Schema
**And** it contains a canonical envelope with `$schema`, `schemaVersion`, `command`, `producedAt`, `producerVersion`, `sourceContext`, and `payload`

**Given** the same inputs run twice with identical options
**When** the second run executes
**Then** the `<run-id>` path is identical (cache hit)

**Given** `--no-cache` is added
**When** the command runs
**Then** a new `<run-id>` is generated and analysis re-executes

**Given** the standard instrument snapshot tests run without `--taint`
**When** tests complete
**Then** all snapshots remain green with no diff

### Story 1.3: Support .csproj, .sln, and .slnx as taint input targets

As a developer,
I want to pass a `.csproj` or `.sln` file to `fennec instrument --taint`,
So that I can analyze my project's IL directly without manually locating build output DLLs.

**Implements:** FR-17, FR-18

**Acceptance Criteria:**

**Given** a built `.csproj` and the command `fennec instrument --taint --filename MyApp.csproj`
**When** the command runs
**Then** the project assembly and all project-referenced assemblies are resolved from MSBuild output paths
**And** analysis proceeds as if the resolved DLLs were passed directly

**Given** a `.sln` file is passed as input
**When** the command runs
**Then** all solution project assemblies are resolved and analyzed with project references treated as first-party

**Given** a `.csproj` is passed but no build outputs exist
**When** the command runs
**Then** the command exits with a clear error message: "No build output found for <project>. Run `dotnet build` first, then re-run fennec."
**And** no partial artifacts are written

---

## Epic 2: Core Taint Engine

### Story 2.1: Build per-method CFG and inter-procedural call graph

As a taint engine,
I need a method-level CFG and a call graph over all analyzable assemblies,
So that taint propagation has structurally sound graph substrate to traverse.

**Implements:** FR-3, FR-4

**Acceptance Criteria:**

**Given** a fixture assembly with branch opcodes and exception handlers
**When** CFG extraction runs on a method body
**Then** every basic block has at least one instruction, a unique block ID, and consistent entry/exit edges
**And** `from`/`to` block IDs in edges both exist in the block list (AC-CFG-1)

**Given** a method with a call to an external NuGet type (no `MethodDefinition`)
**When** the call graph is built
**Then** the edge is recorded as unresolved with the callee reference and reason code
**And** the edge is NOT silently dropped (AC-CFG-2)

**Given** a virtual method call with multiple known overrides
**When** devirtualization runs
**Then** if ambiguous, all candidate types are recorded as unresolved candidates; if unique, resolved with `type-hierarchy-unique` basis

### Story 2.2: Policy-matched taint propagation with deterministic findings

As a security engineer,
I want the taint engine to propagate taint from sources through call chains to sinks using the versioned policy,
So that I receive deterministic, actionable findings for known vulnerability patterns.

**Implements:** FR-2, FR-5, FR-11, NFR-1

**Acceptance Criteria:**

**Given** a fixture with `HttpRequest.QueryString` read flowing into `SqlCommand.ctor`
**When** `fennec instrument --taint` runs
**Then** at least one finding is emitted with `category=sql-injection` and `policyRuleId=snk-sql-cmd` (P2-1, P2-2)

**Given** a fixture with `HtmlEncode` between the source and an XSS sink
**When** analysis runs
**Then** zero findings are emitted for that path (P2-3)

**Given** an API not present in the policy
**When** analysis runs
**Then** the call appears in `diagnostics.policyMisses`; no finding is emitted for it (P2-5)

**Given** the same binary + policy + options run three times consecutively
**When** outputs are compared
**Then** `result.json` is byte-identical across all three runs (P2-4, NFR-1)

**Given** analysis completes
**When** `result.json` is read
**Then** `payload.sourcesInventory`, `payload.sinksInventory`, and `payload.unmatchedRelevantExposures` are all present and non-null (FR-11)

### Story 2.3: Context classification, severity adjustment, and capability fingerprinting

As a security engineer,
I want findings to reflect the application's runtime context (web / worker / console) and capability evidence,
So that severity labels are relevant and low-probability findings are appropriately downgraded.

**Implements:** FR-12, FR-13, FR-14, NFR-10

**Acceptance Criteria:**

**Given** a fixture referencing `Microsoft.AspNetCore.*` with `ControllerBase` subclasses
**When** context classification runs
**Then** `detectedContext=web-aspnet` with `detectedContextConfidence > 0.7` (AC-CTX-1)

**Given** a fixture with no HttpClient usage and an SSRF sink in the policy
**When** capability fingerprinting runs
**Then** the SSRF finding carries `confidenceAdjusted=true` and `confidenceAdjustmentReason=sink-capability-absent`
**And** confidence is multiplied by 0.25 (AC-CAP-1)

**Given** a `web-aspnet` context and an XSS sink rule with base severity `high`
**When** context-adjusted severity is computed
**Then** severity remains `high` (in-context; no downgrade)

**Given** a `worker-hosted-service` context and the same XSS sink rule
**When** context-adjusted severity is computed
**Then** severity is downgraded by exactly one level to `medium`
**And** the finding is not suppressed

**Given** context detection finds no matching signals
**When** analysis runs
**Then** `detectedContext=unknown` and the full policy catalog is applied at uniform priority (OQ-7)

### Story 2.4: DI abstract-to-concrete resolution and ownership classification

As a security engineer,
I want taint to flow through interface/abstract call edges to the concrete implementations registered via DI,
So that vulnerabilities hidden behind interface abstractions are not silently missed.

**Implements:** FR-15, FR-16

**Acceptance Criteria:**

**Given** a fixture with `services.AddScoped<IUserService, UserService>()`
**When** DI resolution runs on a call edge to `IUserService.ProcessInput`
**Then** the resolved edge carries `resolutionBasis=di-registration` and `resolutionConfidence >= 0.90` (AC-DI-1)
**And** taint propagates through `UserService.ProcessInput` (AC-DI-2)

**Given** an interface with zero implementations in the loaded assemblies
**When** DI resolution runs
**Then** the edge carries `resolutionBasis=none`, `resolutionConfidence=0.0`, `uncertain=true`
**And** taint does NOT propagate across that edge (AC-DI-3)

**Given** a `.csproj` input referencing three NuGet packages and no `--taint-second-party-prefix`
**When** ownership classification runs
**Then** the project assembly is `first-party` and all NuGet assemblies are `third-party` (AC-OWN-1)

**Given** `--taint-second-party-prefix MyOrg.` is supplied and a `MyOrg.Core` package is present
**When** ownership classification runs
**Then** `MyOrg.Core` assemblies are classified `second-party` with `ilWalked=true`

**Given** no `--taint-include-third-party` flag
**When** a taint path would cross into a `third-party` assembly
**Then** propagation stops, finding records `propagationStopped=true` with reason `third-party-boundary` (AC-OWN-3)

---

## Epic 3: Symbol Correlation and LLM Handoff

### Story 3.1: PDB-derived source location mapping for findings

As a developer,
I want findings to include the source file and line number where the source and sink occur,
So that I can navigate directly to the vulnerable code without manually correlating IL offsets.

**Implements:** FR-6, NFR-9

**Acceptance Criteria:**

**Given** a fixture built with portable PDB
**When** symbol mapping runs on a finding's source endpoint
**Then** `symbolRef.fidelity=exact`, `symbolRef.file` contains the source file path, and `symbolRef.startLine` matches the actual line (P3-1, AC-SYM-1)

**Given** an instruction with no direct sequence point but a nearby non-hidden one
**When** symbol mapping runs
**Then** `symbolRef.fidelity=approximate` with `file` and `startLine` set; columns omitted

**Given** a fixture built without any PDB
**When** symbol mapping runs
**Then** `symbolRef.fidelity=unresolved`, `symbolRef.metadataToken` is present, file/line fields are absent, and `reason` carries `no-pdb` (P3-2, AC-SYM-2)

**Given** a finding with `fidelity=unresolved`
**When** results are displayed in terminal
**Then** the finding is shown by default without requiring `--taint-verbose` (NFR-9, OQ-3)

### Story 3.2: LLM handoff artifact generation

As a security engineer,
I want `fennec instrument --taint --taint-llm-handoff` to produce a bounded `llm-handoff.json`,
So that I can feed structured, focused taint context to an LLM for exploitability assessment without leaking full IL dumps.

**Implements:** FR-7, FR-11, NFR-8

**Acceptance Criteria:**

**Given** `--taint-llm-handoff` is supplied
**When** analysis completes
**Then** `taint/<run-id>/llm-handoff.json` is written alongside `result.json`

**Given** the handoff artifact is read
**When** any finding is inspected
**Then** it contains `title`, `severity`, `category`, `confidence`, `sourceRef`, `sinkRef`, `chainSummary`, and `suggestedInvestigationQuestions` (P3-4)
**And** `chainSummary` is a string array with ≤ 20 entries per finding (P3-3)
**And** no raw IL bytecode or full assembly dump is present

**Given** the artifact is read
**When** the top level is inspected
**Then** `sourcesInventory`, `sinksInventory`, and `unmatchedRelevantExposures` are present (FR-11)

**Given** `unmatchedRelevantExposures` has entries
**When** results are shown in terminal
**Then** the unmatched exposures are listed by default in a distinct section from matched findings (NFR-8, OQ-8)

**Given** `--taint-llm-handoff` is NOT supplied
**When** analysis completes
**Then** no `llm-handoff.json` is written

---

## Epic 4: Hardening and Hosted-Readiness

### Story 4.1: Performance guardrails and graceful degradation

As a CI engineer,
I want taint analysis to always complete within predictable bounds,
So that it can be added to CI pipelines without risk of hung jobs.

**Implements:** FR-10, NFR-2, NFR-3

**Acceptance Criteria:**

**Given** `--taint-timeout 30` is supplied and the assembly would take longer to analyze
**When** the timeout elapses
**Then** analysis terminates and a partial `result.json` is written with `partial=true` in diagnostics
**And** the elapsed analysis time is < 35 seconds (P4-1)

**Given** a fixture with a call chain longer than `--taint-max-depth 2`
**When** propagation hits the cap
**Then** every affected finding has `depthCapHit=true`
**And** `diagnostics.depthCapHits` tallies the count (AC-PERF-2, P2-6)

**Given** an assembly with > 10,000 analyzable methods
**When** analysis runs
**Then** a warning is emitted to stderr; analysis continues and never hangs silently (AC-PERF-1, P4-2)

**Given** 501 findings are detected before truncation
**When** the artifact is written
**Then** only 500 findings are included and `diagnostics.findingsTruncated=true`

**Given** any run completes (normal, partial, or capped)
**When** the diagnostics block is read
**Then** `elapsedMs.cfgBuild`, `elapsedMs.policyMatch`, `elapsedMs.taintPropagation`, `elapsedMs.symbolMapping`, `elapsedMs.diResolution`, `assembliesWalked`, and `assembliesBoundarySkipped` are all present

### Story 4.2: Schema versioning CI gate and hosted contract continuity

As a maintainer,
I want breaking taint schema changes to fail CI without a corresponding major version bump,
So that downstream consumers (automation, LLM tooling, future hosted mode) are never silently broken.

**Implements:** NFR-6, NFR-7

**Acceptance Criteria:**

**Given** a PR that removes a required field from `fennec.taint.result.v1`
**When** the schema CI gate runs
**Then** the build fails with a message indicating a breaking schema change requires a major version bump (P4-3)

**Given** a PR that adds an optional field to the payload
**When** the schema CI gate runs
**Then** the build passes (additive change = minor version)

**Given** the taint `result.json` schema and a hypothetical hosted ingestion adapter contract
**When** the field set is compared
**Then** the taint schema is a proper superset of all fields the hosted adapter requires (P4-4)
**And** no field is required in the hosted contract that is absent or nullable in the taint schema
