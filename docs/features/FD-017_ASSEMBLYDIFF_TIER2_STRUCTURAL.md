# FD-017: AssemblyDiff Tier 2 — Structured Diff Records, File Split, PInvokeInfo, Generic Constraints

**Status:** Pending Verification
**Priority:** Medium
**Effort:** High (6–10 hours)
**Impact:** Replaces the stringly-typed `List<string> Differences` API with a typed
`List<DiffEvent>` model, making diffs filterable, renderable, and serializable without
string parsing. Also splits the 689-line monolith into three files and adds two missing
comparisons (P/Invoke metadata, generic constraints).

## Problem

### 1. Stringly-typed diff output

Every difference `AssemblyComparer` detects is emitted as a human-readable string:

```csharp
result.Differences.Add($"Type '{typeName}', Method '{methodSig}': IsVirtual differs");
```

Consumers — the CLI renderer, the JSON serializer, and every test — must parse these
strings to extract meaning:

```csharp
Assert.Contains(result.Differences, d => d.Contains("ImplAttributes")); // fragile
```

The MCP server (FD-013) will need to answer questions like "what types were added?" or
"which methods changed IL?" without round-tripping through string parsing.

The JSON schema exposed by `compare` and `reproduce` emits `differences: string[]` — a
flat array that is already inadequate for structured consumers.

### 2. Three classes in one 689-line file

`AssemblyComparer.cs` contains `AssemblyComparer`, `AssemblyComparisonResult`, and
`MethodBodyDifference` with no file-level separation. This makes navigation and future
splitting harder than necessary.

### 3. Missing comparisons

| Gap | Method | Cecil API |
|-----|--------|-----------|
| P/Invoke metadata (`[DllImport]`) | `CompareMethods` | `method.HasPInvokeInfo`, `method.PInvokeInfo` |
| Generic parameter constraints (`where T : ...`) | `GetMethodSignature` | `method.GenericParameters[i].Constraints` |
| Security declarations (`DeclSecurityAttributes`) | `CompareTypeDefinitions`, `CompareMethods` | `type.HasSecurityDeclarations`, `type.SecurityDeclarations` |
| Nested type recursion | `CompareTypeDefinitions` | `type.HasNestedTypes`, `type.NestedTypes` |

## Solution

### Phase 1 — File split (zero behavioral change)

Move the three existing classes to their own files. No logic changes.

| New file | Contains |
|----------|----------|
| `AssemblyComparer.cs` | `AssemblyComparer` (already there, keeps all 15 private methods) |
| `AssemblyComparisonResult.cs` | `AssemblyComparisonResult` class |
| `MethodBodyDifference.cs` | `MethodBodyDifference` class |

Commit as a standalone structural commit before Phase 2 so the diff for the data model
change is clean.

### Phase 2 — DiffEvent model

#### 2a. Define `DiffEvent.cs`

