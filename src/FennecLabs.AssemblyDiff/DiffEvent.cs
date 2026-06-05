using Mono.Cecil;

namespace FennecLabs.AssemblyDiff;

public enum DiffKind { Added, Removed }

public enum DiffFlag
{
    IsPublic, IsAbstract, IsSealed, IsInterface,
    Visibility, IsVirtual, IsStatic,
}

public abstract record DiffEvent
{
    public abstract string FormatMessage();
}

public record InstructionDiff(int Index, string Instruction1, string Instruction2);

// — Assembly-level —

public record AssemblyNameDiff(string Was, string Is) : DiffEvent
{
    public override string FormatMessage() => $"Assembly name differs: '{Was}' vs '{Is}'";
}

public record AssemblyVersionDiff(string Was, string Is) : DiffEvent
{
    public override string FormatMessage() => $"Assembly version differs: '{Was}' vs '{Is}'";
}

public record AssemblyAttributePresenceDiff(string AttributeType, DiffKind Kind) : DiffEvent
{
    public override string FormatMessage() =>
        Kind == DiffKind.Removed
            ? $"Custom attribute only in Assembly1: {AttributeType}"
            : $"Custom attribute only in Assembly2: {AttributeType}";
}

public record AssemblyAttributeArgsDiff(string AttributeType) : DiffEvent
{
    public override string FormatMessage() =>
        $"Custom attribute '{AttributeType}': argument values differ";
}

public record ModulePresenceDiff(string ModuleName, DiffKind Kind) : DiffEvent
{
    public override string FormatMessage() =>
        Kind == DiffKind.Removed
            ? $"Module only in Assembly1: {ModuleName}"
            : $"Module only in Assembly2: {ModuleName}";
}

// — Type-level —

public record TypePresenceDiff(string TypeName, DiffKind Kind, string? DeclaringType = null) : DiffEvent
{
    public override string FormatMessage() =>
        Kind == DiffKind.Removed
            ? $"Type only in Assembly1: {TypeName}"
            : $"Type only in Assembly2: {TypeName}";
}

public record TypeFlagDiff(string TypeName, DiffFlag Flag, bool Was, bool Is) : DiffEvent
{
    public override string FormatMessage() => Flag switch
    {
        DiffFlag.IsPublic => $"Type '{TypeName}': Visibility differs (IsPublic: {Was} vs {Is})",
        _ => $"Type '{TypeName}': {Flag} differs ({Was} vs {Is})"
    };
}

public record TypeBaseTypeDiff(string TypeName, string? Was, string? Is) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}': BaseType differs ('{Was}' vs '{Is}')";
}

public record TypeInterfacePresenceDiff(string TypeName, string InterfaceType, DiffKind Kind) : DiffEvent
{
    public override string FormatMessage() =>
        Kind == DiffKind.Removed
            ? $"Type '{TypeName}': Interface only in Assembly1: {InterfaceType}"
            : $"Type '{TypeName}': Interface only in Assembly2: {InterfaceType}";
}

public record TypeSecurityDeclarationDiff(string TypeName, string Action, DiffKind Kind) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}': SecurityDeclaration '{Action}' " +
        (Kind == DiffKind.Removed ? "only in Assembly1" : "only in Assembly2");
}

// — Method-level —

public record MethodPresenceDiff(string TypeName, string Signature, DiffKind Kind) : DiffEvent
{
    public override string FormatMessage() =>
        Kind == DiffKind.Removed
            ? $"Type '{TypeName}': Method only in Assembly1: {Signature}"
            : $"Type '{TypeName}': Method only in Assembly2: {Signature}";
}

public record MethodFlagDiff(string TypeName, string Signature, DiffFlag Flag) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Method '{Signature}': {Flag} differs";
}

public record MethodImplAttrsDiff(
    string TypeName, string Signature,
    MethodImplAttributes Was, MethodImplAttributes Is) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Method '{Signature}': MethodImplAttributes differ ({Was} vs {Is})";
}

