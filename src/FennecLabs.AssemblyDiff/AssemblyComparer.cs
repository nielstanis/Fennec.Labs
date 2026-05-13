using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FennecLabs.AssemblyDiff;

/// <summary>
/// Compares two AssemblyDefinitions and reports differences including:
/// - Assembly attributes and versions
/// - Modules
/// - Types (classes, interfaces, etc.)
/// - Type members (methods, fields, properties, events)
/// - Method bodies (IL instructions)
/// </summary>
/// <example>
/// Usage:
/// <code>
/// var assembly1 = AssemblyDefinition.ReadAssembly("path/to/v1.dll");
/// var assembly2 = AssemblyDefinition.ReadAssembly("path/to/v2.dll");
///
/// var comparer = new AssemblyComparer(assembly1, assembly2);
/// var result = comparer.Compare();
///
/// Console.WriteLine(result.GenerateReport());
///
/// // Access detailed method body differences
/// foreach (var bodyDiff in result.MethodBodyDifferences)
/// {
///     Console.WriteLine(bodyDiff.GenerateDetailedReport());
/// }
/// </code>
/// </example>
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
        // Compare assembly name and version
        if (Assembly1.Name.Name != Assembly2.Name.Name)
        {
            result.Differences.Add($"Assembly name differs: '{Assembly1.Name.Name}' vs '{Assembly2.Name.Name}'");
        }

        if (Assembly1.Name.Version != Assembly2.Name.Version)
        {
            result.Differences.Add($"Assembly version differs: '{Assembly1.Name.Version}' vs '{Assembly2.Name.Version}'");
        }

        // Compare custom attributes
        var attrs1 = Assembly1.CustomAttributes.Select(a => a.AttributeType.FullName).ToHashSet();
        var attrs2 = Assembly2.CustomAttributes.Select(a => a.AttributeType.FullName).ToHashSet();

        foreach (var attr in attrs1.Except(attrs2))
        {
            result.Differences.Add($"Custom attribute only in Assembly1: {attr}");
        }

        foreach (var attr in attrs2.Except(attrs1))
        {
            result.Differences.Add($"Custom attribute only in Assembly2: {attr}");
        }

        // Compare constructor arguments for attributes present in both assemblies
        foreach (var attrTypeName in attrs1.Intersect(attrs2))
        {
            var args1 = Assembly1.CustomAttributes
                .Where(a => a.AttributeType.FullName == attrTypeName)
                .Select(GetCustomAttributeKey)
                .OrderBy(k => k)
                .ToList();
            var args2 = Assembly2.CustomAttributes
                .Where(a => a.AttributeType.FullName == attrTypeName)
                .Select(GetCustomAttributeKey)
                .OrderBy(k => k)
                .ToList();

            if (!args1.SequenceEqual(args2))
            {
                result.Differences.Add($"Custom attribute '{attrTypeName}': argument values differ");
            }
        }
    }

    private void CompareModules(AssemblyComparisonResult result)
    {
        var modules1 = Assembly1.Modules.Select(m => m.Name).ToHashSet();
        var modules2 = Assembly2.Modules.Select(m => m.Name).ToHashSet();

        foreach (var module in modules1.Except(modules2))
        {
            result.Differences.Add($"Module only in Assembly1: {module}");
        }

        foreach (var module in modules2.Except(modules1))
        {
            result.Differences.Add($"Module only in Assembly2: {module}");
        }
    }

    private void CompareTypes(AssemblyComparisonResult result)
    {
        var types1 = GetAllTypes(Assembly1).ToDictionary(t => t.FullName, t => t);
        var types2 = GetAllTypes(Assembly2).ToDictionary(t => t.FullName, t => t);

        // Find types only in Assembly1
        foreach (var typeName in types1.Keys.Except(types2.Keys))
        {
            result.TypesOnlyInAssembly1.Add(typeName);
            result.Differences.Add($"Type only in Assembly1: {typeName}");
        }

        // Find types only in Assembly2
        foreach (var typeName in types2.Keys.Except(types1.Keys))
        {
            result.TypesOnlyInAssembly2.Add(typeName);
            result.Differences.Add($"Type only in Assembly2: {typeName}");
        }

        // Compare common types
        foreach (var typeName in types1.Keys.Intersect(types2.Keys))
        {
            CompareTypeDefinitions(types1[typeName], types2[typeName], result);
        }
    }

    private void CompareTypeDefinitions(TypeDefinition type1, TypeDefinition type2, AssemblyComparisonResult result)
    {
        string typeName = type1.FullName;

        // Compare type attributes
        if (type1.IsPublic != type2.IsPublic)
        {
            result.Differences.Add($"Type '{typeName}': Visibility differs (IsPublic: {type1.IsPublic} vs {type2.IsPublic})");
        }

        if (type1.IsAbstract != type2.IsAbstract)
        {
            result.Differences.Add($"Type '{typeName}': IsAbstract differs ({type1.IsAbstract} vs {type2.IsAbstract})");
        }

        if (type1.IsSealed != type2.IsSealed)
        {
            result.Differences.Add($"Type '{typeName}': IsSealed differs ({type1.IsSealed} vs {type2.IsSealed})");
        }

        if (type1.IsInterface != type2.IsInterface)
        {
            result.Differences.Add($"Type '{typeName}': IsInterface differs ({type1.IsInterface} vs {type2.IsInterface})");
        }

        // Compare base type
        if (type1.BaseType?.FullName != type2.BaseType?.FullName)
        {
            result.Differences.Add($"Type '{typeName}': BaseType differs ('{type1.BaseType?.FullName}' vs '{type2.BaseType?.FullName}')");
        }

        // Compare interfaces
        var interfaces1 = type1.Interfaces.Select(i => i.InterfaceType.FullName).ToHashSet();
        var interfaces2 = type2.Interfaces.Select(i => i.InterfaceType.FullName).ToHashSet();

        foreach (var iface in interfaces1.Except(interfaces2))
        {
            result.Differences.Add($"Type '{typeName}': Interface only in Assembly1: {iface}");
        }

        foreach (var iface in interfaces2.Except(interfaces1))
        {
            result.Differences.Add($"Type '{typeName}': Interface only in Assembly2: {iface}");
        }

        // Compare methods
        CompareMethods(type1, type2, typeName, result);

        // Compare fields
        CompareFields(type1, type2, typeName, result);

        // Compare properties
        CompareProperties(type1, type2, typeName, result);

        // Compare events
        CompareEvents(type1, type2, typeName, result);
    }

    private void CompareMethods(TypeDefinition type1, TypeDefinition type2, string typeName, AssemblyComparisonResult result)
    {
        var methods1 = type1.Methods.ToDictionary(m => GetMethodSignature(m), m => m);
        var methods2 = type2.Methods.ToDictionary(m => GetMethodSignature(m), m => m);

        foreach (var methodSig in methods1.Keys.Except(methods2.Keys))
        {
            result.Differences.Add($"Type '{typeName}': Method only in Assembly1: {methodSig}");
        }

        foreach (var methodSig in methods2.Keys.Except(methods1.Keys))
        {
            result.Differences.Add($"Type '{typeName}': Method only in Assembly2: {methodSig}");
        }

        // Compare common methods
        foreach (var methodSig in methods1.Keys.Intersect(methods2.Keys))
        {
            var method1 = methods1[methodSig];
            var method2 = methods2[methodSig];

            if (method1.IsPublic != method2.IsPublic)
            {
                result.Differences.Add($"Type '{typeName}', Method '{methodSig}': Visibility differs");
            }

            if (method1.IsVirtual != method2.IsVirtual)
            {
                result.Differences.Add($"Type '{typeName}', Method '{methodSig}': IsVirtual differs");
            }

            if (method1.IsAbstract != method2.IsAbstract)
            {
                result.Differences.Add($"Type '{typeName}', Method '{methodSig}': IsAbstract differs");
            }

            if (method1.IsStatic != method2.IsStatic)
            {
                result.Differences.Add($"Type '{typeName}', Method '{methodSig}': IsStatic differs");
            }

            if (method1.ImplAttributes != method2.ImplAttributes)
            {
                result.Differences.Add(
                    $"Type '{typeName}', Method '{methodSig}': MethodImplAttributes differ " +
                    $"({method1.ImplAttributes} vs {method2.ImplAttributes})");
            }

            // Compare method body instructions
            CompareMethodBodies(method1, method2, typeName, methodSig, result);
        }
    }

    private void CompareMethodBodies(MethodDefinition method1, MethodDefinition method2, string typeName, string methodSig, AssemblyComparisonResult result)
    {
        // Skip abstract methods or methods without bodies
        if (!method1.HasBody && !method2.HasBody)
        {
            return;
        }

        if (!method1.HasBody)
        {
            result.Differences.Add($"Type '{typeName}', Method '{methodSig}': Method1 has no body but Method2 does");
            return;
        }

        if (!method2.HasBody)
        {
            result.Differences.Add($"Type '{typeName}', Method '{methodSig}': Method2 has no body but Method1 does");
            return;
        }

        var body1 = method1.Body;
        var body2 = method2.Body;

        // Compare instruction count
        if (body1.Instructions.Count != body2.Instructions.Count)
        {
            result.Differences.Add($"Type '{typeName}', Method '{methodSig}': Instruction count differs ({body1.Instructions.Count} vs {body2.Instructions.Count})");
            result.MethodBodyDifferences.Add(new MethodBodyDifference
            {
                TypeName = typeName,
                MethodSignature = methodSig,
                Instructions1 = GetInstructionList(body1),
                Instructions2 = GetInstructionList(body2)
            });
            return;
        }

        // Compare each instruction
        bool bodyDiffers = false;
        var bodyDiff = new MethodBodyDifference
        {
            TypeName = typeName,
            MethodSignature = methodSig
        };

        for (int i = 0; i < body1.Instructions.Count; i++)
        {
            var inst1 = body1.Instructions[i];
            var inst2 = body2.Instructions[i];

            string inst1Str = GetInstructionString(inst1);
            string inst2Str = GetInstructionString(inst2);

            if (inst1Str != inst2Str)
            {
                if (!bodyDiffers)
                {
                    bodyDiffers = true;
                    result.Differences.Add($"Type '{typeName}', Method '{methodSig}': Method body differs");
                }

                bodyDiff.InstructionDifferences.Add($"  Instruction {i}: '{inst1Str}' vs '{inst2Str}'");
            }
        }

        if (bodyDiffers)
        {
            bodyDiff.Instructions1 = GetInstructionList(body1);
            bodyDiff.Instructions2 = GetInstructionList(body2);
            result.MethodBodyDifferences.Add(bodyDiff);
        }

        // Compare local variables
        if (body1.Variables.Count != body2.Variables.Count)
        {
            result.Differences.Add($"Type '{typeName}', Method '{methodSig}': Local variable count differs ({body1.Variables.Count} vs {body2.Variables.Count})");
        }
        else
        {
            for (int i = 0; i < body1.Variables.Count; i++)
            {
                if (body1.Variables[i].VariableType.FullName != body2.Variables[i].VariableType.FullName)
                {
                    result.Differences.Add($"Type '{typeName}', Method '{methodSig}': Local variable {i} type differs ('{body1.Variables[i].VariableType.FullName}' vs '{body2.Variables[i].VariableType.FullName}')");
                }
            }
        }

        // Compare exception handlers
        if (body1.ExceptionHandlers.Count != body2.ExceptionHandlers.Count)
        {
            result.Differences.Add($"Type '{typeName}', Method '{methodSig}': Exception handler count differs ({body1.ExceptionHandlers.Count} vs {body2.ExceptionHandlers.Count})");
        }
    }

    private string GetInstructionString(Mono.Cecil.Cil.Instruction instruction)
    {
        var sb = new StringBuilder();
        sb.Append(instruction.OpCode.ToString());

        if (instruction.Operand != null)
        {
            sb.Append(" ");

            // Format operand based on type
            if (instruction.Operand is Mono.Cecil.Cil.Instruction targetInst)
            {
                sb.Append($"IL_{targetInst.Offset:X4}");
            }
            else if (instruction.Operand is Mono.Cecil.Cil.Instruction[] targets)
            {
                sb.Append("[");
                sb.Append(string.Join(", ", targets.Select(t => $"IL_{t.Offset:X4}")));
                sb.Append("]");
            }
            else if (instruction.Operand is MethodReference methodRef)
            {
                sb.Append($"{methodRef.DeclaringType?.FullName}::{methodRef.Name}");
            }
            else if (instruction.Operand is FieldReference fieldRef)
            {
                sb.Append($"{fieldRef.DeclaringType?.FullName}::{fieldRef.Name}");
            }
            else if (instruction.Operand is TypeReference typeRef)
            {
                sb.Append(typeRef.FullName);
            }
            else if (instruction.Operand is string str)
            {
                sb.Append($"\"{str}\"");
            }
            else
            {
                sb.Append(instruction.Operand.ToString());
            }
        }

        return sb.ToString();
    }

    private List<string> GetInstructionList(Mono.Cecil.Cil.MethodBody body)
    {
        var instructions = new List<string>();
        foreach (var inst in body.Instructions)
        {
            instructions.Add($"IL_{inst.Offset:X4}: {GetInstructionString(inst)}");
        }
        return instructions;
    }

    private void CompareFields(TypeDefinition type1, TypeDefinition type2, string typeName, AssemblyComparisonResult result)
    {
        var fields1 = type1.Fields.Where(f => !f.Name.StartsWith("<")).ToDictionary(f => f.Name, f => f);
        var fields2 = type2.Fields.Where(f => !f.Name.StartsWith("<")).ToDictionary(f => f.Name, f => f);

        foreach (var fieldName in fields1.Keys.Except(fields2.Keys))
        {
            result.Differences.Add($"Type '{typeName}': Field only in Assembly1: {fieldName} ({fields1[fieldName].FieldType.FullName})");
        }

        foreach (var fieldName in fields2.Keys.Except(fields1.Keys))
        {
            result.Differences.Add($"Type '{typeName}': Field only in Assembly2: {fieldName} ({fields2[fieldName].FieldType.FullName})");
        }

        // Compare common fields
        foreach (var fieldName in fields1.Keys.Intersect(fields2.Keys))
        {
            var field1 = fields1[fieldName];
            var field2 = fields2[fieldName];

            if (field1.FieldType.FullName != field2.FieldType.FullName)
            {
                result.Differences.Add($"Type '{typeName}', Field '{fieldName}': Type differs ('{field1.FieldType.FullName}' vs '{field2.FieldType.FullName}')");
            }

            if (field1.IsPublic != field2.IsPublic)
            {
                result.Differences.Add($"Type '{typeName}', Field '{fieldName}': Visibility differs");
            }

            if (field1.IsStatic != field2.IsStatic)
            {
                result.Differences.Add($"Type '{typeName}', Field '{fieldName}': IsStatic differs");
            }
        }
    }

    private void CompareProperties(TypeDefinition type1, TypeDefinition type2, string typeName, AssemblyComparisonResult result)
    {
        var properties1 = type1.Properties.ToDictionary(p => p.FullName, p => p);
        var properties2 = type2.Properties.ToDictionary(p => p.FullName, p => p);

        foreach (var propName in properties1.Keys.Except(properties2.Keys))
        {
            result.Differences.Add($"Type '{typeName}': Property only in Assembly1: {propName}");
        }

        foreach (var propName in properties2.Keys.Except(properties1.Keys))
        {
            result.Differences.Add($"Type '{typeName}': Property only in Assembly2: {propName}");
        }

        // Compare common properties
        foreach (var propName in properties1.Keys.Intersect(properties2.Keys))
        {
            var prop1 = properties1[propName];
            var prop2 = properties2[propName];

            if (prop1.PropertyType.FullName != prop2.PropertyType.FullName)
            {
                result.Differences.Add($"Type '{typeName}', Property '{propName}': Type differs ('{prop1.PropertyType.FullName}' vs '{prop2.PropertyType.FullName}')");
            }

            if ((prop1.GetMethod != null) != (prop2.GetMethod != null))
            {
                result.Differences.Add($"Type '{typeName}', Property '{propName}': Getter presence differs");
            }

            if ((prop1.SetMethod != null) != (prop2.SetMethod != null))
            {
                result.Differences.Add($"Type '{typeName}', Property '{propName}': Setter presence differs");
            }
        }
    }

    private void CompareEvents(TypeDefinition type1, TypeDefinition type2, string typeName, AssemblyComparisonResult result)
    {
        var events1 = type1.Events.ToDictionary(e => e.FullName, e => e);
        var events2 = type2.Events.ToDictionary(e => e.FullName, e => e);

        foreach (var eventName in events1.Keys.Except(events2.Keys))
        {
            result.Differences.Add($"Type '{typeName}': Event only in Assembly1: {eventName}");
        }

        foreach (var eventName in events2.Keys.Except(events1.Keys))
        {
            result.Differences.Add($"Type '{typeName}': Event only in Assembly2: {eventName}");
        }

        // Compare common events
        foreach (var eventName in events1.Keys.Intersect(events2.Keys))
        {
            var event1 = events1[eventName];
            var event2 = events2[eventName];

            if (event1.EventType.FullName != event2.EventType.FullName)
            {
                result.Differences.Add($"Type '{typeName}', Event '{eventName}': Type differs ('{event1.EventType.FullName}' vs '{event2.EventType.FullName}')");
            }
        }
    }

    private IEnumerable<TypeDefinition> GetAllTypes(AssemblyDefinition assembly)
    {
        foreach (var module in assembly.Modules)
        {
            foreach (var type in module.Types)
            {
                yield return type;
                foreach (var nestedType in GetNestedTypes(type))
                {
                    yield return nestedType;
                }
            }
        }
    }

    private IEnumerable<TypeDefinition> GetNestedTypes(TypeDefinition type)
    {
        foreach (var nestedType in type.NestedTypes)
        {
            yield return nestedType;
            foreach (var innerNested in GetNestedTypes(nestedType))
            {
                yield return innerNested;
            }
        }
    }

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