```csharp
public enum DiffKind { Added, Removed, Changed }

public abstract record DiffEvent
{
    public abstract string FormatMessage();
}

// — Assembly-level —
public record AssemblyNameDiff(string Was, string Is) : DiffEvent { ... }
public record AssemblyVersionDiff(string Was, string Is) : DiffEvent { ... }
public record AssemblyAttributePresenceDiff(string AttributeType, DiffKind Kind) : DiffEvent { ... }
public record AssemblyAttributeArgsDiff(string AttributeType) : DiffEvent { ... }
public record ModulePresenceDiff(string ModuleName, DiffKind Kind) : DiffEvent { ... }

// — Type-level —
public record TypePresenceDiff(string TypeName, DiffKind Kind, string? DeclaringType = null) : DiffEvent { ... }
public record TypeFlagDiff(string TypeName, string Flag, bool Was, bool Is) : DiffEvent { ... }
public record TypeBaseTypeDiff(string TypeName, string? Was, string? Is) : DiffEvent { ... }
public record TypeInterfacePresenceDiff(string TypeName, string InterfaceType, DiffKind Kind) : DiffEvent { ... }

// — Method-level —
public record MethodPresenceDiff(string TypeName, string Signature, DiffKind Kind) : DiffEvent { ... }
public record MethodFlagDiff(string TypeName, string Signature, string Flag) : DiffEvent { ... }
public record MethodImplAttrsDiff(string TypeName, string Signature,
    MethodImplAttributes Was, MethodImplAttributes Is) : DiffEvent { ... }
public record MethodBodyPresenceDiff(string TypeName, string Signature,
    bool PresentInAssembly1, bool PresentInAssembly2) : DiffEvent { ... }
public record MethodBodyInstructionCountDiff(string TypeName, string Signature,
    int Count1, int Count2) : DiffEvent { ... }
public record MethodBodyInstructionsDiff(string TypeName, string Signature,
    IReadOnlyList<InstructionDiff> Changes,
    IReadOnlyList<string> Instructions1,
    IReadOnlyList<string> Instructions2) : DiffEvent { ... }
public record MethodBodyLocalsDiff(string TypeName, string Signature,
    int Count1, int Count2) : DiffEvent { ... }
public record MethodBodyExceptionHandlersDiff(string TypeName, string Signature,
    int Count1, int Count2) : DiffEvent { ... }
// New in this FD:
public record MethodPInvokeInfoDiff(string TypeName, string Signature,
    string? Was, string? Is) : DiffEvent { ... }
public record MethodGenericConstraintsDiff(string TypeName, string Signature,
    string Was, string Is) : DiffEvent { ... }
public record MethodSecurityDeclarationDiff(string TypeName, string Signature,
    string Action, DiffKind Kind) : DiffEvent { ... }

// — Type-level security —
public record TypeSecurityDeclarationDiff(string TypeName,
    string Action, DiffKind Kind) : DiffEvent { ... }

// — Field-level —
public record FieldPresenceDiff(string TypeName, string FieldName,
    string FieldType, DiffKind Kind) : DiffEvent { ... }
public record FieldTypeDiff(string TypeName, string FieldName,
    string Was, string Is) : DiffEvent { ... }
public record FieldFlagDiff(string TypeName, string FieldName, string Flag) : DiffEvent { ... }

// — Property-level —
public record PropertyPresenceDiff(string TypeName, string PropertyName,
    DiffKind Kind) : DiffEvent { ... }
public record PropertyTypeDiff(string TypeName, string PropertyName,
    string Was, string Is) : DiffEvent { ... }
public record PropertyAccessorDiff(string TypeName, string PropertyName,
    string Accessor, DiffKind Kind) : DiffEvent { ... }

// — Event-level —
public record EventPresenceDiff(string TypeName, string EventName,
    DiffKind Kind) : DiffEvent { ... }
public record EventTypeDiff(string TypeName, string EventName,
    string Was, string Is) : DiffEvent { ... }

// — IL-level (replaces List<string> InstructionDifferences) —
public record InstructionDiff(int Index, string Instruction1, string Instruction2);
```

Each `FormatMessage()` produces the same human-readable string the current code emits,
so the rendered CLI output stays identical.

#### 2b. Rewrite `AssemblyComparisonResult`

Replace `List<string> Differences` and the two `HashSet<string> TypesOnly*` with a
single `List<DiffEvent> Events`. Derive the convenience views from it:

```csharp
public class AssemblyComparisonResult
{
    public string Assembly1Name { get; set; } = string.Empty;
    public string Assembly2Name { get; set; } = string.Empty;
    public List<DiffEvent> Events { get; } = new();

    public bool AreEqual => Events.Count == 0;

    // Derived views — no separate storage
    public IEnumerable<string> TypesOnlyInAssembly1 =>
        Events.OfType<TypePresenceDiff>()
              .Where(e => e.Kind == DiffKind.Removed)
              .Select(e => e.TypeName);

    public IEnumerable<string> TypesOnlyInAssembly2 =>
        Events.OfType<TypePresenceDiff>()
              .Where(e => e.Kind == DiffKind.Added)
              .Select(e => e.TypeName);

    public IEnumerable<MethodBodyInstructionsDiff> MethodBodyChanges =>
        Events.OfType<MethodBodyInstructionsDiff>();

    public string GenerateReport() { /* calls e.FormatMessage() per event */ }
}
```

Remove `MethodBodyDifference.cs` — `MethodBodyInstructionsDiff` supersedes it.

#### 2c. Rewrite all 15 private comparer methods

Replace every `result.Differences.Add(...)` and `result.MethodBodyDifferences.Add(...)`
with `result.Events.Add(new SomeTypedDiff(...))`.

For nested types, populate `TypePresenceDiff.DeclaringType` when the type has a declaring
type in Cecil (`type.DeclaringType?.FullName`), enabling renderers to show hierarchy.

### Phase 3 — Update CLI consumers

#### DiffRenderer

`result.Differences.Count` → `result.Events.Count`

Typed event access replaces string-based counts where the renderer already has structured
views:

```csharp
foreach (var t in result.TypesOnlyInAssembly1.Take(5))   // still works (derived property)
foreach (var m in result.MethodBodyChanges.Take(3))        // replaces MethodBodyDifferences
    AnsiConsole.MarkupLine($"~ changed: {m.TypeName}.{m.Signature}");
```

#### JSON schema (`compare` and `reproduce` commands)

Replace:

```json
{
  "differences": ["Type 'Foo': Method only in Assembly1: Bar()"],
  "methodBodyDifferences": [{ "instructionDifferences": ["  Instruction 0: 'ldarg.0' vs 'ldnull'"] }]
}
```

With:

