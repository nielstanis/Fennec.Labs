# FD-040: Docs Truth Pass and Repo Presentation

**Status:** Open
**Priority:** High (before launch content)
**Effort:** Medium (1-2 days)
**Impact:** Everything a visitor reads matches what the tool does; launch posts can copy examples verbatim.

## Problem

- **CHANGELOG advertises removed code.** `[Unreleased]` describes `FennecLabs.TaintAnalysis` and `instrument --taint*`
  flags removed from main in `6a1b376`. Duplicate `### Added/Fixed/Changed` headings; `0.7.5` was never cut;
  superseded entries (FD-015 `--format`) remain.
- **Wrong JSON field names in docs.** `compare.md`/`output-schemas.md` document `typesOnlyInAssembly1/2` (code emits
  `perDll[].typesAdded/typesRemoved`); scorecard `status` documented as `"Available"` (code emits `"available"`);
  `instrument --json` with `--nuget` is `[{dllPath, invocations}]`, not a flat array; `feeds list --json` shape.
- **Other doc/code drift:** "29 diff-event subtypes" (28); "only `lib/` analysed" (any `.dll`); reproduce cache
  conditions; `fxt` format undocumented; README omits `-C`.
- **`docs/optimizations.md` is stale** — lists the fixed zip-slip bug as an open High-priority security issue.
- **FD index broken links** — FD-013 (MCP, listed Active) and ~10 archived FDs have no file.
- **Visitor confusion** — 274 tracked planning/agent files at the root (`_bmad*`, `.agents/`, `.github/agents/`),
  a dashboard PRD, and docs calling the envelope "dashboard" although no dashboard exists.
- **Weak pitch** — README opens with a feature list, not who it's for and why.

## Solution

1. CHANGELOG: move taint entries to the `feature/taint-analysis` branch; merge duplicate headings; add
   `[0.7.5] - 2026-07-22` retroactively; prepare the `[1.0.0]` section.
2. Generate JSON examples in docs from real runs (or snapshot tests) and fix every field name above.
3. Delete `docs/optimizations.md` (move live items to issues).
4. FD index: mark FD-013 Deferred (post-launch), remove/repair dead links.
5. Add `ROADMAP.md`: MCP server, dashboard, taint analysis explicitly "after 1.0".
6. Either move planning scaffolding under one folder with a short "How this repo is built" note, or keep and
   explain it — decide deliberately (it can itself be launch content).
7. README: audience + problem first ("verify what you ship and consume"), 3-command quickstart, then reference.
8. Add a one-line licence note: running the AGPL CLI imposes no obligations on the code it analyses.

## Verification

- Every `jq` example in `docs/` works against real output.
- No doc mentions a flag, field or feature absent from `src/`.
