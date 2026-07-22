---
title: Fennec.Labs Dashboard — Addendum
related_brief: brief.md
updated: 2026-07-22
---

# Addendum: Fennec.Labs Dashboard

Technical constraints, open questions, and context surfaced during discovery that belong to downstream architecture/technical-feasibility work rather than the product brief itself.

## Shared data model / storage (the core open question)

The brief commits to "a new shared, versioned JSON schema for Fennec result data" without designing it. Key open questions for the architecture phase:

- **Schema shape.** What does a versioned result envelope look like across `scorecard`, `instrument`, `compare`/`reproduce`, and the new transitive-dependency-tree view? Today each command has its own ad hoc JSON shape under `.fennec/<command>/...`.
- **Storage location.** Today `.fennec/` is gitignored, per-machine cache. The brief raises committing results to the source repo so the project-scoped dashboard has history without re-running commands — but that has real tradeoffs (repo bloat, staleness, merge conflicts on generated data, secrets/URLs leaking into results) that need explicit design, not just a checkbox.
- **Reuse across modes.** The same schema needs to serve a local file-read (project-scoped) and, later, a hosted service aggregating many projects — likely implying a real storage/query layer (not just flat files) once hosted mode exists. Design the schema so that transition doesn't force a rewrite.
- **Versioning/migration.** As Fennec's own result shapes evolve (see FD history — DiffEvent record types, ScorecardReportBuilder, etc.), the schema needs a migration story so old cached/committed results don't break the dashboard.

Recommend routing this to `bmad-technical-research` (TR) or `bmad-create-architecture` before implementation starts.

## Deferred ideas (out of v1 scope, worth remembering)

- Instrumentation view and Compare/Reproduce views, using the same shared-view pattern once proven.
- Hosted/centralized dashboard aggregating all packages a team tracks (v2+).
- A GitHub Copilot App canvas surface as an additional presentation layer alongside the browser view — this repo's Copilot CLI session already has canvas tooling (`open_canvas`/`browser` canvas) that could prototype this cheaply once the local dashboard exists.
- Relationship to `FennecLabs.Mcp` (FD-013): CLI/JSON output already serves LLM/agent consumption; the dashboard is the human-facing complement, not a replacement. Worth coordinating schema work so both consumers (MCP tools and dashboard) read the same result shape.

## Rejected / explicitly out of scope for v1

- Auth and multi-tenancy — irrelevant until hosted mode exists, not designed here.
- Precise adoption/usage metrics — brief leaves these TBD pending real usage data.
