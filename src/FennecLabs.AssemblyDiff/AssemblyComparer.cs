using Mono.Cecil;
using System.Text;

namespace FennecLabs.AssemblyDiff;

public class AssemblyComparer
{
    public AssemblyDefinition Assembly1 { get; }
    public AssemblyDefinition Assembly2 { get; }

    public AssemblyComparer(AssemblyDefinition assembly1, AssemblyDefinition assembly2)
    {
        Assembly1 = assembly1 ?? throw new ArgumentNullException(nameof(assembly1));
        Assembly2 = assembly2 ?? throw new ArgumentNullException(nameof(assembly2));
    }

    public AssemblyComparisonResult Compare()
    {
        var result = new AssemblyComparisonResult
        {
            Assembly1Name = Assembly1.FullName,
            Assembly2Name = Assembly2.FullName
        };

        CompareAssemblyAttributes(result);
        CompareModules(result);
        CompareTypes(result);

        return result;
    }

    private void CompareAssemblyAttributes(AssemblyComparisonResult result)
    {
        if (Assembly1.Name.Name != Assembly2.Name.Name)
            result.Events.Add(new AssemblyNameDiff(Assembly1.Name.Name, Assembly2.Name.Name));

        if (Assembly1.Name.Version != Assembly2.Name.Version)
            result.Events.Add(new AssemblyVersionDiff(
                Assembly1.Name.Version.ToString(), Assembly2.Name.Version.ToString()));

        var attrs1 = Assembly1.CustomAttributes.Select(a => a.AttributeType.FullName).ToHashSet();
        var attrs2 = Assembly2.CustomAttributes.Select(a => a.AttributeType.FullName).ToHashSet();

        foreach (var attr in attrs1.Except(attrs2))
            result.Events.Add(new AssemblyAttributePresenceDiff(attr, DiffKind.Removed));
        foreach (var attr in attrs2.Except(attrs1))
            result.Events.Add(new AssemblyAttributePresenceDiff(attr, DiffKind.Added));

        foreach (var attrTypeName in attrs1.Intersect(attrs2))
        {
            var args1 = Assembly1.CustomAttributes
                .Where(a => a.AttributeType.FullName == attrTypeName)
                .Select(GetCustomAttributeKey).OrderBy(k => k).ToList();
            var args2 = Assembly2.CustomAttributes
                .Where(a => a.AttributeType.FullName == attrTypeName)
                .Select(GetCustomAttributeKey).OrderBy(k => k).ToList();

            if (!args1.SequenceEqual(args2))
                result.Events.Add(new AssemblyAttributeArgsDiff(attrTypeName));
        }
    }

    private void CompareModules(AssemblyComparisonResult result)
    {
        var modules1 = Assembly1.Modules.Select(m => m.Name).ToHashSet();
        var modules2 = Assembly2.Modules.Select(m => m.Name).ToHashSet();

        foreach (var module in modules1.Except(modules2))
            result.Events.Add(new ModulePresenceDiff(module, DiffKind.Removed));
        foreach (var module in modules2.Except(modules1))
            result.Events.Add(new ModulePresenceDiff(module, DiffKind.Added));
    }

    private void CompareTypes(AssemblyComparisonResult result)
    {
        var types1 = GetTopLevelTypes(Assembly1).ToDictionary(t => t.FullName, t => t);
        var types2 = GetTopLevelTypes(Assembly2).ToDictionary(t => t.FullName, t => t);

        foreach (var typeName in types1.Keys.Except(types2.Keys))
            result.Events.Add(new TypePresenceDiff(typeName, DiffKind.Removed));
        foreach (var typeName in types2.Keys.Except(types1.Keys))
            result.Events.Add(new TypePresenceDiff(typeName, DiffKind.Added));

        foreach (var typeName in types1.Keys.Intersect(types2.Keys))
            CompareTypeDefinitions(types1[typeName], types2[typeName], result);
    }

