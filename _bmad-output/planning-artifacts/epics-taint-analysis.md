---
stepsCompleted:
  - step-01-requirements-extracted
  - step-02-epics-approved
  - step-03-stories-generated
inputDocuments:
  - _bmad-output/planning-artifacts/architecture/architecture-Fennec.Labs-taint-analysis-2026-07-30/ARCHITECTURE-SPINE.md
  - _bmad-output/planning-artifacts/architecture/architecture-Fennec.Labs-taint-analysis-2026-07-30/TAINT-ANALYSIS-COMPANION.md
  - _bmad-output/planning-artifacts/architecture/architecture-Fennec.Labs-2026-07-22/ARCHITECTURE-SPINE.md
feature: taint-analysis
created: '2026-07-31'
updated: '2026-07-31'
---

# Fennec.Labs Taint Analysis — Epic Breakdown

## Overview

This document decomposes the taint-analysis feature requirements (derived from AD-1..AD-17 in the taint architecture spine and companion) into four phased epics and detailed implementable stories. Each story is scoped for a single developer agent session. Input documents have no PRD (the architecture spine is the requirements contract) and no UX design document (CLI-only feature).

---

## Requirements Inventory

### Functional Requirements

FR1: When `fennec instrument` is invoked without `--taint`, behavior and output MUST be byte-identical to the current baseline. (AD-1, AD-9)
FR2: `fennec instrument` MUST accept `--taint`, `--taint-policy`, `--taint-max-depth`, `--taint-llm-handoff`, `--taint-timeout`, `--taint-include-third-party`, `--taint-second-party-prefix` flags. (AD-1, AD-10, AD-17)
FR3: The instrument command MUST accept `.csproj`, `.sln`, `.slnx` inputs in addition to `.dll`/`.nupkg`; build outputs MUST be resolved via MSBuild project graph traversal. (AD-17)
FR4: Every assembly in the analysis graph MUST be classified as `first-party` (direct project output), `second-party` (transitive `<ProjectReference>`), or `third-party` (`<PackageReference>`); `classificationReason` and `ilWalked` MUST be recorded in `assemblyManifest[]`. (AD-16)
FR5: Third-party assembly IL MUST NOT be walked by default; `--taint-include-third-party` enables opt-in; `--taint-second-party-prefix` (repeatable) promotes NuGet packages to second-party. (AD-16, AD-17)
FR6: A versioned policy contract (`fennec.taint.policy.v1`) MUST define sources, sinks, propagators, and sanitizers with `category`, `severity`, `confidence`; unmatched APIs MUST be classified `unknown`. (AD-2)
FR7: The analysis MUST extract method-level CFGs (basic blocks from branch opcodes + exception boundaries) for all walked assembly methods. (AD-3)
FR8: The analysis MUST build an inter-procedural call graph with resolved edges (callee MethodDefinition present) and unresolved edges (external, virtual, delegate/Ldftn); uncertainty MUST propagate forward into findings. (AD-4)
FR9: Taint MUST propagate through a four-state machine (`untainted → tainted → sanitized | unknown`) bounded by `maxDepth=5` default (overridable via `--taint-max-depth`); depth-cap hits MUST produce `depthCapHit=true` in findings. (AD-5, AD-10)
FR10: Interface and abstract-type call edges MUST be resolved to concrete types via 5-tier priority (di-registration ≥0.90 → known-hosting ≈0.85 → type-hierarchy-unique 0.75 → type-hierarchy-ambiguous 0.40 → none 0.0); each edge MUST record `declaredContractType`, `resolvedConcreteTypes[]`, `resolutionBasis`, `resolutionConfidence`. (AD-15)
FR11: Assembly execution context MUST be classified (`web-aspnet`, `worker-hosted-service`, `console`, `library`, `unknown`) before policy application; `detectedContext` and `detectedContextConfidence` MUST be included in all artifacts. (AD-12)
FR12: The policy engine MUST apply per-context severity weights; irrelevant sinks MUST be downgraded (not suppressed), minimum `informational`. (AD-13)
FR13: A capability fingerprint with 7 boolean dimensions (`httpEgress`, `fileIo`, `processLaunch`, `serialization`, `databaseAccess`, `queueOrBus`, `networkListen`) MUST be derived from call-site inventory; absent capabilities MUST reduce finding confidence by 0.25× with `confidenceAdjusted=true` and reason code. (AD-14)
FR14: Both `result.json` and `llm-handoff.json` MUST include complete `sourcesInventory[]`, `sinksInventory[]`, and `unmatchedRelevantExposures[]` regardless of whether matched paths exist. (AD-11)
FR15: Findings MUST be mapped to source file/line using PDB/Portable PDB via Mono.Cecil SequencePoints; fidelity MUST be `exact`, `approximate`, or `unresolved`; unresolved MUST include `metadataToken` fallback; findings are never dropped due to missing symbols. (AD-6)
FR16: A separate `llm-handoff.json` MUST be produced when `--taint-llm-handoff` is set; it MUST NOT contain full IL dumps; chain summaries MUST be ≤20 string entries; each finding MUST include `suggestedInvestigationQuestions` and `diResolutionContext[]`. (AD-7)
FR17: All taint artifacts MUST be written under `.fennec/instrument/.../taint/<run-id>/` where `run-id = sha256(assembly-identity + policy-version + options-fingerprint)[:12]`; `--no-cache` MUST bypass cache reuse and force recomputation. (AD-8)
FR18: Existing `instrument` JSON output contracts MUST remain unchanged; taint is purely additive. (AD-9)
FR19: Performance diagnostics MUST be emitted in `result.json` diagnostics block: elapsed ms per phase, skipped method count, depth-cap hit count, `assembliesWalked`, `assembliesBoundarySkipped`, `abstractionEdgesResolved`, `abstractionEdgesUnresolved`. (AD-10)
FR20: When taint propagation halts at a third-party ownership boundary, findings MUST include `propagationStopped=true` and `propagationStopReason="third-party-boundary"`. (AD-16)

### Non-Functional Requirements

