# FD-037: CLI Correctness Hardening for Launch

**Status:** Open
**Priority:** High (launch blocker)
**Effort:** Medium (1-2 days)
**Impact:** Every command survives a live demo and a first run by a stranger on Windows, macOS and Linux.

## Problem

Issues found during the pre-launch review (2026-10-07):

1. **`compare --version` help is wrong.** Help/README/docs say "compare against latest"; the code compares the
   given version with the *previous* published version (`CompareCommandHandler.cs`, `sortedVersions[currentIndex + 1]`).
2. **`reproduce --directory` likely fails on Windows.** Feed DLLs are filtered with
   `f.Path.StartsWith($"lib/{tfm}/")`, but `f.Path` comes from `Path.GetRelativePath` (backslashes on Windows).
3. **Unhandled exceptions leak.** `reproduce --directory` has no try/catch around NuGet calls; `feeds list`
   has no catch; `scorecard`/`dependencies` only catch `InvalidOperationException` (a missing `dotnet` on PATH
   throws `Win32Exception`). There is no top-level handler around `InvokeAsync`.
4. **Inconsistent "latest" semantics.** `compare` excludes prereleases; `instrument`/`reproduce` include them.
5. **Silent truncation of multi-project/multi-TFM input.** `scorecard`/`dependencies` analyse only
   `Projects[0]`/`Frameworks[0]` without saying so; `--project` is described as "(required)" but isn't.
6. **`scorecard --json --report-format html` writes no report**, silently.
7. **Unknown `instrument --file-format` silently falls back to `fxt`.**
8. **No Ctrl+C cancellation** is threaded into handlers.

## Solution

1. Fix `compare --version` help/docs to "compare this version with the previous published version".
2. Normalise separators before matching (`f.Path.Replace('\\', '/')`) in `BuildFeedByName`.
3. Add a top-level `try/catch` in `Program.Main` that prints a one-line `error: <message>` to stderr and exits 1;
   full stack trace only when `FENNEC_DEBUG=1`.
4. Pick one "latest" rule (stable unless `--prerelease`), apply everywhere, document it.
5. Warn (stderr) when more than one project/TFM is present and only the first is analysed; drop "(required)".
6. Write reports in JSON mode too (or reject the combination with a clear error).
7. Reject unknown `--file-format` values via `AcceptOnlyFromAmong`.
8. Pass `ParseResult`'s cancellation token into handlers.
9. Add a `windows-latest` + `macos-latest` matrix to `ci-build.yml` (offline tests only).
10. Add handler tests for `instrument`, `compare --nuget`, `feeds`, and `reproduce` beyond TFM resolution.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Top-level handler, help text, cancellation, option validation |
| `src/FennecLabs.Cli/Commands/*CommandHandler.cs` | MODIFY | Items 2, 4, 5, 6 |
| `.github/workflows/ci-build.yml` | MODIFY | OS matrix |
| `test/FennecLabs.Cli.Tests/` | MODIFY | Handler tests |

## Verification

- CI green on Linux, Windows, macOS.
- `fennec scorecard --project does-not-exist.csproj` and `fennec reproduce -d bin -n X` while offline both
  print a single readable error and exit 1 — no stack traces.
