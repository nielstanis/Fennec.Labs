---
baseline_commit: 854074f305cb61170ef6748b1e41a49e412e38c6
---

# Story 1.4: Enforce producer-side schema validity

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a maintainer,  
I want automated producer validation tests for generated artifacts,  
so that invalid schema output is blocked before release.

## Acceptance Criteria

1. **Given** canonical artifact generation for dependency and scorecard flows  
   **When** validation tests execute  
   **Then** generated artifacts are checked against shared contract expectations and fail fast on schema incompatibility.
2. **Given** canonical artifact generation for dependency and scorecard flows  
   **When** validation tests execute  
   **Then** regression coverage prevents producers from shipping envelope/payload breaking changes unnoticed.

## Tasks / Subtasks

- [x] Add producer-side dependencies artifact validation tests in `test/FennecLabs.Cli.Tests` (AC: 1, 2)
  - [x] Add tests that execute `DependencyGraphCommandHandler.ExecuteAsync(...)` and capture emitted JSON.
  - [x] Assert envelope metadata (`$schema`, `schemaVersion`, `command`, `producedAt`, `producerVersion`, `sourceContext`, `payload`) is present and canonical.
  - [x] Deserialize emitted artifact into `DashboardArtifactEnvelope<DependencyGraphPayload>` using `ContractJsonOptions.Default` and assert stable round-trip compatibility.
  - [x] Assert payload-level invariants expected by canonical contracts (normalized package IDs, target framework presence, node collection shape).

- [x] Add producer-side scorecard artifact validation tests in `test/FennecLabs.Cli.Tests` (AC: 1, 2)
  - [x] Add tests that execute `ScorecardCommandHandler.ExecuteAsync(...)` with deterministic inputs and capture emitted JSON.
  - [x] Assert emitted envelope contracts and command identity for scorecard artifacts.
  - [x] Deserialize emitted artifact into `DashboardArtifactEnvelope<ScorecardGraphPayload>` using `ContractJsonOptions.Default`.
  - [x] Assert structured missing/error states in payload remain explicit (no silent omission patterns).

- [x] Add reusable test utilities for artifact assertion and deterministic file handling (AC: 1, 2)
  - [x] Add helper methods in `test/FennecLabs.Cli.Tests` to locate latest artifact files under `.fennec/dependencies/.../result.json` and `.fennec/scorecard/.../result.json`.
  - [x] Add helper methods to parse JSON as both `JsonNode` and typed envelope models.
  - [x] Keep helpers local to CLI test project unless shared reuse is immediately required.

- [x] Protect existing behavior while adding validation coverage (AC: 2)
  - [x] Preserve current command output modes (`Human`, `Json`) and artifact write paths.
  - [x] Preserve existing contract serialization conventions (`camelCase`, null omission, indented output, enum converter).
  - [x] Ensure new tests do not require network calls; use deterministic/fake scorecard client responses where needed.

- [x] Execute targeted test suites (AC: 1, 2)
  - [x] Run `dotnet test test/FennecLabs.Cli.Tests/`.
  - [x] Run `dotnet test test/FennecLabs.Contracts.Tests/` to ensure contract baselines remain aligned.

## Dev Notes

### Story intent and scope

- This story is a **test-enforcement** story for producer outputs, not a redesign of envelope/payload models.
- Scope is producer-side validation guardrails for dependencies and scorecard artifact generation paths.

### Relevant current implementation (must read before editing)

- `src/FennecLabs.Cli/Commands/DependencyGraphCommandHandler.cs`
  - Produces dependency artifact JSON via `DependencyGraphNormalizer.Normalize(...)`.
  - Writes artifact to `OutputCache.DependenciesDir(...)/result.json`.
- `src/FennecLabs.Cli/Commands/ScorecardCommandHandler.cs`
  - Produces scorecard artifact JSON via `ScorecardGraphNormalizer.Normalize(...)`.
  - Writes artifact to `OutputCache.ScorecardDir(...)/result.json`.
- `src/FennecLabs.Contracts/ContractJsonOptions.cs`
  - Canonical serializer settings that producer and consumer must share.
- `src/FennecLabs.Contracts/DashboardArtifactEnvelope.cs`
  - Canonical required envelope shape.
- `src/FennecLabs.DotNetCli/DependencyGraphNormalizer.cs`
  - Canonical dependency payload mapping and envelope construction.
- `src/FennecLabs.Scorecard/ScorecardGraphNormalizer.cs`
  - Canonical scorecard payload mapping and envelope construction.
- `src/FennecLabs.Cli/OutputCache.cs`
  - Artifact path conventions and write behavior.

### Existing test patterns to reuse

- Contract tests already assert canonical field names and round-trip behavior:
  - `test/FennecLabs.Contracts.Tests/DashboardArtifactEnvelopeTests.cs`
  - `test/FennecLabs.Contracts.Tests/DependencyGraphPayloadTests.cs`
  - `test/FennecLabs.Contracts.Tests/ScorecardGraphPayloadTests.cs`