NFR1: Analysis on a 10,000-method assembly MUST complete or produce a structured warning within the configured `--taint-timeout` (default 120 s); analysis MUST never hang silently.
NFR2: Same binary + same policy + same options MUST produce byte-identical `result.json` in 3 successive runs (determinism).
NFR3: `result.json` MUST validate against `fennec.taint.result.v1` JSON Schema; `llm-handoff.json` against `fennec.taint.llm-handoff.v1`.
NFR4: All taint artifact schemas MUST follow SemVer governance; a CI gate MUST reject breaking changes without a major version bump.
NFR5: Same inputs MUST produce the same `<run-id>` path; `--no-cache` MUST bypass artifact reuse.
NFR6: Analysis MUST be cancellation-token-aware; `--taint-timeout` sets a hard deadline producing a partial artifact with `partial=true` in diagnostics.

### Additional Requirements (Architecture)

- New `FennecLabs.TaintAnalysis/` project must be created, referencing `Mono.Cecil` and `FennecLabs.Contracts`.
- Contract types to add to `FennecLabs.Contracts`: `TaintPayload`, `TaintFinding`, `AssemblyManifestEntry`, `TaintDiagnostics`, `TaintLlmHandoffPayload`, `CapabilityFingerprint`, `SourceRef`, `TaintSourceInventoryItem`, `TaintSinkInventoryItem`, `TaintUnmatchedExposure`.
- `OutputCache.TaintPath(root, scope, runId)` helper to be added to `FennecLabs.Cli/OutputCache.cs`.
- `InstrumentCommandHandler.cs` and `Program.cs` updated to wire taint flags using existing `SetAction` / `System.CommandLine` patterns.
- Taint `result.json` MUST be wrapped in `DashboardArtifactEnvelope<TaintPayload>` (inherited invariant from parent spine AD-1).
- Five fixture projects in `test/TestProjects/TaintFixtures/`: `TaintFixture.WebAspNet`, `TaintFixture.WorkerService`, `TaintFixture.ConsoleApp`, `TaintFixture.DiAbstraction`, `TaintFixture.MultiProjectSln`.
- No starter template — brownfield addition to existing CLI project.

### UX Design Requirements

N/A — CLI-only feature; no visual UI.

---

### FR Coverage Map

FR1: Epic 1 — backward-compatible gate; snapshot regression tests
FR2: Epic 1 — CLI flag registration and parsing
FR3: Epic 1 — project/solution input handling, `BuildGraphReader`
FR4: Epic 1 — `OwnershipClassifier`, `assemblyManifest` schema
FR5: Epic 1 — third-party opt-in flag; second-party prefix config
FR6: Epic 2 — `TaintPolicyLoader`
FR7: Epic 2 — `CfgBuilder`
FR8: Epic 2 — `CallGraphBuilder`
FR9: Epic 2 — `TaintPropagator` (depth cap, state machine)
FR10: Epic 2 — `DiResolver`
FR11: Epic 2 — `ContextClassifier`
FR12: Epic 2 — policy context weights in `TaintPropagator`
FR13: Epic 2 — `CapabilityFingerprinter`
FR14: Epic 2 — `FindingCollector` inventories
FR15: Epic 3 — `SymbolMapper`
FR16: Epic 3 — `LlmHandoffSerializer`
FR17: Epic 1 — `TaintPath` helper; deterministic run-id
FR18: Epic 1 — backward-compatibility snapshot tests
FR19: Epic 4 — diagnostics block completeness; timing instrumentation
FR20: Epic 2 — `FindingCollector` boundary-stop propagation
NFR1: Epic 4 — timeout and partial artifact
NFR2: Epic 1 — determinism tests
NFR3: Epic 4 — JSON Schema CI gate
NFR4: Epic 4 — schema SemVer CI gate
NFR5: Epic 1 — cache key stability tests
NFR6: Epic 4 — cancellation-token wiring

---

## Epic List

### Epic 1: Taint Analysis Foundation
Developers can enable taint analysis on any project/solution input with confidence that existing `instrument` behavior is unchanged and that the new flags produce schema-valid taint artifacts in the correct output location with a deterministic cache key.
**FRs covered:** FR1, FR2, FR3, FR4, FR5, FR17, FR18, NFR2, NFR5

### Epic 2: Core Taint Detection Engine
Security engineers can run `fennec instrument --taint` on a project or solution and receive a `result.json` containing real source→sink findings with full source/sink inventories, execution-context classification, capability fingerprint, and DI-resolved call chains — ready for human triage.
**FRs covered:** FR6, FR7, FR8, FR9, FR10, FR11, FR12, FR13, FR14, FR19, FR20

### Epic 3: Source Correlation and LLM Handoff
Developers can correlate every taint finding to a specific source file and line number when PDB symbols are available; security engineers can produce a bounded `llm-handoff.json` artifact for AI-assisted investigation with complete context, DI resolution evidence, and investigation prompts.
**FRs covered:** FR15, FR16, FR14 (handoff inventory section)

### Epic 4: Hardening, Performance, and Schema Governance
CI pipelines and developers can rely on taint analysis completing within predictable time and resource bounds, producing deterministic artifacts that are schema-governed, cache-safe, and ready for future hosted adapter integration.
**FRs covered:** FR19 (full diagnostics), NFR1, NFR2, NFR3, NFR4, NFR5, NFR6

---

## Epic 1: Taint Analysis Foundation

Developers can enable taint analysis on any project/solution input with confidence that existing `instrument` behavior is unchanged and new flags produce schema-valid, correctly cached taint artifacts.

### Story 1.1: Add taint flags and project-input types to `fennec instrument`

As a developer,
I want `fennec instrument` to accept taint-specific flags and project/solution file inputs,
So that I can enable optional taint analysis without affecting the existing instrument workflow.

**Acceptance Criteria:**

**Given** `fennec instrument MyAssembly.dll` is run without any `--taint` flag
**When** the command executes
**Then** the output is byte-identical to the baseline before this story; no taint artifacts are created; exit code is unchanged