    private void CompareTypeDefinitions(
        TypeDefinition type1, TypeDefinition type2, AssemblyComparisonResult result)
    {
        var typeName = type1.FullName;

        if (type1.IsPublic != type2.IsPublic)
            result.Events.Add(new TypeFlagDiff(typeName, DiffFlag.IsPublic, type1.IsPublic, type2.IsPublic));
        if (type1.IsAbstract != type2.IsAbstract)
            result.Events.Add(new TypeFlagDiff(typeName, DiffFlag.IsAbstract, type1.IsAbstract, type2.IsAbstract));
        if (type1.IsSealed != type2.IsSealed)
            result.Events.Add(new TypeFlagDiff(typeName, DiffFlag.IsSealed, type1.IsSealed, type2.IsSealed));
        if (type1.IsInterface != type2.IsInterface)
            result.Events.Add(new TypeFlagDiff(typeName, DiffFlag.IsInterface, type1.IsInterface, type2.IsInterface));

        if (type1.BaseType?.FullName != type2.BaseType?.FullName)
            result.Events.Add(new TypeBaseTypeDiff(typeName, type1.BaseType?.FullName, type2.BaseType?.FullName));

        var interfaces1 = type1.Interfaces.Select(i => i.InterfaceType.FullName).ToHashSet();
        var interfaces2 = type2.Interfaces.Select(i => i.InterfaceType.FullName).ToHashSet();
        foreach (var iface in interfaces1.Except(interfaces2))
            result.Events.Add(new TypeInterfacePresenceDiff(typeName, iface, DiffKind.Removed));
        foreach (var iface in interfaces2.Except(interfaces1))
            result.Events.Add(new TypeInterfacePresenceDiff(typeName, iface, DiffKind.Added));

        CompareTypeSecurityDeclarations(type1, type2, typeName, result);
        CompareMethods(type1, type2, typeName, result);
        CompareFields(type1, type2, typeName, result);
        CompareProperties(type1, type2, typeName, result);
        CompareEvents(type1, type2, typeName, result);
        CompareNestedTypes(type1, type2, typeName, result);
    }

    private static void CompareTypeSecurityDeclarations(
        TypeDefinition type1, TypeDefinition type2, string typeName, AssemblyComparisonResult result)
    {
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
    }

    private void CompareNestedTypes(
        TypeDefinition type1, TypeDefinition type2, string typeName, AssemblyComparisonResult result)
    {
        if (!type1.HasNestedTypes && !type2.HasNestedTypes)
            return;

        var nested1 = type1.NestedTypes.ToDictionary(t => t.Name);
        var nested2 = type2.NestedTypes.ToDictionary(t => t.Name);

        foreach (var name in nested1.Keys.Except(nested2.Keys))
            result.Events.Add(new TypePresenceDiff(nested1[name].FullName, DiffKind.Removed, typeName));
        foreach (var name in nested2.Keys.Except(nested1.Keys))
            result.Events.Add(new TypePresenceDiff(nested2[name].FullName, DiffKind.Added, typeName));
        foreach (var name in nested1.Keys.Intersect(nested2.Keys))
            CompareTypeDefinitions(nested1[name], nested2[name], result);
    }

    private void CompareMethods(
        TypeDefinition type1, TypeDefinition type2, string typeName, AssemblyComparisonResult result)
    {
        var methods1 = type1.Methods.ToDictionary(m => GetMethodSignature(m), m => m);
        var methods2 = type2.Methods.ToDictionary(m => GetMethodSignature(m), m => m);

        foreach (var methodSig in methods1.Keys.Except(methods2.Keys))
            result.Events.Add(new MethodPresenceDiff(typeName, methodSig, DiffKind.Removed));
        foreach (var methodSig in methods2.Keys.Except(methods1.Keys))
            result.Events.Add(new MethodPresenceDiff(typeName, methodSig, DiffKind.Added));

        foreach (var methodSig in methods1.Keys.Intersect(methods2.Keys))
        {
            var method1 = methods1[methodSig];
            var method2 = methods2[methodSig];

            if (method1.IsPublic != method2.IsPublic)
                result.Events.Add(new MethodFlagDiff(typeName, methodSig, DiffFlag.Visibility));
            if (method1.IsVirtual != method2.IsVirtual)
                result.Events.Add(new MethodFlagDiff(typeName, methodSig, DiffFlag.IsVirtual));
            if (method1.IsAbstract != method2.IsAbstract)
                result.Events.Add(new MethodFlagDiff(typeName, methodSig, DiffFlag.IsAbstract));
            if (method1.IsStatic != method2.IsStatic)
                result.Events.Add(new MethodFlagDiff(typeName, methodSig, DiffFlag.IsStatic));
            if (method1.ImplAttributes != method2.ImplAttributes)
                result.Events.Add(new MethodImplAttrsDiff(typeName, methodSig,
                    method1.ImplAttributes, method2.ImplAttributes));

            CompareMethodPInvokeInfo(method1, method2, typeName, methodSig, result);
            CompareMethodSecurityDeclarations(method1, method2, typeName, methodSig, result);
            CompareMethodBodies(method1, method2, typeName, methodSig, result);
        }
    }