- Normalizer tests already assert canonical mapping invariants:
  - `test/FennecLabs.DotNetCli.Tests/DependencyGraphNormalizerTests.cs`
  - `test/FennecLabs.Scorecard.Tests/ScorecardGraphNormalizerTests.cs`

### Architecture compliance guardrails

- Enforce AD-1 canonical envelope invariants on producer outputs.
- Preserve AD-2 flat-file artifact behavior (`.fennec` output path semantics).
- Preserve AD-4 shared contract ownership (`FennecLabs.Contracts` types as source of truth).
- Preserve AD-5 schema governance (`schemaVersion` and `$schema` always emitted).
- Preserve AD-7 normalized dependency payload behavior.

### File structure requirements

- Primary edits should be in:
  - `test/FennecLabs.Cli.Tests/` (new command handler artifact validation tests and helpers)
- Avoid changing contract model files unless a test reveals a real contract defect.
- Avoid introducing new test projects; reuse existing `FennecLabs.Cli.Tests` and `FennecLabs.Contracts.Tests`.

### Testing requirements

- Use xUnit style already used in repository.
- Keep tests deterministic:
  - No network dependency.
  - No real external service calls.
  - Use temp paths for output root where appropriate.
- For scorecard command tests, isolate from real HTTP by stubbing/faking `ScorecardClient` behavior or introducing injectable seam consistent with current project patterns.

### Risks and anti-patterns to avoid

- Do **not** test only pretty-printed JSON text snapshots; prefer semantic assertions on parsed JSON + typed deserialization.
- Do **not** duplicate contract logic inside tests; validate against shared contract types and canonical options.
- Do **not** silently accept missing envelope fields; required envelope fields must be asserted explicitly.
- Do **not** break existing human/json output behavior while adding tests.

### Project Structure Notes

- This repo already contains `src/FennecLabs.Contracts` and corresponding tests; Story 1.4 should leverage that foundation.
- Current CLI tests do not yet cover dependency/scorecard command artifact schema validity end-to-end, which is the gap this story closes.

### References

- Story definition and ACs: [Source: _bmad-output/planning-artifacts/epics.md#Story-14-Enforce-producer-side-schema-validity]
- FR-1 contract expectations: [Source: _bmad-output/planning-artifacts/PRD.md#FR-1-Produce-canonical-dashboard-artifact-envelope]
- Architecture invariants AD-1/2/4/5/7: [Source: _bmad-output/planning-artifacts/architecture/architecture-Fennec.Labs-2026-07-22/ARCHITECTURE-SPINE.md#Invariants--Rules]
- UX explicit schema-mismatch state expectation: [Source: _bmad-output/planning-artifacts/ux-designs/ux-fennec-dashboard-v1-2026-07-22/EXPERIENCE.md#Core-Interaction-Flows]
- Producer implementations:
  - [Source: src/FennecLabs.Cli/Commands/DependencyGraphCommandHandler.cs]
  - [Source: src/FennecLabs.Cli/Commands/ScorecardCommandHandler.cs]
  - [Source: src/FennecLabs.DotNetCli/DependencyGraphNormalizer.cs]
  - [Source: src/FennecLabs.Scorecard/ScorecardGraphNormalizer.cs]
  - [Source: src/FennecLabs.Contracts/ContractJsonOptions.cs]
  - [Source: src/FennecLabs.Contracts/DashboardArtifactEnvelope.cs]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex (model ID: gpt-5.3-codex)

### Debug Log References

- dotnet test test/FennecLabs.Cli.Tests/FennecLabs.Cli.Tests.csproj
- dotnet test test/FennecLabs.Contracts.Tests/FennecLabs.Contracts.Tests.csproj

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.
- No sprint-status file was found; status synchronization step is pending until sprint tracking exists.
- Added injectable package-resolution seams to dependency and scorecard command handlers to allow deterministic producer-level artifact tests without network or dotnet CLI execution.
- Added producer artifact schema validation tests that execute command handlers, read written `result.json`, assert canonical envelope fields, and round-trip deserialize with `ContractJsonOptions.Default`.
- Verified scorecard unavailable results stay explicit via structured error payload (`scorecard.unavailable`) in producer-generated artifacts.
- Executed targeted test suites successfully after implementation.

### File List

- _bmad-output/implementation-artifacts/1-4-enforce-producer-side-schema-validity.md
- src/FennecLabs.Cli/Commands/DependencyGraphCommandHandler.cs
- src/FennecLabs.Cli/Commands/ScorecardCommandHandler.cs
- test/FennecLabs.Cli.Tests/ProducerArtifactSchemaValidationTests.cs

## Change Log

- 2026-07-23: Implemented Story 1.4 producer-side schema validity coverage and marked story ready for review.