**Given** `fennec instrument --taint MyApp.csproj` is run
**When** the command executes
**Then** the system resolves the MSBuild build output DLL for `MyApp.csproj` and proceeds with analysis; no error about unsupported input type

**Given** `fennec instrument --taint MyApp.sln` is run
**When** the command executes
**Then** the system enumerates all projects in the solution and resolves their build output DLLs

**Given** `fennec instrument --taint MyApp.slnx` is run
**When** the command executes
**Then** the system parses the `.slnx` XML format and resolves all project build outputs

**Given** `fennec instrument --taint --taint-max-depth 3 --taint-timeout 60 --taint-policy ./my-policy.json --taint-include-third-party --taint-second-party-prefix MyOrg. MyApp.csproj` is run
**When** the command parses arguments
**Then** all flags are parsed without error; `--help` output lists all new taint flags with their defaults; all existing flags remain functional

**Given** a `.csproj` input where the project has not been built (no `bin/` output)
**When** `fennec instrument --taint MyApp.csproj` is run
**Then** the command fails with a clear, actionable error message indicating the project must be built before analysis; exit code is non-zero

**Technical notes:** Update `Program.cs` (new `Option<>` registrations on the `instrument` command) and `InstrumentCommandHandler.cs` (new `BuildGraphReader` class in `FennecLabs.TaintAnalysis` to resolve project output paths via `dotnet build --getProperty:OutputPath` or `bin/` scan fallback). New flags: `--taint` (bool, default false), `--taint-policy` (string?, default null), `--taint-max-depth` (int, default 5), `--taint-llm-handoff` (bool, default false), `--taint-timeout` (int, default 120), `--taint-include-third-party` (bool, default false), `--taint-second-party-prefix` (string[], repeatable).

---

### Story 1.2: Implement assembly ownership classification via project graph

As a developer,
I want every assembly in the analysis graph to be classified as first-party, second-party, or third-party based on the MSBuild project reference graph,
So that only owned code is analyzed by default, and the artifact clearly shows which assemblies were walked.

**Acceptance Criteria:**

**Given** a `.csproj` input with three referenced assemblies: one project output (first-party), one transitive `<ProjectReference>` (second-party), and one `<PackageReference>` NuGet package (third-party)
**When** `fennec instrument --taint` runs
**Then** `result.json` `payload.assemblyManifest` contains all three entries with correct `ownershipTier` values (`first-party`, `second-party`, `third-party`)
**And** the first-party and second-party entries have `ilWalked=true`; the third-party entry has `ilWalked=false`
**And** `classificationReason` values are `direct-project-output`, `project-reference-transitive`, and `nuget-package-cache` respectively

**Given** `--taint-include-third-party` is set
**When** the same run executes
**Then** the third-party assembly entry has `ilWalked=true`

**Given** `--taint-second-party-prefix MyOrg.` is set and a NuGet package `MyOrg.Shared.dll` is referenced
**When** the run executes
**Then** `MyOrg.Shared.dll` has `ownershipTier=second-party`, `classificationReason=nuget-second-party-prefix`, `ilWalked=true`

**Given** a `.nupkg` file is the input
**When** the run executes
**Then** all DLLs inside the package have `ownershipTier=first-party`, `classificationReason=nupkg-input`, `ilWalked=true`

**Technical notes:** Implement `OwnershipClassifier` in `FennecLabs.TaintAnalysis`. Classification order: (1) direct project output → `first-party`; (2) transitive `<ProjectReference>` closure → `second-party`; (3) `<PackageReference>` assemblies → `third-party`; (4) `--taint-second-party-prefix` matches promote NuGet from third to second. Path heuristics for `.dll` inputs: `/packages/` or `/.nuget/` in path → third-party; `/bin/` under project → first-party; otherwise third-party.

---

### Story 1.3: Add taint contract types and produce a schema-valid empty artifact

As a developer,
I want running `fennec instrument --taint` to produce a schema-valid `result.json` even before any taint engine logic is implemented,
So that the artifact infrastructure is in place and testable end-to-end.

**Acceptance Criteria:**

**Given** `fennec instrument --taint MyApp.dll` is run on any valid .NET assembly
**When** the command completes (with no taint engine yet)
**Then** a `result.json` file is created at `.fennec/instrument/<scope>/taint/<run-id>/result.json`
**And** the file validates against the `fennec.taint.result.v1` JSON Schema
**And** the `payload.findings` array is empty
**And** `payload.assemblyManifest` contains at least the input assembly entry
**And** the file is wrapped in `DashboardArtifactEnvelope<TaintPayload>` with correct `$schema`, `schemaVersion`, `command="instrument"`, and `producedAt` fields

**Given** `fennec instrument --taint --taint-llm-handoff MyApp.dll` is run
**When** the command completes
**Then** a `llm-handoff.json` is also created at `.fennec/instrument/<scope>/taint/<run-id>/llm-handoff.json`
**And** it validates against the `fennec.taint.llm-handoff.v1` JSON Schema

**Technical notes:** Add contract types to `FennecLabs.Contracts`: `TaintPayload`, `TaintFinding`, `AssemblyManifestEntry`, `TaintDiagnostics`, `TaintLlmHandoffPayload`, `CapabilityFingerprint`, `SourceRef`, `TaintSourceInventoryItem`, `TaintSinkInventoryItem`, `TaintUnmatchedExposure`. Wrap `TaintPayload` in existing `DashboardArtifactEnvelope<T>`. Write JSON Schema definition files (`fennec.taint.result.v1.schema.json`, `fennec.taint.llm-handoff.v1.schema.json`) in `src/FennecLabs.Contracts/schemas/`.

---

### Story 1.4: Implement `TaintPath` cache helper and deterministic run-id

As a developer,
I want taint artifacts to be written to a deterministic path derived from assembly identity, policy version, and analysis options,
So that repeated identical runs reuse cached results and `--no-cache` forces recomputation.

**Acceptance Criteria:**

**Given** `fennec instrument --taint MyApp.dll` is run twice with identical inputs and no policy file changes
**When** both runs complete
**Then** both runs produce the same `<run-id>` (first 12 hex chars of SHA-256 of assembly-identity + policy-version + options-fingerprint)
**And** the second run reuses the cached artifact without rerunning analysis; a cache-hit indicator appears in CLI output

