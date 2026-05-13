using FennecLabs.AssemblyDiff;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace FennecLabs.AssemblyDiff.Tests;

/// <summary>
/// Tests for <see cref="AssemblyComparer"/> using minimal in-memory assemblies built
/// with Mono.Cecil — no .cs compilation or disk writes required.
/// </summary>
public class AssemblyComparerTests
{
    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private static AssemblyDefinition CreateAssembly(string name = "TestAssembly")
    {
        var assemblyName = new AssemblyNameDefinition(name, new Version(1, 0, 0, 0));
        return AssemblyDefinition.CreateAssembly(
            assemblyName, name + ".dll", ModuleKind.Dll);
    }

    /// <summary>Adds a public class with no members to the assembly's main module.</summary>
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

    /// <summary>Adds an internal (non-public) class to the assembly's main module.</summary>
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

    /// <summary>Adds a void method with a simple IL body to the given type.</summary>
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

    // ---------------------------------------------------------------------------
    // Added type tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Compare_TypeAddedInAssembly2_AppearsInTypesOnlyInAssembly2()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a2, "NewClass");

        var comparer = new AssemblyComparer(a1, a2);
        var result = comparer.Compare();

        Assert.Contains("TestNamespace.NewClass", result.TypesOnlyInAssembly2);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_TypeAddedInAssembly2_DifferencesContainsEntry()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a2, "AddedType");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Differences, d => d.Contains("AddedType") && d.Contains("Assembly2"));
    }

    // ---------------------------------------------------------------------------
    // Removed type tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Compare_TypeRemovedFromAssembly2_AppearsInTypesOnlyInAssembly1()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "RemovedClass");

        var comparer = new AssemblyComparer(a1, a2);
        var result = comparer.Compare();

        Assert.Contains("TestNamespace.RemovedClass", result.TypesOnlyInAssembly1);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_TypeRemovedFromAssembly2_DifferencesContainsEntry()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "RemovedType");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Differences, d => d.Contains("RemovedType") && d.Contains("Assembly1"));
    }

    // ---------------------------------------------------------------------------
    // Visibility change tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Compare_TypeVisibilityChangedPublicToInternal_CapturedInDifferences()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();
        AddPublicClass(a1, "MyClass");
        AddInternalClass(a2, "MyClass");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains(result.Differences,
            d => d.Contains("MyClass") && d.Contains("Visibility") || d.Contains("IsPublic"));
        Assert.False(result.AreEqual);
    }

    // ---------------------------------------------------------------------------
    // Method body / IL operand type tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Compare_MethodBodyDiffers_StringOperand_CapturedInDifferences()
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

        Assert.NotEmpty(result.MethodBodyDifferences);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodBodyDiffers_IntOperand_CapturedInDifferences()
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

        Assert.NotEmpty(result.MethodBodyDifferences);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_MethodBodyWithMethodRefOperand_IsHandledWithoutException()
    {
        // Verifies that MethodReference operands are serialized without throwing.
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();

        var type1 = AddPublicClass(a1, "MyClass");
        AddVoidMethod(type1, "Run", il =>
        {
            // ldc_i4_0 / nop pair — simple identical bodies
            il.Append(il.Create(OpCodes.Nop));
            il.Append(il.Create(OpCodes.Ret));
        });

        var type2 = AddPublicClass(a2, "MyClass");
        AddVoidMethod(type2, "Run", il =>
        {
            il.Append(il.Create(OpCodes.Nop));
            il.Append(il.Create(OpCodes.Ret));
        });

        // Identical bodies — should not throw
        var ex = Record.Exception(() => new AssemblyComparer(a1, a2).Compare());
        Assert.Null(ex);
    }

    // ---------------------------------------------------------------------------
    // Truncation test
    // ---------------------------------------------------------------------------

    [Fact]
    public void GenerateReport_MoreThan10TypesOnlyInAssembly1_TruncatesToTen()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();

        for (int i = 0; i < 15; i++)
            AddPublicClass(a1, $"TypeOnly_{i:D2}");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Equal(15, result.TypesOnlyInAssembly1.Count);

        var report = result.GenerateReport();

        // Report should mention truncation
        Assert.Contains("... and 5 more", report);
    }

    // ---------------------------------------------------------------------------
    // Nested type tests
    // ---------------------------------------------------------------------------

    [Fact]
    public void Compare_NestedTypeAddedInAssembly2_IsDetected()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();

        // a1 has outer class only
        AddPublicClass(a1, "Outer");

        // a2 has outer class + nested class
        var outer2 = AddPublicClass(a2, "Outer");
        var nested = new TypeDefinition(
            "",
            "Inner",
            TypeAttributes.NestedPublic | TypeAttributes.Class,
            a2.MainModule.TypeSystem.Object);
        outer2.NestedTypes.Add(nested);

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains("TestNamespace.Outer/Inner", result.TypesOnlyInAssembly2);
        Assert.False(result.AreEqual);
    }

    [Fact]
    public void Compare_NestedTypeRemovedFromAssembly2_IsDetected()
    {
        using var a1 = CreateAssembly();
        using var a2 = CreateAssembly();

        // a1 has outer + nested
        var outer1 = AddPublicClass(a1, "Outer");
        var nested1 = new TypeDefinition(
            "",
            "Inner",
            TypeAttributes.NestedPublic | TypeAttributes.Class,
            a1.MainModule.TypeSystem.Object);
        outer1.NestedTypes.Add(nested1);

        // a2 has outer only
        AddPublicClass(a2, "Outer");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.Contains("TestNamespace.Outer/Inner", result.TypesOnlyInAssembly1);
        Assert.False(result.AreEqual);
    }

    // ---------------------------------------------------------------------------
    // Identical assemblies
    // ---------------------------------------------------------------------------

    [Fact]
    public void Compare_IdenticalEmptyAssemblies_AreEqual()
    {
        using var a1 = CreateAssembly("Same");
        using var a2 = CreateAssembly("Same");

        var result = new AssemblyComparer(a1, a2).Compare();

        Assert.True(result.AreEqual);
        Assert.Empty(result.Differences);
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
}