public record MethodBodyPresenceDiff(
    string TypeName, string Signature,
    bool PresentInAssembly1, bool PresentInAssembly2) : DiffEvent
{
    public override string FormatMessage() =>
        !PresentInAssembly1
            ? $"Type '{TypeName}', Method '{Signature}': Method1 has no body but Method2 does"
            : $"Type '{TypeName}', Method '{Signature}': Method2 has no body but Method1 does";
}

public record MethodBodyInstructionsDiff(
    string TypeName, string Signature,
    IReadOnlyList<InstructionDiff> Changes,
    IReadOnlyList<string> Instructions1,
    IReadOnlyList<string> Instructions2) : DiffEvent
{
    public override string FormatMessage() =>
        Instructions1.Count != Instructions2.Count
            ? $"Type '{TypeName}', Method '{Signature}': Instruction count differs " +
              $"({Instructions1.Count} vs {Instructions2.Count})"
            : $"Type '{TypeName}', Method '{Signature}': Method body differs";
}

public record MethodBodyLocalsDiff(string TypeName, string Signature, int Count1, int Count2) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Method '{Signature}': Local variable count differs ({Count1} vs {Count2})";
}

public record MethodBodyLocalVariableTypeDiff(
    string TypeName, string Signature, int Index, string Was, string Is) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Method '{Signature}': Local variable {Index} type differs ('{Was}' vs '{Is}')";
}

public record MethodBodyExceptionHandlersDiff(
    string TypeName, string Signature, int Count1, int Count2) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Method '{Signature}': Exception handler count differs ({Count1} vs {Count2})";
}

public record MethodPInvokeInfoDiff(string TypeName, string Signature, string? Was, string? Is) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Method '{Signature}': PInvokeInfo differs ('{Was}' vs '{Is}')";
}

public record MethodSecurityDeclarationDiff(
    string TypeName, string Signature, string Action, DiffKind Kind) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Method '{Signature}': SecurityDeclaration '{Action}' " +
        (Kind == DiffKind.Removed ? "only in Assembly1" : "only in Assembly2");
}

// — Field-level —

public record FieldPresenceDiff(string TypeName, string FieldName, string FieldType, DiffKind Kind) : DiffEvent
{
    public override string FormatMessage() =>
        Kind == DiffKind.Removed
            ? $"Type '{TypeName}': Field only in Assembly1: {FieldName} ({FieldType})"
            : $"Type '{TypeName}': Field only in Assembly2: {FieldName} ({FieldType})";
}

public record FieldTypeDiff(string TypeName, string FieldName, string Was, string Is) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Field '{FieldName}': Type differs ('{Was}' vs '{Is}')";
}

public record FieldFlagDiff(string TypeName, string FieldName, DiffFlag Flag) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Field '{FieldName}': {Flag} differs";
}

// — Property-level —

public record PropertyPresenceDiff(string TypeName, string PropertyName, DiffKind Kind) : DiffEvent
{
    public override string FormatMessage() =>
        Kind == DiffKind.Removed
            ? $"Type '{TypeName}': Property only in Assembly1: {PropertyName}"
            : $"Type '{TypeName}': Property only in Assembly2: {PropertyName}";
}

public record PropertyTypeDiff(string TypeName, string PropertyName, string Was, string Is) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Property '{PropertyName}': Type differs ('{Was}' vs '{Is}')";
}

public record PropertyAccessorDiff(string TypeName, string PropertyName, string Accessor, DiffKind Kind) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Property '{PropertyName}': {Accessor} presence differs";
}

// — Event-level —

public record EventPresenceDiff(string TypeName, string EventName, DiffKind Kind) : DiffEvent
{
    public override string FormatMessage() =>
        Kind == DiffKind.Removed
            ? $"Type '{TypeName}': Event only in Assembly1: {EventName}"
            : $"Type '{TypeName}': Event only in Assembly2: {EventName}";
}

public record EventTypeDiff(string TypeName, string EventName, string Was, string Is) : DiffEvent
{
    public override string FormatMessage() =>
        $"Type '{TypeName}', Event '{EventName}': Type differs ('{Was}' vs '{Is}')";
}