**Given** `fennec instrument --taint --no-cache MyApp.dll` is run
**When** the run completes
**Then** the artifact is regenerated even if a matching `<run-id>` path already exists
**And** the `result.json` `producedAt` timestamp is updated

**Given** `--taint-max-depth 3` is set vs. the default
**When** both variants run on the same assembly
**Then** they produce different `<run-id>` values and different output paths

**Technical notes:** Add `OutputCache.TaintPath(root, scope, runId)` to `FennecLabs.Cli/OutputCache.cs`. `run-id = sha256(assemblyMvid + ":" + policyVersion + ":" + maxDepth + ":" + includeThirdParty + ":" + secondPartyPrefixes.sorted.joined(",") + ":" + llmHandoff)[:12]`.

---

### Story 1.5: Snapshot regression tests for existing `instrument` behavior

As a developer,
I want automated snapshot tests that prove `fennec instrument` output is unchanged when `--taint` is not specified,
So that any future change that inadvertently breaks the existing contract is caught immediately.

**Acceptance Criteria:**

**Given** snapshot test suite runs `instrument` on each existing test fixture without `--taint`
**When** tests execute
**Then** all tests pass with byte-identical output compared to the committed baseline snapshots

**Given** the story 1.1–1.4 changes are merged
**When** the existing `dotnet test` suite runs
**Then** zero existing tests regress; all taint-related tests are new additions

**Given** `--taint` is added to an existing snapshot fixture run
**When** the test executes
**Then** the existing instrument output files are unchanged; only the new taint artifact files are added

**Technical notes:** Use the existing `FennecLabs.Cli.Tests` snapshot infrastructure. Add snapshot baseline files for 3 existing fixture assemblies. Assert that `.fennec/instrument/<scope>/result.json` (existing) is byte-identical. Taint-specific new files under `.../taint/<run-id>/` are verified separately in new tests.

---

## Epic 2: Core Taint Detection Engine

Security engineers can run `fennec instrument --taint` on a project or solution and receive a `result.json` with real source→sink findings, full source/sink inventories, execution-context classification, capability fingerprint, and DI-resolved call chains.

### Story 2.1: Implement `TaintPolicyLoader`

As a developer,
I want a policy loader that parses and validates the built-in `fennec.taint.policy.v1` policy and allows user-supplied policy files to merge additional rules,
So that the taint engine has a typed, validated rule set to work from at runtime.

**Acceptance Criteria:**

**Given** the built-in v1 policy JSON is loaded
**When** `TaintPolicyLoader.Load()` executes
**Then** the result contains typed source, sink, propagator, and sanitizer rule collections with correct `id`, `kind`, `assembly`, `typeName`, `memberName`, `category`, `severity`, `confidence` values

**Given** a user policy file at `--taint-policy ./custom.json` contains additional rules with new ids
**When** the loader merges it with the built-in policy
**Then** the new rules are appended; existing rules with the same `id` are replaced by the user version

**Given** a malformed policy file is supplied
**When** the loader attempts to parse it
**Then** a clear validation error is returned with file path and field name; analysis does not start

**Given** a rule with `kind=source` and matching `(assembly, typeName, memberName)` tuple
**When** policy lookup is performed for a call site
**Then** the rule is found regardless of casing differences in any field

**Technical notes:** Implement `TaintPolicyLoader` in `FennecLabs.TaintAnalysis`. Policy merge: new `id` → append; existing `id` → user rule wins. Context-aware severity overrides: each rule may carry a `contextOverrides` dictionary keyed by context identifier. Matching is case-insensitive on `(assembly, typeName, memberName)`.

---

### Story 2.2: Implement `ContextClassifier`

As a security engineer,
I want the analysis to automatically classify the assembly's execution context before applying taint rules,
So that web-centric threats are prioritized for ASP.NET apps and different threat models apply to console or worker applications.

**Acceptance Criteria:**

**Given** `TaintFixture.WebAspNet` is analyzed
**When** `ContextClassifier.Classify()` runs
**Then** `detectedContext="web-aspnet"` with `detectedContextConfidence ≥ 0.85`

**Given** `TaintFixture.WorkerService` is analyzed
**When** `ContextClassifier.Classify()` runs
**Then** `detectedContext="worker-hosted-service"` with `detectedContextConfidence ≥ 0.80`

**Given** `TaintFixture.ConsoleApp` is analyzed
**When** `ContextClassifier.Classify()` runs
**Then** `detectedContext="console"` with `detectedContextConfidence ≥ 0.85`

**Given** a pure class library DLL with no entry point and no hosting references
**When** `ContextClassifier.Classify()` runs
**Then** `detectedContext="library"` or `detectedContext="unknown"`

**Given** classification succeeds
**When** `result.json` is written
**Then** `payload.detectedContext` and `payload.detectedContextConfidence` are present and match classifier output

**Technical notes:** Implement `ContextClassifier` in `FennecLabs.TaintAnalysis`. Heuristic weights: (1) assembly reference scan for `Microsoft.AspNetCore.*` / `System.Web` → +60 pts web; `Microsoft.Extensions.Hosting` without web → +50 pts worker; (2) type scan for `IHostedService`/`BackgroundService` implementors → +40 pts worker; controllers/middleware → +40 pts web; (3) entry-point check: `OutputType=Exe` with no web/hosting → +60 pts console. Normalize to confidence ∈ [0,1].

---

### Story 2.3: Implement `CapabilityFingerprinter`

As a security engineer,
I want the analysis to derive a capability fingerprint from observed call-site inventory,
So that findings for capabilities not present in the assembly are automatically de-weighted, reducing false-high-severity noise.

**Acceptance Criteria:**

**Given** `TaintFixture.ConsoleApp` (no HTTP client references) is fingerprinted
**When** `CapabilityFingerprinter.Fingerprint()` runs
**Then** `capabilityFingerprint.httpEgress=false`; `fileIo=true`; `processLaunch=true`; `databaseAccess=false`

