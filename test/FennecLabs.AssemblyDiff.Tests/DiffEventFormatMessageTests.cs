using FennecLabs.AssemblyDiff;

namespace FennecLabs.AssemblyDiff.Tests;

public class DiffEventFormatMessageTests
{
    // ── Assembly-level ─────────────────────────────────────────────────────────

    [Fact]
    public void AssemblyNameDiff_FormatMessage_ContainsBothNames()
    {
        var diff = new AssemblyNameDiff("OldAssembly", "NewAssembly");
        Assert.Equal("Assembly name differs: 'OldAssembly' vs 'NewAssembly'", diff.FormatMessage());
    }

    [Fact]
    public void AssemblyVersionDiff_FormatMessage_ContainsBothVersions()
    {
        var diff = new AssemblyVersionDiff("1.0.0.0", "2.0.0.0");
        Assert.Equal("Assembly version differs: '1.0.0.0' vs '2.0.0.0'", diff.FormatMessage());
    }

    [Fact]
    public void AssemblyAttributePresenceDiff_Removed_ReferencesAssembly1()
    {
        var diff = new AssemblyAttributePresenceDiff("MyNamespace.MyAttribute", DiffKind.Removed);
        Assert.Equal("Custom attribute only in Assembly1: MyNamespace.MyAttribute", diff.FormatMessage());
    }

    [Fact]
    public void AssemblyAttributePresenceDiff_Added_ReferencesAssembly2()
    {
        var diff = new AssemblyAttributePresenceDiff("MyNamespace.MyAttribute", DiffKind.Added);
        Assert.Equal("Custom attribute only in Assembly2: MyNamespace.MyAttribute", diff.FormatMessage());
    }

    // ── Type-level ─────────────────────────────────────────────────────────────

    [Fact]
    public void TypeBaseTypeDiff_FormatMessage_ContainsBothBaseTypeNames()
    {
        var diff = new TypeBaseTypeDiff("MyNamespace.MyClass", "System.Object", "System.IO.Stream");
        Assert.Equal(
            "Type 'MyNamespace.MyClass': BaseType differs ('System.Object' vs 'System.IO.Stream')",
            diff.FormatMessage());
    }

    [Fact]
    public void TypeInterfacePresenceDiff_Removed_ReferencesAssembly1()
    {
        var diff = new TypeInterfacePresenceDiff(
            "MyNamespace.MyClass", "MyNamespace.IMyService", DiffKind.Removed);
        Assert.Equal(
            "Type 'MyNamespace.MyClass': Interface only in Assembly1: MyNamespace.IMyService",
            diff.FormatMessage());
    }

    [Fact]
    public void TypeInterfacePresenceDiff_Added_ReferencesAssembly2()
    {
        var diff = new TypeInterfacePresenceDiff(
            "MyNamespace.MyClass", "MyNamespace.IMyService", DiffKind.Added);
        Assert.Equal(
            "Type 'MyNamespace.MyClass': Interface only in Assembly2: MyNamespace.IMyService",
            diff.FormatMessage());
    }

    [Fact]
    public void TypeFlagDiff_IsAbstract_UsesDefaultBranch()
    {
        var diff = new TypeFlagDiff("MyNamespace.MyClass", "IsAbstract", false, true);
        Assert.Equal("Type 'MyNamespace.MyClass': IsAbstract differs (False vs True)", diff.FormatMessage());
    }

    [Fact]
    public void TypeFlagDiff_IsSealed_UsesDefaultBranch()
    {
        var diff = new TypeFlagDiff("MyNamespace.MyClass", "IsSealed", false, true);
        Assert.Equal("Type 'MyNamespace.MyClass': IsSealed differs (False vs True)", diff.FormatMessage());
    }

    [Fact]
    public void TypeFlagDiff_IsInterface_UsesDefaultBranch()
    {
        var diff = new TypeFlagDiff("MyNamespace.MyClass", "IsInterface", false, true);
        Assert.Equal("Type 'MyNamespace.MyClass': IsInterface differs (False vs True)", diff.FormatMessage());
    }

