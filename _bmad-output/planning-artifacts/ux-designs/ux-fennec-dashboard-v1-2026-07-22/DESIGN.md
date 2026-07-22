---
name: Fennec Dashboard v1 UX Design
type: ux-design-spine
status: final
created: 2026-07-22
updated: 2026-07-22
---

# DESIGN — Fennec Dashboard v1 (Project-Scoped)

## Visual Intent

Balanced dashboard UI that combines rapid scanability (summary cards) with deep inspection (expandable dependency tree + details panel).

## Design Tokens

- **Color tokens**
  - `color.surface.default`: `#0F172A`
  - `color.surface.elevated`: `#1E293B`
  - `color.text.primary`: `#E2E8F0`
  - `color.text.secondary`: `#94A3B8`
  - `color.status.good`: `#22C55E`
  - `color.status.medium`: `#F59E0B`
  - `color.status.high`: `#F97316`
  - `color.status.critical`: `#EF4444`
  - `color.border.default`: `#334155`
- **Spacing tokens**
  - `space.1`: 4px
  - `space.2`: 8px
  - `space.3`: 12px
  - `space.4`: 16px
  - `space.6`: 24px
  - `space.8`: 32px
- **Typography tokens**
  - `font.family.base`: Inter, Segoe UI, system-ui
  - `font.size.sm`: 12px
  - `font.size.md`: 14px
  - `font.size.lg`: 16px
  - `font.size.xl`: 20px
  - `font.weight.medium`: 500
  - `font.weight.semibold`: 600

## Component Proposals

1. `SummaryKpiCard` for quick indicators (package count, critical count, unknown count, generated time).
2. `DependencyTreePanel` for hierarchical package visualization.
3. `DependencyTreeNode` for each package row with expand/collapse and risk badges.
4. `ScorecardDetailPanel` for selected package checks and rationale.
5. `RiskBadge` for normalized severity/status display.
6. `StateMessage` for loading/empty/error/success states.
7. `FilterBar` for package search, score threshold, and transitive-only toggle.
8. `ProvenanceBanner` for schema version, producer version, and source context.

## Visual Standards

- Use semantic severity colors only through `RiskBadge` token mapping (no hardcoded per-screen colors).
- Keep tree and detail regions visible together on desktop widths to reduce context switching.
- Keep actionable metadata (schema version, artifact timestamp, source path) visible in the header/provenance area.

## Responsive Layout

- **Desktop (`>=1200px`)**: 3-column layout: summary cards + tree panel + details panel.
- **Tablet (`768px - 1199px`)**: summary row, tree takes primary area, detail panel collapses into tab.
- **Mobile (`<768px`)**: stacked flow with detail as drill-in screen.

## Accessibility Baseline

- Contrast ratio minimum 4.5:1 for normal text, 3:1 for large text/icons.
- All interactive controls keyboard reachable with visible focus state.
- Tree semantics exposed with ARIA roles and expanded/collapsed state.
- Non-color indicators required for severity/risk states.
