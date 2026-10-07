# FD-036: Wire Configured Feeds into Package Resolution

**Status:** Open
**Priority:** High (launch blocker)
**Effort:** Medium (0.5-1 day)
**Impact:** `fennec feeds add` actually changes where `instrument`, `compare`, `reproduce` and `scorecard`
resolve packages, so private/internal feeds work as documented.

## Problem

`fennec feeds add/remove/list` persists feeds to `~/.fennec/settings.json`, but no command reads them. Every
consumer is constructed with `new NuGetService()` (`Program.cs` for instrument/compare/reproduce,
`ScorecardClient.cs` for scorecard). With no `FeedService`, `NuGetService` always falls back to
`https://api.nuget.org/v3/index.json`.

`docs/commands/feeds.md` and the `feeds` help text both claim feeds are used "when resolving packages for
instrument, compare, and reproduce" and recommend adding a private feed. A user who does this gets
"Package not found". There is also no credential support for authenticated feeds.

## Solution

1. Construct one `FeedService` in `Program.cs` and pass it into every `NuGetService` (and `ScorecardClient`).
2. Decide resolution semantics and document them: default feed only, or try the default feed then fall back to
   the others in order (recommended — matches how users expect `nuget.config` sources to behave).
3. Honour credentials from the user's `nuget.config` (`PackageSourceCredential`) via NuGet's
   `SettingsUtility`/`PackageSourceProvider` instead of inventing a credential store.
4. Show the resolved feed in human output ("from <feed name>") and add `feed` to the JSON results.
5. Wiring test: `Program`-level test (or a factory) asserting handlers receive a feed-aware `NuGetService`.

If this slips, the fallback is to remove `feeds` from v1 and its docs, rather than ship a no-op command.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Cli/Program.cs` | MODIFY | Single `FeedService`, pass to all `NuGetService` instances |
| `src/FennecLabs.NuGet/NuGetService.cs` | MODIFY | Multi-feed resolution + fallback |
| `src/FennecLabs.NuGet/FeedService.cs` | MODIFY | Credential lookup from nuget.config |
| `src/FennecLabs.Scorecard/ScorecardClient.cs` | MODIFY | Accept injected `NuGetService` |
| `docs/commands/feeds.md` | MODIFY | Accurate resolution semantics |
| `test/FennecLabs.NuGet.Tests/` | MODIFY | Resolution order + fallback tests |

## Verification

1. `fennec feeds add local <folder-with-nupkg>` then `fennec instrument --nuget <pkg-only-in-that-folder>` works.
2. Removing the feed makes the same command fail with a clear "not found in configured feeds" message.
3. Default behaviour with no settings file is unchanged (nuget.org).