    private static void CompareMethodPInvokeInfo(
        MethodDefinition method1, MethodDefinition method2,
        string typeName, string methodSig, AssemblyComparisonResult result)
    {
        var pinvoke1 = method1.HasPInvokeInfo ? FormatPInvokeInfo(method1.PInvokeInfo) : null;
        var pinvoke2 = method2.HasPInvokeInfo ? FormatPInvokeInfo(method2.PInvokeInfo) : null;
        if (pinvoke1 != pinvoke2)
            result.Events.Add(new MethodPInvokeInfoDiff(typeName, methodSig, pinvoke1, pinvoke2));
    }

    private static string FormatPInvokeInfo(PInvokeInfo info) =>
        $"{info.Module.Name}::{info.EntryPoint} [{info.Attributes}]";

    private static void CompareMethodSecurityDeclarations(
        MethodDefinition method1, MethodDefinition method2,
        string typeName, string methodSig, AssemblyComparisonResult result)
    {
        var secDecls1 = method1.HasSecurityDeclarations
            ? method1.SecurityDeclarations.Select(d => d.Action.ToString()).ToHashSet()
            : [];
        var secDecls2 = method2.HasSecurityDeclarations
            ? method2.SecurityDeclarations.Select(d => d.Action.ToString()).ToHashSet()
            : [];

        foreach (var action in secDecls1.Except(secDecls2))
            result.Events.Add(new MethodSecurityDeclarationDiff(typeName, methodSig, action, DiffKind.Removed));
        foreach (var action in secDecls2.Except(secDecls1))
            result.Events.Add(new MethodSecurityDeclarationDiff(typeName, methodSig, action, DiffKind.Added));
    }

    private void CompareMethodBodies(
        MethodDefinition method1, MethodDefinition method2,
        string typeName, string methodSig, AssemblyComparisonResult result)
    {
        if (!method1.HasBody && !method2.HasBody)
            return;
        if (!method1.HasBody)
        {
            result.Events.Add(new MethodBodyPresenceDiff(typeName, methodSig, false, true));
            return;
        }
        if (!method2.HasBody)
        {
            result.Events.Add(new MethodBodyPresenceDiff(typeName, methodSig, true, false));
            return;
        }

        var body1 = method1.Body;
        var body2 = method2.Body;

        if (body1.Instructions.Count != body2.Instructions.Count)
        {
            result.Events.Add(new MethodBodyInstructionsDiff(
                typeName, methodSig,
                Array.Empty<InstructionDiff>(),
                GetInstructionList(body1),
                GetInstructionList(body2)));
            CompareMethodBodyLocals(body1, body2, typeName, methodSig, result);
            CompareMethodBodyExceptionHandlers(body1, body2, typeName, methodSig, result);
            return;
        }

        var changes = new List<InstructionDiff>();
        for (int i = 0; i < body1.Instructions.Count; i++)
        {
            var inst1Str = GetInstructionString(body1.Instructions[i]);
            var inst2Str = GetInstructionString(body2.Instructions[i]);
            if (inst1Str != inst2Str)
                changes.Add(new InstructionDiff(i, inst1Str, inst2Str));
        }

        if (changes.Count > 0)
        {
            result.Events.Add(new MethodBodyInstructionsDiff(
                typeName, methodSig,
                changes,
                GetInstructionList(body1),
                GetInstructionList(body2)));
        }

        CompareMethodBodyLocals(body1, body2, typeName, methodSig, result);
        CompareMethodBodyExceptionHandlers(body1, body2, typeName, methodSig, result);
    }

