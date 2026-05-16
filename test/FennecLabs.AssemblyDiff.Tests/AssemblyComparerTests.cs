using FennecLabs.AssemblyDiff;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace FennecLabs.AssemblyDiff.Tests;

public class AssemblyComparerTests
{
    private static AssemblyDefinition CreateAssembly(string name = "TestAssembly")
    {
        var assemblyName = new AssemblyNameDefinition(name, new Version(1, 0, 0, 0));
        return AssemblyDefinition.CreateAssembly(
            assemblyName, name + ".dll", ModuleKind.Dll);
    }

    private static TypeDefinition AddPublicClass(AssemblyDefinition assembly, string typeName)
    {
        var type = new TypeDefinition(
            "TestNamespace",
            typeName,
            TypeAttributes.Public | TypeAttributes.Class,
            assembly.MainModule.TypeSystem.Object);
        assembly.MainModule.Types.Add(type);
        return type;
    }

    private static TypeDefinition AddInternalClass(AssemblyDefinition assembly, string typeName)
    {
        var type = new TypeDefinition(
            "TestNamespace",
            typeName,
            TypeAttributes.NotPublic | TypeAttributes.Class,
            assembly.MainModule.TypeSystem.Object);
        assembly.MainModule.Types.Add(type);
        return type;
    }

    private static MethodDefinition AddVoidMethod(TypeDefinition type, string methodName,
        Action<ILProcessor>? bodyBuilder = null)
    {
        var method = new MethodDefinition(
            methodName,
            MethodAttributes.Public,
            type.Module.TypeSystem.Void);
        method.Body = new MethodBody(method);
        var il = method.Body.GetILProcessor();
        if (bodyBuilder != null)
            bodyBuilder(il);
        else
            il.Append(il.Create(OpCodes.Ret));
        type.Methods.Add(method);
        return method;
    }

    private static TypeDefinition AddNestedPublicClass(TypeDefinition parent)
    {
        var nested = new TypeDefinition(
            "",
            "Inner",
            TypeAttributes.NestedPublic | TypeAttributes.Class,
            parent.Module.TypeSystem.Object);
        parent.NestedTypes.Add(nested);
        return nested;
    }

    // ── Type presence ──────────────────────────────────────────────────────────

