# FD-041: AssemblyDiff Coverage Gaps — Resources, Static Data, References, Non-DLL Content

**Status:** Open
**Priority:** Medium (required before any "xz-style backdoor" messaging)
**Effort:** Medium (1-2 days)
**Impact:** `compare`/`reproduce` detect tampering that lives outside method IL and type shapes.

## Problem

`AssemblyComparer` covers assembly identity/attributes, types, members and IL bodies, but not:

- Embedded/manifest resources (content hashes)
- Field `InitialValue` / RVA static data (byte arrays baked into the binary)
- Assembly references (name, version, public key token)
- Compiler-generated fields (names starting with `<` are skipped)

And packages are only inspected for `*.dll`. `build/*.targets`, `.props`, `tools/*.ps1`, `buildTransitive/`,
analyzers' non-DLL files and native `runtimes/` binaries are invisible. The xz backdoor lived in test blobs plus
build-script changes — exactly these blind spots.

## Solution

1. New `DiffEvent` subtypes: `ResourceAdded/Removed/Changed` (by SHA-256), `FieldInitialValueChanged`,
   `AssemblyReferenceAdded/Removed/Changed`.
2. Package-level non-DLL comparison: hash every file in both packages; report added/removed/changed files, with
   MSBuild (`.targets`/`.props`) and script files flagged as high-signal in human output.
3. Revisit the `<`-prefixed field skip (compare by shape, not name).

## Verification

- Fixture assemblies differing only in a resource / static array / reference each produce exactly one event.
- Fixture `.nupkg` pair differing only in `build/X.targets` reports it in `compare --file`.