**Given** `TaintFixture.WebAspNet` (has `HttpClient` and `SqlCommand` references) is fingerprinted
**When** the result is produced
**Then** `capabilityFingerprint.httpEgress=true`; `databaseAccess=true`

**Given** a finding's sink rule requires `httpEgress` capability and the fingerprint has `httpEgress=false`
**When** the finding's confidence is calculated
**Then** `finding.confidenceAdjusted=true`; `finding.confidence` is 0.25× the base policy confidence; `finding.confidenceAdjustmentReason` is present

**Given** the fingerprint is produced
**When** `result.json` is written
**Then** `payload.capabilityFingerprint` contains all 7 boolean dimensions

**Technical notes:** The 7 dimensions and their detection call patterns are specified in the companion's capability fingerprinting section. Implement `CapabilityFingerprinter` in `FennecLabs.TaintAnalysis`.

---

### Story 2.4: Implement `CfgBuilder` — method-level control-flow graph extraction

As a developer,
I want the taint engine to extract a basic-block CFG for every analyzed method,
So that the `TaintPropagator` has a precise intra-procedural data-flow substrate to traverse.

**Acceptance Criteria:**

**Given** a method body with a conditional branch (e.g., `if/else`)
**When** `CfgBuilder.Build(method)` runs
**Then** the resulting CFG has at least 3 blocks (condition, true-branch, false-branch/merge); all block IDs referenced in edges exist as block nodes; entry block is identified

**Given** a method with a try/catch block
**When** CFG is built
**Then** exception-handling edges are represented with `kind="exceptional"`

**Given** a method with `Call`/`Callvirt`/`Newobj`/`Ldftn` instructions
**When** CFG is built
**Then** each such instruction appears as a call-site anchor in the block's call-site list with the callee `MethodReference` recorded

**Given** a method with no body (abstract, extern, or interface member)
**When** `CfgBuilder.Build(method)` is called
**Then** no CFG is produced and the method is recorded in `methodsSkipped` diagnostics counter

**Technical notes:** Implement `CfgBuilder` in `FennecLabs.TaintAnalysis`. Use Mono.Cecil `MethodBody.Instructions` and branch target analysis. Block-start set = all branch targets + instruction after branch + exception-handler start offsets. CFG node = `{ blockId, instructionOffsets[], callSites[], successors[] }`.

---

### Story 2.5: Implement `CallGraphBuilder` with basic call resolution

As a developer,
I want the taint engine to build a call graph from CFG call-site anchors, distinguishing resolved edges from unresolved ones,
So that the propagator can traverse inter-procedural call chains and track uncertainty.

**Acceptance Criteria:**

**Given** a method calls another method in the same loaded assembly
**When** `CallGraphBuilder.Build()` runs
**Then** a directed resolved edge `(caller, callee)` exists in the call graph

**Given** a method calls an external method (not in any loaded module)
**When** the call graph is built
**Then** an unresolved edge is recorded with `reason="external-target"` and the callee `MethodReference` is preserved for policy matching

**Given** a virtual dispatch call (`callvirt`) to a type with multiple overriding implementations
**When** the call graph is built
**Then** the edge is recorded as unresolved with `reason="virtual-dispatch-ambiguous"` unless devirtualization succeeds

**Given** a `Ldftn` or `Ldvirtftn` opcode (delegate pointer)
**When** the call graph is built
**Then** an unresolved edge is recorded with `reason="delegate-indirect"`

**Given** the call graph is complete
**When** `diagnostics.unresolvedCallEdges` is read
**Then** the count matches the number of unresolved edges actually recorded in the graph

**Technical notes:** Implement `CallGraphBuilder` in `FennecLabs.TaintAnalysis`. Resolved edge: callee `MethodReference.Resolve()` returns a `MethodDefinition` in a loaded module. Virtual devirtualization: collect type hierarchy, if exactly one non-abstract override exists → treat as resolved (confidence 0.75). Limit devirtualization to cases with ≤5 override candidates (performance guardrail).

---

### Story 2.6: Implement `DiResolver` — DI abstraction-to-concrete mapping

As a security engineer,
I want interface and abstract call edges in the call graph to be resolved to concrete implementations using static binary evidence,
So that taint propagation correctly traverses DI-injected service boundaries and sinks are not missed when they only appear in concrete types.

**Acceptance Criteria:**

**Given** an assembly containing `services.AddScoped<IUserService, UserService>()` in IL
**When** `DiResolver.Enrich(callGraph)` runs
**Then** the call edge to `IUserService` method has `resolutionBasis="di-registration"`, `resolutionConfidence ≥ 0.90`, `resolvedConcreteTypes=["UserService"]`

**Given** an interface with exactly one non-abstract implementing type (but no ServiceCollection registration)
**When** `DiResolver.Enrich(callGraph)` runs
**Then** the edge has `resolutionBasis="type-hierarchy-unique"`, `resolutionConfidence=0.75`

**Given** an interface with multiple implementing types and no registration
**When** `DiResolver.Enrich(callGraph)` runs
**Then** the edge has `resolutionBasis="type-hierarchy-ambiguous"`, `resolutionConfidence=0.40`, `resolvedConcreteTypes` contains all candidates

**Given** an interface with zero implementing types
**When** `DiResolver.Enrich(callGraph)` runs
**Then** the edge has `resolutionBasis="none"`, `resolutionConfidence=0.0`; taint is suspended at this edge with `uncertain=true`

**Given** enrichment completes
**When** diagnostics are written
**Then** `abstractionEdgesResolved` and `abstractionEdgesUnresolved` counts are present and correct

**Technical notes:** Implement `DiResolver` in `FennecLabs.TaintAnalysis`. Scan all `MethodBody` IL in loaded modules for `callvirt`→`IServiceCollection.AddSingleton/AddScoped/AddTransient/AddHostedService` with generic type arguments; extract `(serviceType, implementationType)` pairs. Type-hierarchy scan: `ModuleDefinition.Types` where `type.Interfaces.Any(i => i.InterfaceType == contractType)`.

---