    // ── Method-level ───────────────────────────────────────────────────────────

    [Fact]
    public void MethodFlagDiff_FormatMessage_IncludesTypeMethodAndFlag()
    {
        var diff = new MethodFlagDiff("MyNamespace.MyClass", "System.Void MyMethod()", "Visibility");
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Method 'System.Void MyMethod()': Visibility differs",
            diff.FormatMessage());
    }

    [Fact]
    public void MethodBodyPresenceDiff_Method1HasNoBody_CorrectMessage()
    {
        var diff = new MethodBodyPresenceDiff("MyNamespace.MyClass", "System.Void MyMethod()", false, true);
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Method 'System.Void MyMethod()': Method1 has no body but Method2 does",
            diff.FormatMessage());
    }

    [Fact]
    public void MethodBodyPresenceDiff_Method2HasNoBody_CorrectMessage()
    {
        var diff = new MethodBodyPresenceDiff("MyNamespace.MyClass", "System.Void MyMethod()", true, false);
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Method 'System.Void MyMethod()': Method2 has no body but Method1 does",
            diff.FormatMessage());
    }

    [Fact]
    public void MethodBodyLocalsDiff_FormatMessage_IncludesBothCounts()
    {
        var diff = new MethodBodyLocalsDiff("MyNamespace.MyClass", "System.Void MyMethod()", 1, 2);
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Method 'System.Void MyMethod()': Local variable count differs (1 vs 2)",
            diff.FormatMessage());
    }

    [Fact]
    public void MethodBodyLocalVariableTypeDiff_FormatMessage_IncludesIndexAndBothTypes()
    {
        var diff = new MethodBodyLocalVariableTypeDiff(
            "MyNamespace.MyClass", "System.Void MyMethod()", 0, "System.Int32", "System.String");
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Method 'System.Void MyMethod()': " +
            "Local variable 0 type differs ('System.Int32' vs 'System.String')",
            diff.FormatMessage());
    }

    [Fact]
    public void MethodBodyExceptionHandlersDiff_FormatMessage_IncludesBothCounts()
    {
        var diff = new MethodBodyExceptionHandlersDiff("MyNamespace.MyClass", "System.Void MyMethod()", 0, 1);
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Method 'System.Void MyMethod()': Exception handler count differs (0 vs 1)",
            diff.FormatMessage());
    }

    // ── Field-level ────────────────────────────────────────────────────────────

    [Fact]
    public void FieldPresenceDiff_Removed_ReferencesAssembly1WithType()
    {
        var diff = new FieldPresenceDiff("MyNamespace.MyClass", "_count", "System.Int32", DiffKind.Removed);
        Assert.Equal(
            "Type 'MyNamespace.MyClass': Field only in Assembly1: _count (System.Int32)",
            diff.FormatMessage());
    }

    [Fact]
    public void FieldPresenceDiff_Added_ReferencesAssembly2WithType()
    {
        var diff = new FieldPresenceDiff("MyNamespace.MyClass", "_count", "System.Int32", DiffKind.Added);
        Assert.Equal(
            "Type 'MyNamespace.MyClass': Field only in Assembly2: _count (System.Int32)",
            diff.FormatMessage());
    }

    [Fact]
    public void FieldTypeDiff_FormatMessage_IncludesBothTypes()
    {
        var diff = new FieldTypeDiff("MyNamespace.MyClass", "_value", "System.Int32", "System.String");
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Field '_value': Type differs ('System.Int32' vs 'System.String')",
            diff.FormatMessage());
    }

    [Fact]
    public void FieldFlagDiff_FormatMessage_IncludesFieldAndFlag()
    {
        var diff = new FieldFlagDiff("MyNamespace.MyClass", "_value", "Visibility");
        Assert.Equal("Type 'MyNamespace.MyClass', Field '_value': Visibility differs", diff.FormatMessage());
    }

    // ── Property-level ─────────────────────────────────────────────────────────

    [Fact]
    public void PropertyPresenceDiff_Removed_ReferencesAssembly1()
    {
        var diff = new PropertyPresenceDiff(
            "MyNamespace.MyClass", "System.Int32 MyNamespace.MyClass::Value", DiffKind.Removed);
        Assert.Equal(
            "Type 'MyNamespace.MyClass': Property only in Assembly1: System.Int32 MyNamespace.MyClass::Value",
            diff.FormatMessage());
    }

    [Fact]
    public void PropertyPresenceDiff_Added_ReferencesAssembly2()
    {
        var diff = new PropertyPresenceDiff(
            "MyNamespace.MyClass", "System.Int32 MyNamespace.MyClass::Value", DiffKind.Added);
        Assert.Equal(
            "Type 'MyNamespace.MyClass': Property only in Assembly2: System.Int32 MyNamespace.MyClass::Value",
            diff.FormatMessage());
    }

    [Fact]
    public void PropertyTypeDiff_FormatMessage_IncludesBothTypes()
    {
        var diff = new PropertyTypeDiff(
            "MyNamespace.MyClass", "System.Int32 MyNamespace.MyClass::Value",
            "System.Int32", "System.String");
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Property 'System.Int32 MyNamespace.MyClass::Value': " +
            "Type differs ('System.Int32' vs 'System.String')",
            diff.FormatMessage());
    }

    [Fact]
    public void PropertyAccessorDiff_GetterRemoved_CorrectMessage()
    {
        var diff = new PropertyAccessorDiff(
            "MyNamespace.MyClass", "System.Int32 MyNamespace.MyClass::Value", "Getter", DiffKind.Removed);
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Property 'System.Int32 MyNamespace.MyClass::Value': Getter presence differs",
            diff.FormatMessage());
    }

    [Fact]
    public void PropertyAccessorDiff_SetterAdded_CorrectMessage()
    {
        var diff = new PropertyAccessorDiff(
            "MyNamespace.MyClass", "System.Int32 MyNamespace.MyClass::Value", "Setter", DiffKind.Added);
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Property 'System.Int32 MyNamespace.MyClass::Value': Setter presence differs",
            diff.FormatMessage());
    }

    // ── Event-level ────────────────────────────────────────────────────────────

    [Fact]
    public void EventPresenceDiff_Removed_ReferencesAssembly1()
    {
        var diff = new EventPresenceDiff(
            "MyNamespace.MyClass", "System.EventHandler MyNamespace.MyClass::StateChanged", DiffKind.Removed);
        Assert.Equal(
            "Type 'MyNamespace.MyClass': Event only in Assembly1: System.EventHandler MyNamespace.MyClass::StateChanged",
            diff.FormatMessage());
    }

    [Fact]
    public void EventPresenceDiff_Added_ReferencesAssembly2()
    {
        var diff = new EventPresenceDiff(
            "MyNamespace.MyClass", "System.EventHandler MyNamespace.MyClass::StateChanged", DiffKind.Added);
        Assert.Equal(
            "Type 'MyNamespace.MyClass': Event only in Assembly2: System.EventHandler MyNamespace.MyClass::StateChanged",
            diff.FormatMessage());
    }

    [Fact]
    public void EventTypeDiff_FormatMessage_IncludesBothTypes()
    {
        var diff = new EventTypeDiff(
            "MyNamespace.MyClass", "System.EventHandler MyNamespace.MyClass::StateChanged",
            "System.EventHandler", "System.EventHandler`1<System.EventArgs>");
        Assert.Equal(
            "Type 'MyNamespace.MyClass', Event 'System.EventHandler MyNamespace.MyClass::StateChanged': " +
            "Type differs ('System.EventHandler' vs 'System.EventHandler`1<System.EventArgs>')",
            diff.FormatMessage());
    }
}