```json
{
  "events": [
    { "type": "MethodPresenceDiff", "typeName": "Foo", "signature": "Bar()", "kind": "Removed" }
  ],
  "methodBodyChanges": [
    {
      "typeName": "Foo", "signature": "Bar()",
      "instructionDiffs": [{ "index": 0, "instruction1": "ldarg.0", "instruction2": "ldnull" }]
    }
  ]
}
```

The `differences` string array is removed. Old `result.json` cache files written before
this FD are invalidated — use `--no-cache` to regenerate them.

### Phase 4 — New comparisons

#### P/Invoke metadata

In `CompareMethods`, after the existing `ImplAttributes` check:

```csharp
var pinvoke1 = method1.HasPInvokeInfo ? FormatPInvokeInfo(method1.PInvokeInfo) : null;
var pinvoke2 = method2.HasPInvokeInfo ? FormatPInvokeInfo(method2.PInvokeInfo) : null;
if (pinvoke1 != pinvoke2)
    result.Events.Add(new MethodPInvokeInfoDiff(typeName, methodSig, pinvoke1, pinvoke2));

private static string FormatPInvokeInfo(PInvokeInfo info) =>
    $"{info.Module.Name}::{info.EntryPoint} [{info.Attributes}]";
```

#### Generic constraints

In `GetMethodSignature`, append generic parameter constraints after the parameter list:

```csharp
if (method.HasGenericParameters)
{
    var constraints = method.GenericParameters
        .Select(gp => gp.HasConstraints
            ? $"{gp.Name}:{string.Join("&", gp.Constraints.Select(c => c.ConstraintType.FullName))}"
            : gp.Name);
    sb.Append($" where {string.Join(", ", constraints)}");
}
```

Two methods that differ only in `where T : IDisposable` vs `where T : IComparable` will
now produce different signature keys and be treated as distinct methods.

#### Security declarations

In `CompareTypeDefinitions`, after the existing interface comparison, diff
`DeclSecurityAttributes` on types:

```csharp
var secDecls1 = type1.HasSecurityDeclarations
    ? type1.SecurityDeclarations.Select(d => d.Action.ToString()).ToHashSet()
    : [];
var secDecls2 = type2.HasSecurityDeclarations
    ? type2.SecurityDeclarations.Select(d => d.Action.ToString()).ToHashSet()
    : [];
foreach (var action in secDecls1.Except(secDecls2))
    result.Events.Add(new TypeSecurityDeclarationDiff(typeName, action, DiffKind.Removed));
foreach (var action in secDecls2.Except(secDecls1))
    result.Events.Add(new TypeSecurityDeclarationDiff(typeName, action, DiffKind.Added));
```

Apply the same pattern in `CompareMethods` for method-level security declarations,
emitting `MethodSecurityDeclarationDiff`.

#### Nested type recursion

In `CompareTypeDefinitions`, after comparing the type's own members, recurse into nested
types that appear in both assemblies:

```csharp
if (type1.HasNestedTypes || type2.HasNestedTypes)
{
    var nested1 = type1.NestedTypes.ToDictionary(t => t.Name);
    var nested2 = type2.NestedTypes.ToDictionary(t => t.Name);

    foreach (var name in nested1.Keys.Except(nested2.Keys))
        result.Events.Add(new TypePresenceDiff(nested1[name].FullName, DiffKind.Removed, typeName));
    foreach (var name in nested2.Keys.Except(nested1.Keys))
        result.Events.Add(new TypePresenceDiff(nested2[name].FullName, DiffKind.Added, typeName));
    foreach (var name in nested1.Keys.Intersect(nested2.Keys))
        CompareTypeDefinitions(nested1[name], nested2[name], result);
}
```

This ensures nested types like compiler-generated state machines and inner classes are
compared structurally rather than only appearing as flat `TypePresenceDiff` entries.

### Phase 5 — Tests

Every change in this FD requires dedicated test coverage. Tests are in
`test/FennecLabs.AssemblyDiff.Tests/AssemblyComparerTests.cs`.

#### Phase 2 — DiffEvent model migration

Rewrite all 13 existing tests to assert on typed events instead of string contains:

```csharp
// Before
Assert.Contains(result.Differences, d => d.Contains("Assembly2"));

// After
Assert.Contains(result.Events.OfType<TypePresenceDiff>(),
    e => e.TypeName == "TestNamespace.AddedType" && e.Kind == DiffKind.Added);
```

Every comparer method must have at least one test that asserts on a named typed event —
no `result.Events.Count > 0` assertions, and no `d.Contains(...)` string sniffing.

#### Phase 3 — JSON schema

- `CompareCommand_JsonOutput_ContainsEventsArray` — `fennec compare` JSON contains
  `events[]` with typed objects; `differences` key is absent.
- `CompareCommand_JsonOutput_MethodBodyChangesTyped` — `methodBodyChanges[]` entries
  include `instructionDiffs` with `index`, `instruction1`, `instruction2` fields.

