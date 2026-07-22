---
name: Fennec Dashboard v1 UX Experience
type: ux-experience-spine
status: final
created: 2026-07-22
updated: 2026-07-22
---

# EXPERIENCE — Fennec Dashboard v1 (Project-Scoped)

## Information Architecture

1. **Dashboard Home**
   - Summary KPI row
   - Provenance banner
   - Filter bar
   - Dependency tree panel
   - Package scorecard detail panel
2. **Artifact Selection (if multiple snapshots)**
   - Snapshot list with timestamp/source/branch
3. **Error + Recovery States**
   - Invalid schema state
   - Missing artifacts state
   - Partial data state

## Core Interaction Flows

### Flow 1: Inspect dependency risk

1. User opens dashboard from local artifact set.
2. Summary cards render high-level risk overview.
3. User expands tree nodes to locate suspicious transitive package.
4. User selects package node.
5. Detail panel renders scorecard checks and signals.

### Flow 2: Narrow risk candidates

1. User enters package name or risk threshold in `FilterBar`.
2. Tree updates in-place while preserving expanded path where possible.
3. Empty results state explains why no nodes match and how to clear filters.

### Flow 3: Validate artifact provenance

1. User checks `ProvenanceBanner` for `schemaVersion`, `producerVersion`, and source context.
2. If schema mismatch is detected, user sees explicit incompatibility state with guidance.

## States and Behaviors

- **Loading:** Skeleton cards + tree placeholders while reading artifacts.
- **Empty:** Clear message when no dependencies are present.
- **Partial:** If scorecard entries are missing, show node-level "Unavailable" status and explain source gap.
- **Error:** Structured error card with actionable message (bad path, invalid schema, parse failure).
- **Success:** Stable render with responsive interactions under filter and expand/collapse operations.

## Accessibility and Input Behavior

- Full keyboard traversal order: header → filters → tree → details.
- Tree keyboard behavior: arrows for navigate/expand/collapse; Enter/Space to select.
- Screen reader labels for score statuses include textual severity and package name.
- Announce filter result count changes through live region updates.

## Compatibility Targets

- Latest Chromium-based browsers.
- Latest Firefox.
- Latest Safari.
- Progressive degradation for reduced-motion preference (disable animation-heavy transitions).
