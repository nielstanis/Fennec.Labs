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

    // ── Helpers for new sections ───────────────────────────────────────────────

    private static void AddMarkerAttribute(AssemblyDefinition assembly, string attrTypeName)
    {
        var attrType = new TypeDefinition("", attrTypeName,
            TypeAttributes.NotPublic | TypeAttributes.Class,
            assembly.MainModule.TypeSystem.Object);
        var ctor = new MethodDefinition(".ctor",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            assembly.MainModule.TypeSystem.Void);
        ctor.Body = new MethodBody(ctor);
        ctor.Body.GetILProcessor().Append(ctor.Body.GetILProcessor().Create(OpCodes.Ret));
        attrType.Methods.Add(ctor);
        assembly.MainModule.Types.Add(attrType);
        assembly.CustomAttributes.Add(new CustomAttribute(ctor));
    }

    private static TypeDefinition AddInterface(AssemblyDefinition assembly, string typeName)
    {
        var iface = new TypeDefinition(
            "TestNamespace",
            typeName,
            TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract,
            null);
        assembly.MainModule.Types.Add(iface);
        return iface;
    }

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
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                propType);
            getter.Body = new MethodBody(getter);
            getter.Body.GetILProcessor().Append(getter.Body.GetILProcessor().Create(OpCodes.Ret));
            type.Methods.Add(getter);
            property.GetMethod = getter;
        }
        if (hasSetter)
        {
            var setter = new MethodDefinition("set_" + name,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                type.Module.TypeSystem.Void);
            setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.None, propType));
            setter.Body = new MethodBody(setter);
            setter.Body.GetILProcessor().Append(setter.Body.GetILProcessor().Create(OpCodes.Ret));
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

    private static MethodDefinition AddAbstractVoidMethod(TypeDefinition type, string name)
    {
        var method = new MethodDefinition(
            name,
            MethodAttributes.Public | MethodAttributes.Abstract | MethodAttributes.Virtual,
            type.Module.TypeSystem.Void);
        type.Methods.Add(method);
        return method;
    }

    // ── Assembly name and version ──────────────────────────────────────────────

    [Fact]
    public void Compare_AssemblyNameDiffers_EmitsAssemblyNameDiff()
    {
        using var a1 = CreateAssembly("OldAssembly");
        using var a2 = CreateAssembly("NewAssembly");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<AssemblyNameDiff>(),
            e => e.Was == "OldAssembly" && e.Is == "NewAssembly");
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_AssemblyVersionDiffers_EmitsAssemblyVersionDiff()
    {
        var n1 = new AssemblyNameDefinition("TestAssembly", new Version(1, 0, 0, 0));
        using var a1 = AssemblyDefinition.CreateAssembly(n1, "TestAssembly.dll", ModuleKind.Dll);
        var n2 = new AssemblyNameDefinition("TestAssembly", new Version(2, 0, 0, 0));
        using var a2 = AssemblyDefinition.CreateAssembly(n2, "TestAssembly.dll", ModuleKind.Dll);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<AssemblyVersionDiff>(),
            e => e.Was == "1.0.0.0" && e.Is == "2.0.0.0");
        Assert.False(result.AreEqual);
    }

    // ── Assembly attribute presence ────────────────────────────────────────────

    [Fact]
    public void Compare_AssemblyAttributeRemovedFromAssembly2_EmitsAttributePresenceDiffRemoved()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddMarkerAttribute(a1, "MyRemovedAttr");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<AssemblyAttributePresenceDiff>(),
            e => e.AttributeType.Contains("MyRemovedAttr") && e.Kind == DiffKind.Removed);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_AssemblyAttributeOnlyInAssembly2_EmitsAttributePresenceDiffAdded()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddMarkerAttribute(a2, "MyAddedAttr");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<AssemblyAttributePresenceDiff>(),
            e => e.AttributeType.Contains("MyAddedAttr") && e.Kind == DiffKind.Added);
        Assert.False(result.AreEqual);
    }

    // ── Type base type and interfaces ──────────────────────────────────────────

    [Fact]
    public void Compare_TypeBaseTypeChanged_EmitsTypeBaseTypeDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "MyClass");

        var streamRef = a2.MainModule.ImportReference(typeof(System.IO.Stream));
        var type2 = new TypeDefinition(
            "TestNamespace", "MyClass",
            TypeAttributes.Public | TypeAttributes.Class,
            streamRef);
        a2.MainModule.Types.Add(type2);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<TypeBaseTypeDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Was == "System.Object"
              && e.Is == "System.IO.Stream");
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_InterfaceAddedToType_EmitsTypeInterfacePresenceDiffAdded()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        var iface = AddInterface(a2, "IMyService");
        type2.Interfaces.Add(new InterfaceImplementation(iface));

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<TypeInterfacePresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.InterfaceType == "TestNamespace.IMyService"
              && e.Kind == DiffKind.Added);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_InterfaceRemovedFromType_EmitsTypeInterfacePresenceDiffRemoved()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        var iface = AddInterface(a1, "IMyService");
        type1.Interfaces.Add(new InterfaceImplementation(iface));
        AddPublicClass(a2, "MyClass");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<TypeInterfacePresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.InterfaceType == "TestNamespace.IMyService"
              && e.Kind == DiffKind.Removed);
        Assert.False(result.AreEqual);
    }

    // ── Type flags (abstract / sealed) ────────────────────────────────────────

    [Fact]
    public void Compare_TypeAbstractChanged_EmitsTypeFlagDiffIsAbstract()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        type2.Attributes |= TypeAttributes.Abstract;

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<TypeFlagDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Flag == "IsAbstract"
              && e.Was == false && e.Is == true);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_TypeSealedChanged_EmitsTypeFlagDiffIsSealed()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        type2.Attributes |= TypeAttributes.Sealed;

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<TypeFlagDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Flag == "IsSealed"
              && e.Was == false && e.Is == true);
        Assert.False(result.AreEqual);
    }

    // ── Method flags ───────────────────────────────────────────────────────────

    [Fact]
    public void Compare_MethodVisibilityChanged_EmitsMethodFlagDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddVoidMethod(type1, "DoWork");

        var type2 = AddPublicClass(a2, "MyClass");
        var m2 = new MethodDefinition("DoWork", MethodAttributes.Assembly, a2.MainModule.TypeSystem.Void);
        m2.Body = new MethodBody(m2);
        m2.Body.GetILProcessor().Append(m2.Body.GetILProcessor().Create(OpCodes.Ret));
        type2.Methods.Add(m2);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodFlagDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Signature.Contains("DoWork")
              && e.Flag == "Visibility");
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodIsStaticChanged_EmitsMethodFlagDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddVoidMethod(type1, "DoWork");

        var type2 = AddPublicClass(a2, "MyClass");
        var m2 = new MethodDefinition(
            "DoWork",
            MethodAttributes.Public | MethodAttributes.Static,
            a2.MainModule.TypeSystem.Void);
        m2.Body = new MethodBody(m2);
        m2.Body.GetILProcessor().Append(m2.Body.GetILProcessor().Create(OpCodes.Ret));
        type2.Methods.Add(m2);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodFlagDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Signature.Contains("DoWork")
              && e.Flag == "IsStatic");
        Assert.False(result.AreEqual);
    }

    // ── Method body presence ───────────────────────────────────────────────────

    [Fact]
    public void Compare_MethodBodyPresentInAssembly2Only_EmitsMethodBodyPresenceDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        type1.Attributes |= TypeAttributes.Abstract;
        AddAbstractVoidMethod(type1, "DoWork");

        var type2 = AddPublicClass(a2, "MyClass");
        type2.Attributes |= TypeAttributes.Abstract;
        AddVoidMethod(type2, "DoWork");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodBodyPresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Signature.Contains("DoWork")
              && !e.PresentInAssembly1 && e.PresentInAssembly2);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodBodyPresentInAssembly1Only_EmitsMethodBodyPresenceDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        type1.Attributes |= TypeAttributes.Abstract;
        AddVoidMethod(type1, "DoWork");

        var type2 = AddPublicClass(a2, "MyClass");
        type2.Attributes |= TypeAttributes.Abstract;
        AddAbstractVoidMethod(type2, "DoWork");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodBodyPresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Signature.Contains("DoWork")
              && e.PresentInAssembly1 && !e.PresentInAssembly2);
        Assert.False(result.AreEqual);
    }

    // ── Method body locals and exception handlers ─────────────────────────────

    [Fact]
    public void Compare_MethodLocalCountDiffers_EmitsMethodBodyLocalsDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        var m1 = AddVoidMethod(type1, "Compute");
        m1.Body.Variables.Add(new VariableDefinition(a1.MainModule.TypeSystem.Int32));

        var type2 = AddPublicClass(a2, "MyClass");
        AddVoidMethod(type2, "Compute");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodBodyLocalsDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Signature.Contains("Compute")
              && e.Count1 == 1 && e.Count2 == 0);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodLocalVariableTypeDiffers_EmitsMethodBodyLocalVariableTypeDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        var m1 = AddVoidMethod(type1, "Transform");
        m1.Body.Variables.Add(new VariableDefinition(a1.MainModule.TypeSystem.Int32));

        var type2 = AddPublicClass(a2, "MyClass");
        var m2 = AddVoidMethod(type2, "Transform");
        m2.Body.Variables.Add(new VariableDefinition(a2.MainModule.TypeSystem.String));

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodBodyLocalVariableTypeDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Signature.Contains("Transform")
              && e.Index == 0
              && e.Was == "System.Int32" && e.Is == "System.String");
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodExceptionHandlerCountDiffers_EmitsMethodBodyExceptionHandlersDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddVoidMethod(type1, "TryMethod");

        var type2 = AddPublicClass(a2, "MyClass");
        var m2 = AddVoidMethod(type2, "TryMethod");
        m2.Body.ExceptionHandlers.Add(new Mono.Cecil.Cil.ExceptionHandler(
            Mono.Cecil.Cil.ExceptionHandlerType.Catch));

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<MethodBodyExceptionHandlersDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Signature.Contains("TryMethod")
              && e.Count1 == 0 && e.Count2 == 1);
        Assert.False(result.AreEqual);
    }

    // ── Fields ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Compare_FieldAddedInAssembly2_EmitsFieldPresenceDiffAdded()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        AddField(type2, "_count", a2.MainModule.TypeSystem.Int32);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<FieldPresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.FieldName == "_count"
              && e.Kind == DiffKind.Added);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_FieldRemovedFromAssembly2_EmitsFieldPresenceDiffRemoved()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddField(type1, "_count", a1.MainModule.TypeSystem.Int32);
        AddPublicClass(a2, "MyClass");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<FieldPresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.FieldName == "_count"
              && e.Kind == DiffKind.Removed);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_FieldTypeChanged_EmitsFieldTypeDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddField(type1, "_value", a1.MainModule.TypeSystem.Int32);

        var type2 = AddPublicClass(a2, "MyClass");
        AddField(type2, "_value", a2.MainModule.TypeSystem.String);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<FieldTypeDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.FieldName == "_value"
              && e.Was == "System.Int32" && e.Is == "System.String");
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_FieldVisibilityChanged_EmitsFieldFlagDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddField(type1, "_value", a1.MainModule.TypeSystem.Int32, FieldAttributes.Public);

        var type2 = AddPublicClass(a2, "MyClass");
        AddField(type2, "_value", a2.MainModule.TypeSystem.Int32, FieldAttributes.Assembly);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<FieldFlagDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.FieldName == "_value"
              && e.Flag == "Visibility");
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_FieldIsStaticChanged_EmitsFieldFlagDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddField(type1, "_value", a1.MainModule.TypeSystem.Int32, FieldAttributes.Public);

        var type2 = AddPublicClass(a2, "MyClass");
        AddField(type2, "_value", a2.MainModule.TypeSystem.Int32,
            FieldAttributes.Public | FieldAttributes.Static);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<FieldFlagDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.FieldName == "_value"
              && e.Flag == "IsStatic");
        Assert.False(result.AreEqual);
    }

    // ── Properties ─────────────────────────────────────────────────────────────

    [Fact]
    public void Compare_PropertyAddedInAssembly2_EmitsPropertyPresenceDiffAdded()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        AddProperty(type2, "Value", a2.MainModule.TypeSystem.Int32, hasGetter: true, hasSetter: false);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<PropertyPresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.PropertyName.Contains("Value")
              && e.Kind == DiffKind.Added);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_PropertyRemovedFromAssembly2_EmitsPropertyPresenceDiffRemoved()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddProperty(type1, "Value", a1.MainModule.TypeSystem.Int32, hasGetter: true, hasSetter: false);
        AddPublicClass(a2, "MyClass");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<PropertyPresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.PropertyName.Contains("Value")
              && e.Kind == DiffKind.Removed);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_PropertyGetterRemoved_EmitsPropertyAccessorDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddProperty(type1, "Value", a1.MainModule.TypeSystem.Int32, hasGetter: true, hasSetter: true);

        var type2 = AddPublicClass(a2, "MyClass");
        AddProperty(type2, "Value", a2.MainModule.TypeSystem.Int32, hasGetter: false, hasSetter: true);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<PropertyAccessorDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Accessor == "Getter"
              && e.Kind == DiffKind.Removed);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_PropertySetterAdded_EmitsPropertyAccessorDiff()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddProperty(type1, "Value", a1.MainModule.TypeSystem.Int32, hasGetter: true, hasSetter: false);

        var type2 = AddPublicClass(a2, "MyClass");
        AddProperty(type2, "Value", a2.MainModule.TypeSystem.Int32, hasGetter: true, hasSetter: true);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<PropertyAccessorDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.Accessor == "Setter"
              && e.Kind == DiffKind.Added);
        Assert.False(result.AreEqual);
    }

    // ── Events ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Compare_EventAddedInAssembly2_EmitsEventPresenceDiffAdded()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "MyClass");
        var type2 = AddPublicClass(a2, "MyClass");
        AddEvent(type2, "StateChanged", a2.MainModule.TypeSystem.Object);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<EventPresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.EventName.Contains("StateChanged")
              && e.Kind == DiffKind.Added);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_EventRemovedFromAssembly2_EmitsEventPresenceDiffRemoved()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        var type1 = AddPublicClass(a1, "MyClass");
        AddEvent(type1, "StateChanged", a1.MainModule.TypeSystem.Object);
        AddPublicClass(a2, "MyClass");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Events.OfType<EventPresenceDiff>(),
            e => e.TypeName == "TestNamespace.MyClass"
              && e.EventName.Contains("StateChanged")
              && e.Kind == DiffKind.Removed);
        Assert.False(result.AreEqual);
    }
}