### Story 2.7: Implement `TaintPropagator` with state machine and context-aware scoring

As a security engineer,
I want the taint engine to propagate taint from sources to sinks through the enriched call graph, respecting ownership boundaries, depth caps, sanitizers, and context-adjusted confidence,
So that findings represent real, plausible attack paths with accurate severity and confidence scores.

**Acceptance Criteria:**

**Given** a source call site (policy rule `kind=source`) is found in a first-party method
**When** `TaintPropagator.Propagate()` runs with `maxDepth=5`
**Then** taint propagates forward through outgoing edges up to 5 inter-procedural levels deep; a finding is emitted when a sink is reached within the depth cap

**Given** a propagation path crosses a sanitizer (policy rule `kind=sanitizer`)
**When** propagation encounters the sanitizer call site
**Then** taint state transitions to `sanitized`; no sink finding is emitted for that path

**Given** propagation reaches the `maxDepth` limit without finding a sink
**When** the partial path is recorded
**Then** `finding.uncertaintyFlags.depthCapHit=true`; `diagnostics.depthCapHits` is incremented

**Given** taint propagation reaches a third-party assembly boundary (`ilWalked=false`)
**When** propagation processes the boundary edge
**Then** propagation stops; if a sink policy rule matches the boundary call site directly, a finding is emitted with `propagationStopped=true, propagationStopReason="third-party-boundary"`; if no sink rule matches, the call site is recorded in `unmatchedRelevantExposures`

**Given** a finding's sink matches a context-irrelevant category (e.g., XSS sink in `console` context)
**When** context severity weights are applied
**Then** the finding's `contextAdjustedSeverity` is downgraded to at most `low`; the finding is still emitted (not suppressed)

**Given** `TaintFixture.WebAspNet` is analyzed
**When** propagation completes
**Then** at least one finding with `category=sql-injection, severity=high` is produced (HttpRequest.QueryString → SqlCommand)

**Technical notes:** Implement `TaintPropagator` in `FennecLabs.TaintAnalysis`. BFS traversal: queue starts with all source call sites; each dequeue step propagates through CFG intra-procedurally then crosses outgoing call graph edges; state per path = `{method, taintState, depth, chain[]}`. Apply `CancellationToken` checks at every queue iteration.

---

### Story 2.8: Implement `FindingCollector` with full source/sink inventories

As a security engineer,
I want the finding collector to produce a complete `result.json` payload including all matched findings, all discovered sources, all discovered sinks, and unmatched relevant exposures,
So that the artifact gives a complete attack-surface picture, not just confirmed paths.

**Acceptance Criteria:**

**Given** analysis completes on `TaintFixture.WebAspNet`
**When** `result.json` is written
**Then** `payload.sourcesInventory` contains at least one entry with `category=network-input`
**And** `payload.sinksInventory` contains at least two entries (SqlCommand and Process.Start)
**And** `payload.findings` contains at least one confirmed finding with `category=sql-injection`
**And** `payload.unmatchedRelevantExposures` contains `Process.Start` (no tainted path found)

**Given** analysis of `TaintFixture.WorkerService`
**When** `result.json` is written
**Then** `payload.sourcesInventory` includes `Environment.GetEnvironmentVariable` source entry
**And** at least one finding with `category=command-injection, severity=critical` is present

**Given** analysis of `TaintFixture.ConsoleApp`
**When** `result.json` is written
**Then** `payload.capabilityFingerprint.httpEgress=false`; any finding with an HTTP-egress-dependent sink carries `confidenceAdjusted=true`

**Given** any analysis run
**When** `result.json` is written
**Then** `payload.diagnostics` contains all required fields: `totalMethodsAnalyzed`, `methodsSkipped`, `assembliesWalked`, `assembliesBoundarySkipped`, `unresolvedCallEdges`, `abstractionEdgesResolved`, `abstractionEdgesUnresolved`, `depthCapHits`, `policyMisses`, `findingsTruncated`, `partial`, `elapsedMs.*`

**Technical notes:** Implement `FindingCollector` in `FennecLabs.TaintAnalysis`. Inventory is built by scanning all method CFGs for policy-matched source/sink call sites, regardless of whether a path connects them. `unmatchedRelevantExposures`: sinks found in the inventory but not reached by taint from any source.

---

### Story 2.9: Fixture projects and per-context end-to-end acceptance tests

As a developer,
I want five fixture test projects and automated acceptance tests that validate the core taint engine against concrete, known-good assemblies,
So that every context-detection, capability, DI-resolution, and ownership-boundary scenario has a machine-verifiable pass condition.

**Acceptance Criteria:**

**Given** `TaintFixture.WebAspNet` fixture project is built and analyzed
**When** all FA-series acceptance tests run
**Then** FA-1 (`detectedContext=web-aspnet`), FA-2 (inventory completeness), FA-3 (SQL injection finding), FA-4 (SSRF confidence not adjusted), FA-5 (Process.Start downgraded), FA-6 (unmatched exposure), FA-7 (inventory in handoff) all pass

**Given** `TaintFixture.WorkerService` fixture is analyzed
**When** FB-series tests run
**Then** FB-1..FB-6 all pass (context, command injection, SSRF medium, XSS informational, capability fingerprint, env var source)

**Given** `TaintFixture.ConsoleApp` fixture is analyzed
**When** FC-series tests run
**Then** FC-1..FC-6 all pass (context, path traversal, command injection, SSRF low/adjusted, SQL absent, fingerprint)

**Given** `TaintFixture.DiAbstraction` fixture (`AddScoped<IUserService, UserService>()`) is analyzed
**When** FD-series tests run
**Then** FD-1..FD-6 all pass (DI discovered, concrete resolved, taint through abstraction, hop annotated, handoff context, unresolvable interface)

**Given** `TaintFixture.MultiProjectSln` fixture (3-project solution) is analyzed
**When** FE-series tests run
**Then** FE-1..FE-7 all pass (manifest classification, first/second-party walked, third-party boundary, sink rule applied, opt-in walk, cross-project finding, second-party prefix reclassification)

