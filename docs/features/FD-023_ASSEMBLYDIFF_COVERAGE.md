# FD-023: AssemblyDiff Test Coverage — DiffEvent Subtypes

**Status:** Open
**Priority:** Medium
**Effort:** Medium (2–4 hours)
**Impact:** Raises `FennecLabs.AssemblyDiff` from 61.9% to ~85%+ line coverage; validates
that `AssemblyComparer` emits the correct event type and `FormatMessage` produces the
correct string for every structural change category.

## Problem

`FennecLabs.AssemblyDiff` has 28 `DiffEvent` subtypes but only 10 are exercised by the
existing 28 tests. The following types have 0% line coverage despite being emitted by
`AssemblyComparer`:

| Category | 0% types |
|----------|----------|
| Assembly | `AssemblyNameDiff`, `AssemblyVersionDiff`, `AssemblyAttributePresenceDiff` |
| Type | `TypeBaseTypeDiff`, `TypeInterfacePresenceDiff` |
| Method | `MethodFlagDiff`, `MethodBodyPresenceDiff`, `MethodBodyLocalsDiff`, `MethodBodyLocalVariableTypeDiff`, `MethodBodyExceptionHandlersDiff` |
| Field | `FieldPresenceDiff`, `FieldTypeDiff`, `FieldFlagDiff` |
| Property | `PropertyPresenceDiff`, `PropertyTypeDiff`, `PropertyAccessorDiff` |
| Event | `EventPresenceDiff`, `EventTypeDiff` |

Additionally, several partially-covered types are missing branches:
- `TypeFlagDiff` (16%) — only `IsPublic` branch tested; `IsAbstract`, `IsSealed`, `IsInterface` untested
- `MethodPresenceDiff` (25%), `MethodImplAttrsDiff` (75%), `MethodPInvokeInfoDiff` (50%),
  `TypeSecurityDeclarationDiff` (33%), `MethodSecurityDeclarationDiff` (50%)

`ModulePresenceDiff` is intentionally skipped — multi-module assemblies are difficult to
construct in-memory and the event is rarely triggered in practice.

## Solution

### Two-file approach

**`DiffEventFormatMessageTests.cs`** — New file. Pure unit tests: instantiate each
zero-coverage record and assert `FormatMessage()` output. Covers both `DiffKind.Removed`
and `DiffKind.Added` branches where applicable. No Cecil required.

```csharp
[Fact]
public void AssemblyNameDiff_FormatMessage_ContainsBothNames()
{
    var diff = new AssemblyNameDiff("OldName", "NewName");
    Assert.Equal("Assembly name differs: 'OldName' vs 'NewName'", diff.FormatMessage());
}
```

**`AssemblyComparerTests.cs`** — Add comparer tests grouped by category (assembly,
type, method, field, property, event). These go through the full `AssemblyComparer` path
and validate that the correct event is emitted.

### Test groups

#### Assembly-level

- `Compare_AssemblyNameDiffers_EmitsAssemblyNameDiff` — set `assembly2.Name.Name`
- `Compare_AssemblyVersionDiffers_EmitsAssemblyVersionDiff` — set `assembly2.Name.Version`
- `Compare_AssemblyAttributeRemovedFromAssembly2_EmitsAttributePresenceDiffRemoved`
- `Compare_AssemblyAttributeOnlyInAssembly2_EmitsAttributePresenceDiffAdded`

#### Type-level

- `Compare_TypeBaseTypeChanged_EmitsTypeBaseTypeDiff` — change `TypeDefinition.BaseType`
- `Compare_InterfaceAddedToType_EmitsTypeInterfacePresenceDiffAdded`
- `Compare_InterfaceRemovedFromType_EmitsTypeInterfacePresenceDiffRemoved`
- `Compare_TypeAbstractChanged_EmitsTypeFlagDiff` — toggle `TypeAttributes.Abstract`
- `Compare_TypeSealedChanged_EmitsTypeFlagDiff` — toggle `TypeAttributes.Sealed`

#### Method-level

- `Compare_MethodVisibilityChanged_EmitsMethodFlagDiff`
- `Compare_MethodIsStaticChanged_EmitsMethodFlagDiff`
- `Compare_MethodBodyPresentInAssembly1Only_EmitsMethodBodyPresenceDiff`
- `Compare_MethodLocalCountDiffers_EmitsMethodBodyLocalsDiff`
- `Compare_MethodLocalVariableTypeDiffers_EmitsMethodBodyLocalVariableTypeDiff`
- `Compare_MethodExceptionHandlerCountDiffers_EmitsMethodBodyExceptionHandlersDiff`