    [Fact]
    public void Compare_TypeAddedInAssembly2_AppearsInTypesOnlyInAssembly2()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a2, "NewClass");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains("TestNamespace.NewClass", result.TypesOnlyInAssembly2);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_TypeAddedInAssembly2_EmitsTypePresenceDiffAdded()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a2, "AddedType");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<TypePresenceDiff>(),
            e => e.TypeName == "TestNamespace.AddedType" && e.Kind == DiffKind.Added);
    }

    [Fact]
    public void Compare_TypeRemovedFromAssembly2_AppearsInTypesOnlyInAssembly1()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "RemovedClass");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains("TestNamespace.RemovedClass", result.TypesOnlyInAssembly1);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_TypeRemovedFromAssembly2_EmitsTypePresenceDiffRemoved()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "RemovedType");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<TypePresenceDiff>(),
            e => e.TypeName == "TestNamespace.RemovedType" && e.Kind == DiffKind.Removed);
    }

    // ── Type flags ─────────────────────────────────────────────────────────────

    [Fact]
    public void Compare_TypeVisibilityChangedPublicToInternal_EmitsTypeFlagDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "MyClass");
        AddInternalClass(a2, "MyClass");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<TypeFlagDiff>(),
            e => e.TypeName == "TestNamespace.MyClass" && e.Flag == "IsPublic");
        Assert.False(result.AreEqual);
    }

    // ── Method body ────────────────────────────────────────────────────────────

    [Fact]
    public void Compare_MethodBodyDiffers_StringOperand_EmitsMethodBodyInstructionsDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddVoidMethod(type1, "Greet", il =>
        {
            il.Append(il.Create(OpCodes.Ldstr, "Hello"));
            il.Append(il.Create(OpCodes.Pop));
            il.Append(il.Create(OpCodes.Ret));
        });
        var type2 = AddPublicClass(a2, "MyClass");
        AddVoidMethod(type2, "Greet", il =>
        {
            il.Append(il.Create(OpCodes.Ldstr, "World"));
            il.Append(il.Create(OpCodes.Pop));
            il.Append(il.Create(OpCodes.Ret));
        });

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.NotEmpty(result.MethodBodyChanges);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodBodyDiffers_IntOperand_EmitsMethodBodyInstructionsDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddVoidMethod(type1, "Compute", il =>
        {
            il.Append(il.Create(OpCodes.Ldc_I4, 42));
            il.Append(il.Create(OpCodes.Pop));
            il.Append(il.Create(OpCodes.Ret));
        });
        var type2 = AddPublicClass(a2, "MyClass");
        AddVoidMethod(type2, "Compute", il =>
        {
            il.Append(il.Create(OpCodes.Ldc_I4, 99));
            il.Append(il.Create(OpCodes.Pop));
            il.Append(il.Create(OpCodes.Ret));
        });

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.NotEmpty(result.MethodBodyChanges);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodBodyInstructionsDiff_InstructionDiffsAreTyped()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddVoidMethod(type1, "Greet", il =>
        {
            il.Append(il.Create(OpCodes.Ldstr, "Hello"));
            il.Append(il.Create(OpCodes.Pop));
            il.Append(il.Create(OpCodes.Ret));
        });
        var type2 = AddPublicClass(a2, "MyClass");
        AddVoidMethod(type2, "Greet", il =>
        {
            il.Append(il.Create(OpCodes.Ldstr, "World"));
            il.Append(il.Create(OpCodes.Pop));
            il.Append(il.Create(OpCodes.Ret));
        });

        var result = new AssemblyComparer(a1, a2).Compare();

        var bodyDiff = Assert.Single(result.Events.OfType<MethodBodyInstructionsDiff>());
        Assert.Single(bodyDiff.Changes);
        Assert.Equal(0, bodyDiff.Changes[0].Index);
        Assert.Contains("Hello", bodyDiff.Changes[0].Instruction1);
        Assert.Contains("World", bodyDiff.Changes[0].Instruction2);
    }

    [Fact]
    public void Compare_MethodBodyWithMethodRefOperand_IsHandledWithoutException()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddVoidMethod(type1, "Run", il =>
        {
            il.Append(il.Create(OpCodes.Nop));
            il.Append(il.Create(OpCodes.Ret));
        });
        var type2 = AddPublicClass(a2, "MyClass");
        AddVoidMethod(type2, "Run", il =>
        {
            il.Append(il.Create(OpCodes.Nop));
            il.Append(il.Create(OpCodes.Ret));
        });

        var ex = Record.Exception(() => new AssemblyComparer(a1, a2).Compare());
        Assert.Null(ex);
    }

    // ── Report truncation ──────────────────────────────────────────────────────

    [Fact]
    public void GenerateReport_MoreThan10TypesOnlyInAssembly1_TruncatesToTen()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        for (int i = 0; i < 15; i++)
            AddPublicClass(a1, $"TypeOnly_{i:D2}");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Equal(15, result.TypesOnlyInAssembly1.Count());
        var report = result.GenerateReport();
        Assert.Contains("... and 5 more", report);
    }

    // ── Nested types ───────────────────────────────────────────────────────────

    [Fact]
    public void Compare_NestedTypeAddedInAssembly2_IsDetected()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "Outer");
        var outer2 = AddPublicClass(a2, "Outer");
        AddNestedPublicClass(outer2);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains("TestNamespace.Outer/Inner", result.TypesOnlyInAssembly2);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_NestedTypeRemovedFromAssembly2_IsDetected()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var outer1 = AddPublicClass(a1, "Outer");
        AddNestedPublicClass(outer1);
        AddPublicClass(a2, "Outer");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains("TestNamespace.Outer/Inner", result.TypesOnlyInAssembly1);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_NestedTypeOnlyInAssembly2_EmitsTypePresenceDiffWithDeclaringType()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "Outer");
        var outer2 = AddPublicClass(a2, "Outer");
        AddNestedPublicClass(outer2);

        var result = new AssemblyComparer(a1, a2).Compare();

        var evt = Assert.Single(result.Events.OfType<TypePresenceDiff>(),
            e => e.TypeName == "TestNamespace.Outer/Inner");
        Assert.Equal(DiffKind.Added, evt.Kind);
        Assert.Equal("TestNamespace.Outer", evt.DeclaringType);
    }

    [Fact]
    public void Compare_NestedTypeOnlyInAssembly1_EmitsTypePresenceDiffWithDeclaringType()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var outer1 = AddPublicClass(a1, "Outer");
        AddNestedPublicClass(outer1);
        AddPublicClass(a2, "Outer");

        var result = new AssemblyComparer(a1, a2).Compare();

        var evt = Assert.Single(result.Events.OfType<TypePresenceDiff>(),
            e => e.TypeName == "TestNamespace.Outer/Inner");
        Assert.Equal(DiffKind.Removed, evt.Kind);
        Assert.Equal("TestNamespace.Outer", evt.DeclaringType);
    }

    [Fact]
    public void Compare_NestedTypeModified_RecursesIntoNestedType()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var outer1 = AddPublicClass(a1, "Outer");
        var inner1 = AddNestedPublicClass(outer1);
        AddVoidMethod(inner1, "Work", il =>
        {
            il.Append(il.Create(OpCodes.Ldc_I4, 1));
            il.Append(il.Create(OpCodes.Pop));
            il.Append(il.Create(OpCodes.Ret));
        });

        var outer2 = AddPublicClass(a2, "Outer");
        var inner2 = AddNestedPublicClass(outer2);
        AddVoidMethod(inner2, "Work", il =>
        {
            il.Append(il.Create(OpCodes.Ldc_I4, 2));
            il.Append(il.Create(OpCodes.Pop));
            il.Append(il.Create(OpCodes.Ret));
        });

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodBodyInstructionsDiff>(),
            e => e.TypeName.EndsWith("/Inner") && e.Signature.Contains("Work"));
        Assert.False(result.AreEqual);
    }

    // ── Identity ───────────────────────────────────────────────────────────────

    [Fact]
    public void Compare_IdenticalEmptyAssemblies_AreEqual()
    {
        using var a1 = CreateAssembly("Same");
        using var a2 = CreateAssembly("Same");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.True(result.AreEqual);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void Compare_IdenticalAssembliesWithSharedType_AreEqual()
    {
        using var a1 = CreateAssembly("Same");
        using var a2 = CreateAssembly("Same");
        AddPublicClass(a1, "SharedClass");
        AddPublicClass(a2, "SharedClass");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.True(result.AreEqual);
    }

    // ── Assembly attributes ────────────────────────────────────────────────────

    private static void AddAssemblyStringAttribute(AssemblyDefinition assembly, string value)
    {
        var attrType = new TypeDefinition("", "TestStringAttr",
            TypeAttributes.NotPublic | TypeAttributes.Class,
            assembly.MainModule.TypeSystem.Object);
        var ctor = new MethodDefinition(".ctor",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            assembly.MainModule.TypeSystem.Void);
        ctor.Parameters.Add(new ParameterDefinition(assembly.MainModule.TypeSystem.String));
        var il = ctor.Body.GetILProcessor();
        il.Append(il.Create(OpCodes.Ret));
        attrType.Methods.Add(ctor);
        assembly.MainModule.Types.Add(attrType);

        var attr = new CustomAttribute(ctor);
        attr.ConstructorArguments.Add(
            new CustomAttributeArgument(assembly.MainModule.TypeSystem.String, value));
        assembly.CustomAttributes.Add(attr);
    }

    [Fact]
    public void Compare_AssemblyAttributeConstructorArgsDiffer_EmitsAssemblyAttributeArgsDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddAssemblyStringAttribute(a1, "1.0.0");
        AddAssemblyStringAttribute(a2, "2.0.0");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<AssemblyAttributeArgsDiff>(),
            e => e.AttributeType.Contains("TestStringAttr"));
        Assert.False(result.AreEqual);
    }

    // ── Method flags ───────────────────────────────────────────────────────────

    [Fact]
    public void Compare_MethodImplAttributesDiffer_EmitsMethodImplAttrsDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        var m1 = AddVoidMethod(type1, "Compute");
        m1.ImplAttributes = MethodImplAttributes.AggressiveInlining;

        var type2 = AddPublicClass(a2, "MyClass");
        var m2 = AddVoidMethod(type2, "Compute");
        m2.ImplAttributes = MethodImplAttributes.NoInlining;

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodImplAttrsDiff>(),
            e => e.TypeName == "TestNamespace.MyClass");
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodParameterAttributesDiffer_TreatedAsDistinctMethods()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        var method1 = new MethodDefinition("Process", MethodAttributes.Public, a1.MainModule.TypeSystem.Void);
        method1.Body = new MethodBody(method1);
        method1.Body.GetILProcessor().Append(method1.Body.GetILProcessor().Create(OpCodes.Ret));
        method1.Parameters.Add(new ParameterDefinition("data", ParameterAttributes.In, a1.MainModule.TypeSystem.Int32));
        type1.Methods.Add(method1);

        var type2 = AddPublicClass(a2, "MyClass");
        var method2 = new MethodDefinition("Process", MethodAttributes.Public, a2.MainModule.TypeSystem.Void);
        method2.Body = new MethodBody(method2);
        method2.Body.GetILProcessor().Append(method2.Body.GetILProcessor().Create(OpCodes.Ret));
        method2.Parameters.Add(new ParameterDefinition("data", ParameterAttributes.Out, a2.MainModule.TypeSystem.Int32));
        type2.Methods.Add(method2);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.False(result.AreEqual);
        Assert.Contains(result.Events.OfType<MethodPresenceDiff>(),
            e => e.Signature.Contains("Process"));
    }

    // ── P/Invoke ───────────────────────────────────────────────────────────────

    private static void AddPInvokeMethod(
        TypeDefinition type, string methodName, string dllName, string entryPoint)
    {
        var method = new MethodDefinition(
            methodName,
            MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.PInvokeImpl,
            type.Module.TypeSystem.Void);
        var moduleRef = new ModuleReference(dllName);
        type.Module.ModuleReferences.Add(moduleRef);
        method.PInvokeInfo = new PInvokeInfo(PInvokeAttributes.CallConvWinapi, entryPoint, moduleRef);
        type.Methods.Add(method);
    }

    [Fact]
    public void Compare_PInvokeInfoDiffers_EmitsMethodPInvokeInfoDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        AddPInvokeMethod(type1, "NativeMethod", "kernel32.dll", "FunctionA");
        AddPInvokeMethod(type2, "NativeMethod", "kernel32.dll", "FunctionB");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodPInvokeInfoDiff>(),
            e => e.TypeName == "TestNamespace.MyClass" && e.Signature.Contains("NativeMethod"));
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_PInvokeInfoUnchanged_NoMethodPInvokeInfoDiffEvent()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        AddPInvokeMethod(type1, "NativeMethod", "kernel32.dll", "FunctionA");
        AddPInvokeMethod(type2, "NativeMethod", "kernel32.dll", "FunctionA");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.DoesNotContain(result.Events, e => e is MethodPInvokeInfoDiff);
    }

    // ── Generic constraints ────────────────────────────────────────────────────

    private static void AddGenericMethodWithConstraint(
        TypeDefinition type, string methodName, TypeReference? constraintType)
    {
        var method = new MethodDefinition(methodName, MethodAttributes.Public, type.Module.TypeSystem.Void);
        method.Body = new MethodBody(method);
        method.Body.GetILProcessor().Append(method.Body.GetILProcessor().Create(OpCodes.Ret));
        var genericParam = new GenericParameter("T", method);
        if (constraintType != null)
            genericParam.Constraints.Add(new GenericParameterConstraint(constraintType));
        method.GenericParameters.Add(genericParam);
        type.Methods.Add(method);
    }

    [Fact]
    public void Compare_GenericConstraintsDiffer_TreatedAsDistinctMethods()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        AddGenericMethodWithConstraint(type1, "GenericMethod", a1.MainModule.TypeSystem.Object);
        AddGenericMethodWithConstraint(type2, "GenericMethod", constraintType: null);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodPresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass" && e.Signature.Contains("GenericMethod"));
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_GenericConstraintsUnchanged_NoSpuriousPresenceDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        AddGenericMethodWithConstraint(type1, "GenericMethod", a1.MainModule.TypeSystem.Object);
        AddGenericMethodWithConstraint(type2, "GenericMethod", a2.MainModule.TypeSystem.Object);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.DoesNotContain(result.Events.OfType<MethodPresenceDiff>(),
            e => e.Signature.Contains("GenericMethod"));
    }

    // ── Security declarations ──────────────────────────────────────────────────

    [Fact]
    public void Compare_TypeSecurityDeclarationAdded_EmitsTypeSecurityDeclarationDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        type2.SecurityDeclarations.Add(new SecurityDeclaration(SecurityAction.Demand));

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<TypeSecurityDeclarationDiff>(),
            e => e.TypeName == "TestNamespace.MyClass" && e.Kind == DiffKind.Added);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_TypeSecurityDeclarationRemoved_EmitsTypeSecurityDeclarationDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        type1.SecurityDeclarations.Add(new SecurityDeclaration(SecurityAction.Demand));
        AddPublicClass(a2, "MyClass");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<TypeSecurityDeclarationDiff>(),
            e => e.TypeName == "TestNamespace.MyClass" && e.Kind == DiffKind.Removed);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodSecurityDeclarationAdded_EmitsMethodSecurityDeclarationDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddVoidMethod(type1, "Secure");

        var type2 = AddPublicClass(a2, "MyClass");
        var m2 = AddVoidMethod(type2, "Secure");
        m2.SecurityDeclarations.Add(new SecurityDeclaration(SecurityAction.Demand));

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodSecurityDeclarationDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Signature.Contains("Secure")
              && e.Kind == DiffKind.Added);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodSecurityDeclarationRemoved_EmitsMethodSecurityDeclarationDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        var m1 = AddVoidMethod(type1, "Secure");
        m1.SecurityDeclarations.Add(new SecurityDeclaration(SecurityAction.Demand));

        var type2 = AddPublicClass(a2, "MyClass");
        AddVoidMethod(type2, "Secure");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodSecurityDeclarationDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Signature.Contains("Secure")
              && e.Kind == DiffKind.Removed);
        Assert.False(result.AreEqual);
    }
}