**Technical notes:** Create fixture projects in `test/TestProjects/TaintFixtures/`. Each project is a minimal .NET 10 library or app targeting the fixture scenario described in the companion. Tests live in `FennecLabs.TaintAnalysis.Tests` (new test project). Tests invoke the taint pipeline programmatically against the fixture DLLs; assert on `result.json` payload properties. Tag tests with `[Category("TaintEngine")]` for selective running.

---

## Epic 3: Source Correlation and LLM Handoff

Developers can correlate taint findings to source files/lines via PDB symbols; security engineers can produce a bounded `llm-handoff.json` for AI-assisted investigation.

### Story 3.1: Implement `SymbolMapper` — PDB/Portable PDB source correlation

As a developer,
I want the taint engine to load PDB symbols and map every finding endpoint to a source file, line, and column when possible,
So that developers can navigate directly to the vulnerable code line from a taint finding.

**Acceptance Criteria:**

**Given** a fixture assembly built with Portable PDB (embedded or co-located)
**When** `SymbolMapper.Map(instruction, method)` is called
**Then** the returned `SourceRef` has `fidelity=exact`, non-null `file`, `startLine`, `startCol`, `endLine`, `endCol`

**Given** the instruction has no direct non-hidden `SequencePoint` but a preceding one exists
**When** `SymbolMapper.Map()` is called
**Then** the returned `SourceRef` has `fidelity=approximate` with the nearest preceding non-hidden sequence point's file/line

**Given** an assembly has no PDB (NuGet package without symbols)
**When** `SymbolMapper.Map()` is called
**Then** the returned `SourceRef` has `fidelity=unresolved`, `metadataToken` populated, and no `file`/`line` fields
**And** the finding is still emitted — never dropped due to missing symbols

**Given** a PDB file is present but does not match the assembly (wrong MVID)
**When** assembly loading occurs
**Then** the mismatch is handled gracefully (no crash); symbols are treated as absent; `pdbPresent=false` in `assemblyManifest`

**Technical notes:** Implement `SymbolMapper` in `FennecLabs.TaintAnalysis`. Use Mono.Cecil `ReaderParameters { ReadSymbols=true, SymbolReaderProvider=new PdbReaderProvider(), ThrowIfSymbolsAreNotMatching=false }`. Wrap load in try-catch `SymbolsNotFoundException` → reload without symbols.

---

### Story 3.2: Wire symbol mapping into findings, source/sink endpoints, and chain hops

As a developer,
I want every source endpoint, sink endpoint, and chain hop in `result.json` to carry symbol-mapped source references,
So that taint findings provide precise file/line navigation for the full call chain, not just the endpoints.

**Acceptance Criteria:**

**Given** a finding with `fidelity=exact` symbols on source and sink
**When** `result.json` is read
**Then** `finding.sourceEndpoint.symbolRef.file`, `startLine`, `fidelity=exact` are present and match the source file
**And** `finding.sinkEndpoint.symbolRef.file`, `startLine`, `fidelity=exact` are present and match the sink file

**Given** a finding whose chain passes through a method from an assembly without PDB
**When** `result.json` is read
**Then** the chain hop for that method has `symbolRef.fidelity=unresolved` and `symbolRef.metadataToken` is present

**Given** a finding chain passes through a DI abstraction edge resolved to a concrete type
**When** `result.json` is read
**Then** the chain hop for the abstraction edge carries `abstractionEdge=true`, `declaredContractType`, `resolvedConcreteMethod`, `resolutionBasis`, `resolutionConfidence`

**Technical notes:** Update `FindingCollector` to call `SymbolMapper.Map()` for each instruction at each endpoint and chain hop. Wire `SymbolMapper` into the propagation pipeline after Epic 2 baseline is working.

---

### Story 3.3: Implement `LlmHandoffSerializer` — bounded LLM artifact

As a security engineer,
I want `--taint-llm-handoff` to produce a bounded `llm-handoff.json` with all context needed for AI-assisted investigation,
So that I can feed the artifact to an LLM without exposing full IL or hitting token limits.

**Acceptance Criteria:**

**Given** `--taint-llm-handoff` is set and analysis produces findings
**When** `llm-handoff.json` is written
**Then** the file contains no raw IL instruction sequences; `findings[*].chainSummary` is a string array with ≤20 entries

**Given** a finding with a DI-resolved abstraction edge exists
**When** `llm-handoff.json` is read
**Then** `finding.diResolutionContext[]` contains `declaredContractType`, `resolvedConcreteTypes`, `resolutionBasis`, `resolutionConfidence`

**Given** any finding exists
**When** `llm-handoff.json` is read
**Then** every finding has `title`, `severity`, `category`, `confidence`, `sourceRef`, `sinkRef`, `chainSummary`, and `suggestedInvestigationQuestions` (array of ≥1 string)

**Given** the analysis produces `sourcesInventory`, `sinksInventory`, and `unmatchedRelevantExposures`
**When** `llm-handoff.json` is read
**Then** all three collections are present and match the corresponding fields in `result.json`

**Given** the LLM handoff artifact is schema-validated
**When** validated against `fennec.taint.llm-handoff.v1` JSON Schema
**Then** validation passes

**Technical notes:** Implement `LlmHandoffSerializer` in `FennecLabs.TaintAnalysis`. Investigation questions generated from policy rule metadata (category + sink type → templated question strings). Chain summary: one string per call-graph hop in the finding path, format: `"MethodA → reads X (source)"`, `"→ passes to IFoo.M [resolved: FooImpl.M via di-registration]"`, etc.

---

### Story 3.4: Symbol-correlation fixture tests (PDB present and absent matrix)

As a developer,
I want automated tests that verify symbol correlation fidelity for assemblies both with and without PDB symbols,
So that the `exact`/`approximate`/`unresolved` fidelity contract is verified against real fixture assemblies.

**Acceptance Criteria:**

**Given** `TaintFixture.WebAspNet` is built with Portable PDB (default)
**When** P3-1 acceptance test runs
**Then** source and sink `symbolRef.fidelity=exact`; `file` and `startLine` match the fixture source file