#### Field-level

- `Compare_FieldAddedInAssembly2_EmitsFieldPresenceDiffAdded`
- `Compare_FieldRemovedFromAssembly2_EmitsFieldPresenceDiffRemoved`
- `Compare_FieldTypeChanged_EmitsFieldTypeDiff`
- `Compare_FieldVisibilityChanged_EmitsFieldFlagDiff`
- `Compare_FieldIsStaticChanged_EmitsFieldFlagDiff`

#### Property-level

- `Compare_PropertyAddedInAssembly2_EmitsPropertyPresenceDiffAdded`
- `Compare_PropertyRemovedFromAssembly2_EmitsPropertyPresenceDiffRemoved`
- `Compare_PropertyTypeChanged_EmitsPropertyTypeDiff`
- `Compare_PropertyGetterRemoved_EmitsPropertyAccessorDiff`
- `Compare_PropertySetterAdded_EmitsPropertyAccessorDiff`

#### Event-level

- `Compare_EventAddedInAssembly2_EmitsEventPresenceDiffAdded`
- `Compare_EventRemovedFromAssembly2_EmitsEventPresenceDiffRemoved`
- `Compare_EventTypeChanged_EmitsEventTypeDiff`

### Helpers to add to `AssemblyComparerTests.cs`

```csharp
private static FieldDefinition AddField(TypeDefinition type, string name, TypeReference fieldType,
    FieldAttributes attrs = FieldAttributes.Public)
{
    var field = new FieldDefinition(name, attrs, fieldType);
    type.Fields.Add(field);
    return field;
}

private static PropertyDefinition AddProperty(TypeDefinition type, string name,
    TypeReference propType, bool hasGetter = true, bool hasSetter = true)
{
    var property = new PropertyDefinition(name, PropertyAttributes.None, propType);
    if (hasGetter)
    {
        var getter = new MethodDefinition("get_" + name,
            MethodAttributes.Public | MethodAttributes.SpecialName, propType);
        getter.Body = new MethodBody(getter);
        getter.Body.GetILProcessor().Append(
            getter.Body.GetILProcessor().Create(OpCodes.Ret));
        type.Methods.Add(getter);
        property.GetMethod = getter;
    }
    if (hasSetter)
    {
        var setter = new MethodDefinition("set_" + name,
            MethodAttributes.Public | MethodAttributes.SpecialName,
            type.Module.TypeSystem.Void);
        setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, propType));
        setter.Body = new MethodBody(setter);
        setter.Body.GetILProcessor().Append(
            setter.Body.GetILProcessor().Create(OpCodes.Ret));
        type.Methods.Add(setter);
        property.SetMethod = setter;
    }
    type.Properties.Add(property);
    return property;
}

private static EventDefinition AddEvent(TypeDefinition type, string name, TypeReference eventType)
{
    var evt = new EventDefinition(name, EventAttributes.None, eventType);
    type.Events.Add(evt);
    return evt;
}
```

## Files to Create/Modify

| File | Action | Purpose |
|------|--------|---------|
| `test/FennecLabs.AssemblyDiff.Tests/DiffEventFormatMessageTests.cs` | CREATE | Pure `FormatMessage()` unit tests for all zero-coverage subtypes |
| `test/FennecLabs.AssemblyDiff.Tests/AssemblyComparerTests.cs` | MODIFY | Add comparer tests for assembly, type, method, field, property, event changes |

## Verification

1. `dotnet test test/FennecLabs.AssemblyDiff.Tests/` — all tests pass (expect ~55+ tests total)
2. `dotnet test --settings coverage.runsettings --collect:"XPlat Code Coverage" --results-directory ./TestResults`
3. `dotnet tool run reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:coverage-report -reporttypes:"TextSummary"`
4. `cat coverage-report/Summary.txt` — `FennecLabs.AssemblyDiff` line coverage ≥ 85%

## Related

- FD-021: Code coverage infrastructure — `coverage.runsettings` + `reportgenerator`
- FD-017: DiffEvent typed records — the test targets defined here
- FD-022: `FennecLabs.Cli` test coverage — parallel effort on the CLI layer