public class AssemblyComparisonResult
{
    public string Assembly1Name { get; set; } = string.Empty;
    public string Assembly2Name { get; set; } = string.Empty;
    public List<string> Differences { get; set; } = new List<string>();
    public HashSet<string> TypesOnlyInAssembly1 { get; set; } = new HashSet<string>();
    public HashSet<string> TypesOnlyInAssembly2 { get; set; } = new HashSet<string>();
    public List<MethodBodyDifference> MethodBodyDifferences { get; set; } = new List<MethodBodyDifference>();

    public bool AreEqual => Differences.Count == 0;

    public string GenerateReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("===== Assembly Comparison Report =====");
        sb.AppendLine();
        sb.AppendLine($"Assembly 1: {Assembly1Name}");
        sb.AppendLine($"Assembly 2: {Assembly2Name}");
        sb.AppendLine();

        if (AreEqual)
        {
            sb.AppendLine("✓ Assemblies are identical");
        }
        else
        {
            sb.AppendLine($"✗ Found {Differences.Count} difference(s)");
            sb.AppendLine();

            if (TypesOnlyInAssembly1.Count > 0)
            {
                sb.AppendLine($"Types only in Assembly 1: {TypesOnlyInAssembly1.Count}");
                foreach (var type in TypesOnlyInAssembly1.Take(10))
                {
                    sb.AppendLine($"  - {type}");
                }
                if (TypesOnlyInAssembly1.Count > 10)
                {
                    sb.AppendLine($"  ... and {TypesOnlyInAssembly1.Count - 10} more");
                }
                sb.AppendLine();
            }

            if (TypesOnlyInAssembly2.Count > 0)
            {
                sb.AppendLine($"Types only in Assembly 2: {TypesOnlyInAssembly2.Count}");
                foreach (var type in TypesOnlyInAssembly2.Take(10))
                {
                    sb.AppendLine($"  - {type}");
                }
                if (TypesOnlyInAssembly2.Count > 10)
                {
                    sb.AppendLine($"  ... and {TypesOnlyInAssembly2.Count - 10} more");
                }
                sb.AppendLine();
            }

            sb.AppendLine("All Differences:");
            foreach (var diff in Differences)
            {
                sb.AppendLine($"  - {diff}");
            }

            if (MethodBodyDifferences.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Methods with body differences: {MethodBodyDifferences.Count}");
                sb.AppendLine("(Use MethodBodyDifferences property for detailed IL comparison)");
            }
        }

        return sb.ToString();
    }

    public override string ToString() => GenerateReport();
}

public class MethodBodyDifference
{
    public string TypeName { get; set; } = string.Empty;
    public string MethodSignature { get; set; } = string.Empty;
    public List<string> Instructions1 { get; set; } = new List<string>();
    public List<string> Instructions2 { get; set; } = new List<string>();
    public List<string> InstructionDifferences { get; set; } = new List<string>();

    public string GenerateDetailedReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Method: {TypeName}.{MethodSignature}");
        sb.AppendLine();

        if (InstructionDifferences.Count > 0)
        {
            sb.AppendLine("Specific Instruction Differences:");
            foreach (var diff in InstructionDifferences)
            {
                sb.AppendLine(diff);
            }
            sb.AppendLine();
        }

        sb.AppendLine("Assembly 1 Instructions:");
        foreach (var inst in Instructions1)
        {
            sb.AppendLine($"  {inst}");
        }
        sb.AppendLine();

        sb.AppendLine("Assembly 2 Instructions:");
        foreach (var inst in Instructions2)
        {
            sb.AppendLine($"  {inst}");
        }

        return sb.ToString();
    }
}