**Given** `TaintFixture.WebAspNet` is built without PDB (`<DebugType>none</DebugType>`)
**When** P3-2 acceptance test runs
**Then** `symbolRef.fidelity=unresolved`; `metadataToken` is present; findings are still emitted (not dropped)

**Given** `--taint-llm-handoff` is set and P3-3 acceptance test runs on `TaintFixture.WebAspNet`
**When** `llm-handoff.json` is read
**Then** no full IL dump exists in any field; `chainSummary` has ≤20 entries

**Given** P3-4 test runs on a finding with DI abstraction edge
**When** `llm-handoff.json` is read
**Then** `finding.diResolutionContext[0].declaredContractType`, `resolvedConcreteTypes`, `resolutionBasis`, `resolutionConfidence` are all present

**Technical notes:** Add two build configurations to `TaintFixture.WebAspNet`: one with PDB (default), one with `<DebugType>none</DebugType>`. Tests in `FennecLabs.TaintAnalysis.Tests` tagged `[Category("SymbolCorrelation")]`.

---

## Epic 4: Hardening, Performance, and Schema Governance

CI pipelines and developers can rely on taint analysis completing within predictable time and resource bounds, producing deterministic artifacts that are schema-governed and ready for future hosted adapter integration.

### Story 4.1: Implement timeout, cancellation, and partial-artifact emission

As a developer,
I want analysis to terminate cleanly when `--taint-timeout` expires and emit a partial but structurally valid artifact,
So that CI pipelines never hang indefinitely and incomplete results are clearly labeled.

**Acceptance Criteria:**

**Given** `--taint-timeout 5` is set on a large assembly that takes > 5 s to analyze
**When** the timeout expires
**Then** analysis terminates within 2 s of the deadline; a partial `result.json` is written with `payload.diagnostics.partial=true`
**And** the partial artifact validates against the JSON Schema

**Given** the user presses Ctrl+C (SIGINT/cancellation) during analysis
**When** the cancellation token fires
**Then** analysis stops at the next cancellation check point and writes a partial artifact if any phase is complete

**Given** analysis completes fully within the timeout
**When** `result.json` is read
**Then** `diagnostics.partial=false`; no timeout warning appears in CLI output

**Technical notes:** Wire `CancellationTokenSource` from `--taint-timeout` seconds into the `TaintPropagator` BFS loop (check `ct.ThrowIfCancellationRequested()` every N iterations). Catch `OperationCanceledException` in `InstrumentCommandHandler`; write partial artifact; return exit code 2 (partial/timeout).

---

### Story 4.2: Large-assembly warning and findings truncation guard

As a developer,
I want clear warnings when analyzed assemblies exceed the supported size threshold, and a hard cap on findings to prevent oversized artifacts,
So that analysis degrades gracefully rather than silently producing unmanageably large outputs.

**Acceptance Criteria:**

**Given** a fixture assembly with > 10,000 analyzable methods
**When** analysis begins
**Then** a warning is emitted to stderr: `"Assembly <name> has <N> methods (threshold: 10,000); analysis may be slow"`
**And** analysis continues (does not bail); `diagnostics.totalMethodsAnalyzed` reflects the actual count

**Given** analysis produces more than 500 findings
**When** `FindingCollector` finalizes results
**Then** findings are truncated to the first 500 (ordered by severity desc, confidence desc)
**And** `diagnostics.findingsTruncated=true`; truncated artifact validates against schema

**Technical notes:** Add method-count check in `InstrumentCommandHandler` before calling `TaintPropagator`. Add `findingsTruncated` guard in `FindingCollector.Finalize()`.

---

### Story 4.3: JSON Schema CI gate for taint contracts

As a maintainer,
I want a CI gate that validates taint artifact JSON schemas and rejects breaking changes without a major version bump,
So that downstream consumers are protected from unintentional contract regressions.

**Acceptance Criteria:**

**Given** a PR modifies `fennec.taint.result.v1.schema.json` by removing a required field
**When** the schema CI gate runs
**Then** the gate fails with a message identifying the removed field and the schema version conflict

**Given** a PR bumps `schemaVersion` from `1.0.0` to `2.0.0` along with a breaking field removal
**When** the schema CI gate runs
**Then** the gate passes (major version bump acknowledged)

**Given** a PR adds a new optional field to the schema without changing the version
**When** the schema CI gate runs
**Then** the gate passes (additive, non-breaking change)

**Given** the existing test fixture `result.json` files are validated against the registered schema
**When** the CI gate runs
**Then** all fixture artifacts validate successfully

**Technical notes:** Add `dotnet test` target or MSBuild task using `Newtonsoft.Json.Schema` or `JsonSchema.Net` to validate committed schema files. Check `schemaVersion` SemVer bump policy using a diff-based approach (previous version stored in `src/FennecLabs.Contracts/schemas/.schema-versions`).

---

### Story 4.4: Hosted adapter contract documentation and readiness verification

As a maintainer,
I want the taint artifact schema to be documented as hosted-compatible from v1,
So that a future hosted ingestion adapter can be built without schema changes.

**Acceptance Criteria:**

**Given** `result.json` is produced for any assembly
**When** its fields are compared to a documented set of hosted-mode required fields
**Then** `result.json` is a proper superset: every required field for hosted ingestion is present (verified in design review checklist)

**Given** `result.json` `sourceContext.projectPath` and `symbolRef.file` fields exist
**When** they contain absolute local file paths
**Then** the schema documentation clearly states these fields are nullable and MUST be treated as optional by hosted adapters (no hosted ingestion should fail when these are null)

**Given** hosted adapter contract documentation is reviewed
**When** an engineer reads `docs/hosted-adapter-contract.md`
**Then** the required field list, nullable conventions, and SemVer upgrade path are clearly documented

**Technical notes:** Create `docs/hosted-adapter-contract.md` documenting the field contract. Ensure `sourceContext.projectPath` and `symbolRef.file` are marked nullable (`string?`) in `FennecLabs.Contracts`. Add P4-4 acceptance test to `FennecLabs.TaintAnalysis.Tests` asserting that nullable fields are never required by test fixtures.