    private static void CompareMethodBodyLocals(
        Mono.Cecil.Cil.MethodBody body1, Mono.Cecil.Cil.MethodBody body2,
        string typeName, string methodSig, AssemblyComparisonResult result)
    {
        if (body1.Variables.Count != body2.Variables.Count)
        {
            result.Events.Add(new MethodBodyLocalsDiff(
                typeName, methodSig, body1.Variables.Count, body2.Variables.Count));
            return;
        }

        for (int i = 0; i < body1.Variables.Count; i++)
        {
            var t1 = body1.Variables[i].VariableType.FullName;
            var t2 = body2.Variables[i].VariableType.FullName;
            if (t1 != t2)
                result.Events.Add(new MethodBodyLocalVariableTypeDiff(typeName, methodSig, i, t1, t2));
        }
    }

    private static void CompareMethodBodyExceptionHandlers(
        Mono.Cecil.Cil.MethodBody body1, Mono.Cecil.Cil.MethodBody body2,
        string typeName, string methodSig, AssemblyComparisonResult result)
    {
        if (body1.ExceptionHandlers.Count != body2.ExceptionHandlers.Count)
            result.Events.Add(new MethodBodyExceptionHandlersDiff(
                typeName, methodSig, body1.ExceptionHandlers.Count, body2.ExceptionHandlers.Count));
    }

    private string GetInstructionString(Mono.Cecil.Cil.Instruction instruction)
    {
        var sb = new StringBuilder();
        sb.Append(instruction.OpCode.ToString());

        if (instruction.Operand != null)
        {
            sb.Append(" ");
            if (instruction.Operand is Mono.Cecil.Cil.Instruction targetInst)
                sb.Append($"IL_{targetInst.Offset:X4}");
            else if (instruction.Operand is Mono.Cecil.Cil.Instruction[] targets)
            {
                sb.Append("[");
                sb.Append(string.Join(", ", targets.Select(t => $"IL_{t.Offset:X4}")));
                sb.Append("]");
            }
            else if (instruction.Operand is MethodReference methodRef)
                sb.Append($"{methodRef.DeclaringType?.FullName}::{methodRef.Name}");
            else if (instruction.Operand is FieldReference fieldRef)
                sb.Append($"{fieldRef.DeclaringType?.FullName}::{fieldRef.Name}");
            else if (instruction.Operand is TypeReference typeRef)
                sb.Append(typeRef.FullName);
            else if (instruction.Operand is string str)
                sb.Append($"\"{str}\"");
            else
                sb.Append(instruction.Operand.ToString());
        }

        return sb.ToString();
    }

    private List<string> GetInstructionList(Mono.Cecil.Cil.MethodBody body)
    {
        var instructions = new List<string>();
        foreach (var inst in body.Instructions)
            instructions.Add($"IL_{inst.Offset:X4}: {GetInstructionString(inst)}");
        return instructions;
    }

    private void CompareFields(
        TypeDefinition type1, TypeDefinition type2, string typeName, AssemblyComparisonResult result)
    {
        var fields1 = type1.Fields.Where(f => !f.Name.StartsWith("<"))
            .ToDictionary(f => f.Name, f => f);
        var fields2 = type2.Fields.Where(f => !f.Name.StartsWith("<"))
            .ToDictionary(f => f.Name, f => f);

        foreach (var fieldName in fields1.Keys.Except(fields2.Keys))
            result.Events.Add(new FieldPresenceDiff(
                typeName, fieldName, fields1[fieldName].FieldType.FullName, DiffKind.Removed));
        foreach (var fieldName in fields2.Keys.Except(fields1.Keys))
            result.Events.Add(new FieldPresenceDiff(
                typeName, fieldName, fields2[fieldName].FieldType.FullName, DiffKind.Added));

        foreach (var fieldName in fields1.Keys.Intersect(fields2.Keys))
        {
            var field1 = fields1[fieldName];
            var field2 = fields2[fieldName];

            if (field1.FieldType.FullName != field2.FieldType.FullName)
                result.Events.Add(new FieldTypeDiff(
                    typeName, fieldName, field1.FieldType.FullName, field2.FieldType.FullName));
            if (field1.IsPublic != field2.IsPublic)
                result.Events.Add(new FieldFlagDiff(typeName, fieldName, DiffFlag.Visibility));
            if (field1.IsStatic != field2.IsStatic)
                result.Events.Add(new FieldFlagDiff(typeName, fieldName, DiffFlag.IsStatic));
        }
    }

