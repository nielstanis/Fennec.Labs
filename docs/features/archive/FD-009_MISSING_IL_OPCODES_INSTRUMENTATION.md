# FD-009: Capture Missing IL Opcodes in Instrumentation

**Status:** Complete
**Completed:** 2026-07-16
**Priority:** Low
**Effort:** Low (1-4 hours)
**Impact:** Closes coverage gaps in `fennec instrument`'s method-invocation extraction so
delegate creation, virtual-dispatch function pointers, and tail calls show up in the
invocation graph instead of being silently dropped.

## Problem

`AssemblyAnalyzer.Analyze` (`src/FennecLabs.Instrumentation/AnalyseAssembly.cs`) only walks
instructions whose opcode is one of:

```csharp
OpCodes.Call, OpCodes.Callvirt, OpCodes.Calli, OpCodes.Newobj
```

This misses IL opcodes that also represent a "reference to a method" and are relevant to an
invocation/dependency graph:

- **`Ldftn`** — loads a pointer to a method; emitted for `new Action(SomeMethod)`,
  `new EventHandler(Handler)`, and any non-virtual delegate/method-group construction.
- **`Ldvirtftn`** — loads a pointer to a virtual method's implementation; emitted for delegate
  construction over a virtual/interface method (e.g. `new Func<int>(instance.VirtualMethod)`).
- **`Jmp`** — a tail-call-style jump directly to another method with the same signature,
  replacing the current frame. Rare (compilers rarely emit it), but it is a genuine method
  reference that today produces zero invocations for the containing method.

Today, any method whose only method-references are through delegate construction (a very
common pattern — event handlers, callbacks, `Func`/`Action` usage) reports `Invocations` as
empty even though it clearly depends on another method. This under-reports the dependency
graph that `instrument` is meant to capture.

## Solution

Extend the opcode filter in `AssemblyAnalyzer.Analyze` to include `Ldftn`, `Ldvirtftn`, and
`Jmp`, alongside the existing `Call`/`Callvirt`/`Calli`/`Newobj`:

```csharp
foreach (var instruction in method.Body.Instructions
    .Where(u => u.OpCode == OpCodes.Call
        || u.OpCode == OpCodes.Callvirt
        || u.OpCode == OpCodes.Calli
        || u.OpCode == OpCodes.Newobj
        || u.OpCode == OpCodes.Ldftn
        || u.OpCode == OpCodes.Ldvirtftn
        || u.OpCode == OpCodes.Jmp
    ))
```

The existing operand-parsing logic (`instruction.Operand?.ToString()?.Split(" ")`, taking
`splits[0]` as return type and `splits[1]` as invocation signature) already works unchanged
for these opcodes — `Ldftn`/`Ldvirtftn`/`Jmp` all carry a `MethodReference` operand whose
`ToString()` has the same `"ReturnType Namespace.Type::Method(params)"` shape as `Call`.

No changes needed to `InvocationResult`, `MethodResult`, or the JSON/FXT writers — this is
purely an input-filter extension, not a schema change.

### Out of scope

- `Calli`'s operand is a `CallSite`, not a `MethodReference`; it's already handled today and
  is untouched by this change.
- Opcodes like `Ldtoken` (used for `typeof(Method)`/reflection metadata tokens) and
  `Newarr`/`Initobj` are not method invocations and are deliberately excluded.
- No new `InvocationKind`/opcode-name field is added to `InvocationResult` in this pass —
  callers can't currently distinguish a `Call` from an `Ldftn` reference. That's a reasonable
  Tier-2 follow-up if consumers need it, but is not required to close this gap.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.Instrumentation/AnalyseAssembly.cs` | MODIFY | Add `Ldftn`, `Ldvirtftn`, `Jmp` to the opcode filter in `Analyze` |
| `test/FennecLabs.Instrumentation.Tests/AssemblyAnalyzerTests.cs` | MODIFY | Add test cases covering delegate construction (`Ldftn`), virtual delegate construction (`Ldvirtftn`), and a synthetic `Jmp` method |

## Verification

1. `dotnet build` — 0 errors, 0 warnings
2. New test: a method that only does `var handler = new EventHandler(OnClick);` — asserts the
   invocation list is no longer empty and contains `OnClick`.
3. New test: a method that constructs a delegate over an instance's virtual method — asserts
   the `Ldvirtftn` reference appears in `Invocations`.
4. New synthetic-assembly test using `ILProcessor` emitting `OpCodes.Jmp` directly to another
   method — asserts it is captured.
5. Existing `AssemblyAnalyzerTests` (e.g. `BasicConsoleResultTest`'s `Invocations.Count == 7`)
   continue to pass unchanged — confirms no regression/duplication for the `Call`/`Callvirt`
   paths.
6. `dotnet test test/FennecLabs.Instrumentation.Tests/` — all green.

## Related

- `FD-012` (archive) — renamed `Analyse` → `Analyze`, added `CancellationToken`; this FD
  builds on that same method.
- `src/FennecLabs.Instrumentation/AnalyseAssembly.cs` — sole file with the opcode filter.