#### Phase 4 — New comparisons

- `Compare_PInvokeInfoDiffers_EmitsMethodPInvokeInfoDiff`
- `Compare_PInvokeInfoUnchanged_NoEvent` — equal P/Invoke metadata emits nothing
- `Compare_GenericConstraintsDiffer_TreatedAsDistinctMethods`
- `Compare_GenericConstraintsUnchanged_NoSpuriousEvent`
- `Compare_TypeSecurityDeclarationAdded_EmitsTypeSecurityDeclarationDiff`
- `Compare_TypeSecurityDeclarationRemoved_EmitsTypeSecurityDeclarationDiff`
- `Compare_MethodSecurityDeclarationAdded_EmitsMethodSecurityDeclarationDiff`
- `Compare_MethodSecurityDeclarationRemoved_EmitsMethodSecurityDeclarationDiff`
- `Compare_NestedTypeOnlyInAssembly2_EmitsTypePresenceDiffWithDeclaringType`
- `Compare_NestedTypeOnlyInAssembly1_EmitsTypePresenceDiffWithDeclaringType`
- `Compare_NestedTypeModified_RecursesIntoNestedType` — member change inside a nested
  type appears as a typed event with the nested type's name, not the outer type's
- `Compare_MethodBodyInstructionsDiff_InstructionDiffsAreTyped` (not just strings)

#### Coverage rule

Each new comparer branch (PInvokeInfo, generic constraints, security declarations, nested
recursion) must have both a positive test (difference detected) and a negative test (no
spurious event when values are equal). Tests that only verify the happy path will be
rejected at review.

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/FennecLabs.AssemblyDiff/AssemblyComparisonResult.cs` | CREATE | Move class from monolith; switch to `List<DiffEvent> Events` |
| `src/FennecLabs.AssemblyDiff/DiffEvent.cs` | CREATE | All `DiffEvent` subtypes + `InstructionDiff` record |
| `src/FennecLabs.AssemblyDiff/AssemblyComparer.cs` | MODIFY | Emit typed events; add PInvokeInfo + generic constraint comparisons |
| `src/FennecLabs.AssemblyDiff/MethodBodyDifference.cs` | DELETE | Superseded by `MethodBodyInstructionsDiff` event |
| `src/FennecLabs.Cli/Rendering/DiffRenderer.cs` | MODIFY | Use `result.Events.Count`, `result.MethodBodyChanges` |
| `src/FennecLabs.Cli/Commands/CompareCommandHandler.cs` | MODIFY | Emit `events[]` + `methodBodyChanges[]` in JSON; remove `differences` |
| `src/FennecLabs.Cli/Commands/ReproduceCommandHandler.cs` | MODIFY | Same as CompareCommandHandler |
| `test/FennecLabs.AssemblyDiff.Tests/AssemblyComparerTests.cs` | MODIFY | Rewrite all 13 existing assertions to typed events; add 12 new tests (Phase 3, 4, 5) |

## Verification

1. `dotnet build` — 0 errors, 0 warnings.
2. `dotnet test test/FennecLabs.AssemblyDiff.Tests/` — all tests green including new ones.
3. `fennec compare --nuget Polly --version 8.5.0 --format json` — output contains `events[]`
   array with typed objects; `differences` field absent.
4. `fennec compare --nuget Polly --version 8.5.0` (human mode) — rendered output identical
   to pre-FD (same text, same colors).
5. Grep for `result.Differences` in CLI source — zero hits (fully replaced).
6. Grep for `Contains("` in test file — zero hits (all assertions are typed).
7. Find a package with `[DllImport]` methods and confirm `MethodPInvokeInfoDiff` events
   appear when the entry point or attributes differ between versions.
8. Grep for `HasSecurityDeclarations` in `AssemblyComparer.cs` — confirms security
   declaration diffing is wired up for both types and methods.
9. Compare two assemblies where a nested type was added or modified — confirm
   `TypePresenceDiff` events carry the correct `DeclaringType` and that nested member
   changes (e.g. a method body change inside the nested type) also appear in `events[]`.

## Implementation Order

1. Phase 1 (file split, commit alone)
2. Phase 2 (DiffEvent model — library only, tests still pass via derived properties)
3. Phase 4 (new comparisons — piggyback on the new event types)
4. Phase 3 (CLI consumers)
5. Phase 5 (update + extend tests)

## Related

- `docs/optimizations-assdiff.md` — source analysis this FD implements
- FD-011 — completed Tier 1 (custom attr args, MethodImplAttributes, param attrs)
- FD-013 — MCP server; primary beneficiary of the typed event model
- FD-015 — established JSON schema that this FD's Phase 3 will update
- FD-016 — result.json caching; old files invalidated by schema change, `--no-cache` is the escape hatch
