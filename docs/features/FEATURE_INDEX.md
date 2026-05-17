# Feature Design Index

Planned features and improvements for FennecLabs.

See `CLAUDE.md` for FD lifecycle stages and management guidelines.

## Active Features

| FD | Title | Status | Effort | Priority |
|----|-------|--------|--------|----------|
| [FD-020](FD-020_SCORECARD_REPORT_FORMATS.md) | Scorecard Report — JSON-Driven and MD/HTML Formats | Open | Medium | Medium |
| [FD-013](FD-013_FENNECLABS_MCP_SERVER.md) | FennecLabs.Mcp — MCP Server exposing Fennec capabilities as AI agent tools | Design | High | High |
| [FD-009](FD-009_MISSING_IL_OPCODES_INSTRUMENTATION.md) | Capture Missing IL Opcodes in Instrumentation | Open | Low | Low |

## Completed

| FD | Title | Completed | Notes |
|----|-------|-----------|-------|
| [FD-019](archive/FD-019_NUPKG_HELPER_CONSOLIDATION.md) | Consolidate nupkg Extraction Helpers | 2026-05-17 | `NupkgHelper` static class; `ExtractAsync` + `GetDlls` shared by Reproduce and CompareLocalFiles |
| [FD-018](archive/FD-018_COMPARE_LOCAL_FILES.md) | Compare Local Files — Accept Two .nupkg or .dll Files Directly | 2026-05-16 | `compare --file a b`; arity-2 option; .dll direct compare + .nupkg extract-and-match; same JSON schema as NuGet path; no cache |
| [FD-017](archive/FD-017_ASSEMBLYDIFF_TIER2_STRUCTURAL.md) | AssemblyDiff Tier 2 — Structured Diff Records, File Split, PInvokeInfo, Generic Constraints | 2026-05-16 | Typed DiffEvent records replace List<string> Differences; AssemblyComparisonResult + DiffEvent split into separate files; PInvokeInfo, security declarations, nested type recursion added; 28 tests |
| [FD-016](archive/FD-016_STRUCTURED_OUTPUT_DIRECTORY.md) | Structured Output Directory — Per-Command Subfolders and Result Cache | 2026-05-16 | Global --output/-o root; instrument→.fennec/instrument/; scorecard→.fennec/scorecard/<project>/<ts>/; compare/reproduce write result.json + serve from cache; global --no-cache to bypass |
| [FD-015](archive/FD-015_GLOBAL_JSON_OUTPUT.md) | Global JSON Output — All Commands | 2026-05-14 | Global --format human\|json recursive option; OutputMode enum; --file-format rename on instrument; all 5 handlers emit structured JSON; progress suppressed in JSON mode |
| [FD-014](archive/FD-014_SEMANTIC_SPECTRE_UI.md) | Semantic Spectre.Console UI — Scorecard & Compare | 2026-05-14 | ColorTheme + ScorecardRenderer + DiffRenderer; score thresholds (green/yellow/red); diff lines (+/−/~); download spinners via AnsiConsole.Status |
| [FD-006](archive/FD-006_SPECTRE_CONSOLE_FOUNDATION.md) | Add Spectre.Console as CLI Rendering Foundation | 2026-05-14 | Spectre.Console 0.55.2 added to FennecLabs.Cli; foundation for FD-014 rendering layer |
| [FD-012](archive/FD-012_INSTRUMENTATION_TIER2_STRUCTURAL.md) | Instrumentation Tier 2 Structural — Rename Analyse→Analyze, CancellationToken, OutputFormat Enum, Edge-Case Tests | 2026-05-14 | Renamed Analyse→Analyze; CancellationToken on Analyze() and WriteOutputAsync; OutputFormat enum replaces stringly-typed factory; 3 new AssemblyAnalyzer edge-case tests |
| [FD-011](archive/FD-011_ASSEMBLYDIFF_TIER1_QUICK_WINS.md) | AssemblyDiff Tier 1 Quick Wins — Custom Attribute Args, MethodImplAttributes, Parameter Attributes, Configurable Truncation | 2026-05-13 | Custom attribute ctor arg comparison; MethodImplAttributes diff; parameter attributes in method signature key; confirmed Take(10) already data-layer clean |
| [FD-010](archive/FD-010_INSTRUMENTATION_TIER1_QUICK_WINS.md) | Instrumentation Tier 1 Quick Wins — Writer Base Path, WriterFactory Guard, ClassTypeResult Cleanup, OrdinalIgnoreCase | 2026-05-13 | Extract ResolveOutputPath into Writer base; WriterFactory throws on unknown format; remove dead _module field; OrdinalIgnoreCase in factory |
| [FD-008](archive/FD-008_TIER2_STRUCTURAL_FIXES.md) | Tier 2 Structural Fixes — Extract Handlers, Deduplicate NuGetService, Inject HttpClient, AssemblyDiff Tests | 2026-05-13 | Extracted 5 command handlers, deduplicated NuGetService download pipeline, IDisposable HttpClient injection, AssemblyDiff test project (13 tests), CONTRIBUTING.md CI filter docs |
| [FD-007](archive/FD-007_TIER1_QUICK_WINS.md) | Tier 1 Quick Wins — Output, Exit Codes, Error Routing, Feeds CLI | 2026-05-13 | --format option, non-zero exit codes, stderr routing, feeds subcommands, TreatWarningsAsErrors, exception cleanup |
| [FD-005](archive/FD-005_FIX_INSTRUMENTATION_OUTPUT_PATH.md) | Fix Instrumentation Output Double-Nest Path Bug | 2026-05-12 | Remove hardcoded "fenneclabs" subfolder; add --output/-o option (default: .fennec); NuGet scoped to packageId/version/ |
| [FD-004](archive/FD-004_SCORECARD_TRANSITIVE_DEPS.md) | Fetch Scorecards for Transitive Dependencies | 2026-05-12 | Concat TopLevelPackages + TransitivePackages; Castle.Core visible via AnotherWebApp |
| [FD-003](archive/FD-003_POLLYAWS_SCORECARD_JSON.md) | Scorecard JSON Integration Test for PollyAwsMvcApp | 2026-05-10 | Offline fixture JSON in TestData/, live tests tagged Category=Live, .fennec/ gitignored |
| [FD-002](archive/FD-002_POLLYAWS_TEST_APP.md) | Add PollyAwsMvcApp Test Fixture with Transitive Package Verification | 2026-05-10 | Added TestProjectCsprojAttribute, migrated tests to TestResources, exact transitive assertions |
| [FD-001](archive/FD-001_UPDATE_DEPENDENCIES.md) | Update All Dependencies to Latest | 2026-05-10 | Bumped NuGet.Protocol → 7.3.1, System.CommandLine → 2.0.7 |

## Deferred / Closed

| FD | Title | Status | Notes |
|----|-------|--------|-------|
| - | - | - | No deferred features yet |

## Backlog

Low-priority or blocked items. Promote to Active when ready to design.

| FD | Title | Notes |
|----|-------|-------|
| - | - | No backlog items yet |