    private void CompareProperties(
        TypeDefinition type1, TypeDefinition type2, string typeName, AssemblyComparisonResult result)
    {
        var properties1 = type1.Properties.ToDictionary(p => p.FullName, p => p);
        var properties2 = type2.Properties.ToDictionary(p => p.FullName, p => p);

        foreach (var propName in properties1.Keys.Except(properties2.Keys))
            result.Events.Add(new PropertyPresenceDiff(typeName, propName, DiffKind.Removed));
        foreach (var propName in properties2.Keys.Except(properties1.Keys))
            result.Events.Add(new PropertyPresenceDiff(typeName, propName, DiffKind.Added));

        foreach (var propName in properties1.Keys.Intersect(properties2.Keys))
        {
            var prop1 = properties1[propName];
            var prop2 = properties2[propName];

            if (prop1.PropertyType.FullName != prop2.PropertyType.FullName)
                result.Events.Add(new PropertyTypeDiff(
                    typeName, propName, prop1.PropertyType.FullName, prop2.PropertyType.FullName));

            if ((prop1.GetMethod != null) != (prop2.GetMethod != null))
            {
                var kind = prop1.GetMethod != null ? DiffKind.Removed : DiffKind.Added;
                result.Events.Add(new PropertyAccessorDiff(typeName, propName, "Getter", kind));
            }
            if ((prop1.SetMethod != null) != (prop2.SetMethod != null))
            {
                var kind = prop1.SetMethod != null ? DiffKind.Removed : DiffKind.Added;
                result.Events.Add(new PropertyAccessorDiff(typeName, propName, "Setter", kind));
            }
        }
    }

    private void CompareEvents(
        TypeDefinition type1, TypeDefinition type2, string typeName, AssemblyComparisonResult result)
    {
        var events1 = type1.Events.ToDictionary(e => e.FullName, e => e);
        var events2 = type2.Events.ToDictionary(e => e.FullName, e => e);

        foreach (var eventName in events1.Keys.Except(events2.Keys))
            result.Events.Add(new EventPresenceDiff(typeName, eventName, DiffKind.Removed));
        foreach (var eventName in events2.Keys.Except(events1.Keys))
            result.Events.Add(new EventPresenceDiff(typeName, eventName, DiffKind.Added));

        foreach (var eventName in events1.Keys.Intersect(events2.Keys))
        {
            var event1 = events1[eventName];
            var event2 = events2[eventName];
            if (event1.EventType.FullName != event2.EventType.FullName)
                result.Events.Add(new EventTypeDiff(
                    typeName, eventName, event1.EventType.FullName, event2.EventType.FullName));
        }
    }

    private static IEnumerable<TypeDefinition> GetTopLevelTypes(AssemblyDefinition assembly) =>
        assembly.Modules.SelectMany(m => m.Types);

    private string GetMethodSignature(MethodDefinition method)
    {
        var sb = new StringBuilder();
        sb.Append(method.ReturnType.FullName);
        sb.Append(" ");
        sb.Append(method.Name);
        sb.Append("(");
        sb.Append(string.Join(", ", method.Parameters.Select(p =>
        {
            var attrPrefix = p.Attributes != ParameterAttributes.None
                ? $"[{p.Attributes}] "
                : "";
            return $"{attrPrefix}{p.ParameterType.FullName}";
        })));
        sb.Append(")");

        if (method.HasGenericParameters)
        {
            var constraints = method.GenericParameters.Select(gp =>
                gp.HasConstraints
                    ? $"{gp.Name}:{string.Join("&", gp.Constraints.Select(c => c.ConstraintType.FullName))}"
                    : gp.Name);
            sb.Append($" where {string.Join(", ", constraints)}");
        }

        return sb.ToString();
    }

    private static string GetCustomAttributeKey(CustomAttribute attr)
    {
        var ctorArgs = string.Join(", ",
            attr.ConstructorArguments.Select(a => a.Value?.ToString() ?? "null"));
        var namedArgs = string.Join(", ",
            attr.Fields.Select(f => $"{f.Name}={f.Argument.Value?.ToString() ?? "null"}")
                .Concat(attr.Properties.Select(p => $"{p.Name}={p.Argument.Value?.ToString() ?? "null"}"))
                .OrderBy(s => s));
        return namedArgs.Length > 0 ? $"({ctorArgs})|{namedArgs}" : ctorArgs;
    }
}
