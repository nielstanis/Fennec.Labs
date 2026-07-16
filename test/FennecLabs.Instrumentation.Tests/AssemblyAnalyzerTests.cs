using System.Linq;
using FennecLabs.Instrumentation;
using FennecLabs.Instrumentation.Result;
using FennecLabs.TestUtilities;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Xunit;

namespace FennecLabs.Instrumentation.Tests
{
    public class AssemblyAnalyzerTests
    {
        [Fact]
        public void BasicConsoleResultTest()
        {
            var x = TestResources.GetTestProjectAssembly("BasicConsole");
            var result = new AssemblyAnalyzer(x);
            AssemblyResult assemblyResult = result.Analyze();
            Assert.Equal(2, assemblyResult.Types.Count);
            Assert.Equal("<Module>", assemblyResult.Types[0].ClassType);
            Assert.Equal("BasicConsole.Program", assemblyResult.Types[1].ClassType);

            var ct = assemblyResult.Types[1];
            Assert.Equal(3, ct.Methods.Count());

            var mt = ct.Methods[2];
            Assert.Equal("Main", mt.Name);
            Assert.Equal("args", mt.Parameters);
            Assert.Equal(7, mt.Invocations.Count);

            Assert.Contains(mt.Invocations, i => i.Invocation.Contains("BinaryFormatter") && i.Invocation.Contains("Deserialize"));
            Assert.Contains(mt.Invocations, i => i.Invocation.Contains("File") && i.Invocation.Contains("Exists"));
        }

        [Fact]
        public void Analyze_WithNonExistentFile_ReturnsHasError()
        {
            var result = new AssemblyAnalyzer("/nonexistent/path/Missing.dll");

            var assemblyResult = result.Analyze();

            Assert.True(assemblyResult.HasError);
            Assert.NotNull(assemblyResult.ExceptionOccurred);
            Assert.Equal("NotAvailable", assemblyResult.Assembly);
        }

        [Fact]
        public void Analyze_WithEmptyAssembly_ReturnsOnlyModuleType()
        {
            var tempPath = CreateTempAssembly(_ => { });
            try
            {
                var result = new AssemblyAnalyzer(tempPath).Analyze();

                Assert.False(result.HasError);
                Assert.All(result.Types, t => Assert.Equal("<Module>", t.ClassType));
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        [Fact]
        public void Analyze_TypeWithNoCallInstructions_ReturnsMethodWithEmptyInvocations()
        {
            var tempPath = CreateTempAssembly(assembly =>
            {
                var type = new TypeDefinition("", "EmptyClass",
                    TypeAttributes.Public | TypeAttributes.Class,
                    assembly.MainModule.TypeSystem.Object);
                var method = new MethodDefinition("DoNothing",
                    MethodAttributes.Public, assembly.MainModule.TypeSystem.Void);
                method.Body = new MethodBody(method);
                method.Body.GetILProcessor().Append(
                    method.Body.GetILProcessor().Create(OpCodes.Ret));
                type.Methods.Add(method);
                assembly.MainModule.Types.Add(type);
            });
            try
            {
                var result = new AssemblyAnalyzer(tempPath).Analyze();

                Assert.False(result.HasError);
                var userType = result.Types.First(t => t.ClassType == "EmptyClass");
                var method = userType.Methods.First(m => m.Name == "DoNothing");
                Assert.Empty(method.Invocations);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        [Fact]
        public void Analyze_WithDeeplyNestedNamespace_ReturnsTypeWithFullName()
        {
            var tempPath = CreateTempAssembly(assembly =>
            {
                var type = new TypeDefinition("A.B.C.D.E", "MyClass",
                    TypeAttributes.Public | TypeAttributes.Class,
                    assembly.MainModule.TypeSystem.Object);
                assembly.MainModule.Types.Add(type);
            });
            try
            {
                var result = new AssemblyAnalyzer(tempPath).Analyze();

                Assert.False(result.HasError);
                Assert.Contains(result.Types, t => t.ClassType == "A.B.C.D.E.MyClass");
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        [Fact]
        public void Analyze_WithDelegateConstruction_CapturesLdftnTarget()
        {
            var tempPath = CreateTempAssembly(assembly =>
            {
                var module = assembly.MainModule;
                var type = new TypeDefinition("", "DelegateHost",
                    TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);

                var target = new MethodDefinition("OnClick",
                    MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
                target.Body = new MethodBody(target);
                target.Body.GetILProcessor().Append(target.Body.GetILProcessor().Create(OpCodes.Ret));
                type.Methods.Add(target);

                var actionCtor = module.ImportReference(typeof(Action).GetConstructor(new[] { typeof(object), typeof(IntPtr) }));

                var caller = new MethodDefinition("MakeDelegate",
                    MethodAttributes.Public | MethodAttributes.Static, module.ImportReference(typeof(Action)));
                caller.Body = new MethodBody(caller);
                var il = caller.Body.GetILProcessor();
                il.Append(il.Create(OpCodes.Ldnull));
                il.Append(il.Create(OpCodes.Ldftn, target));
                il.Append(il.Create(OpCodes.Newobj, actionCtor));
                il.Append(il.Create(OpCodes.Ret));
                type.Methods.Add(caller);

                module.Types.Add(type);
            });
            try
            {
                var result = new AssemblyAnalyzer(tempPath).Analyze();

                Assert.False(result.HasError);
                var host = result.Types.First(t => t.ClassType == "DelegateHost");
                var method = host.Methods.First(m => m.Name == "MakeDelegate");
                Assert.Contains(method.Invocations, i => i.Invocation.Contains("OnClick"));
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        [Fact]
        public void Analyze_WithJmpInstruction_CapturesTarget()
        {
            var tempPath = CreateTempAssembly(assembly =>
            {
                var module = assembly.MainModule;
                var type = new TypeDefinition("", "JmpHost",
                    TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);

                var target = new MethodDefinition("Target",
                    MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
                target.Body = new MethodBody(target);
                target.Body.GetILProcessor().Append(target.Body.GetILProcessor().Create(OpCodes.Ret));
                type.Methods.Add(target);

                var caller = new MethodDefinition("Caller",
                    MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
                caller.Body = new MethodBody(caller);
                var il = caller.Body.GetILProcessor();
                il.Append(il.Create(OpCodes.Jmp, target));
                type.Methods.Add(caller);

                module.Types.Add(type);
            });
            try
            {
                var result = new AssemblyAnalyzer(tempPath).Analyze();

                Assert.False(result.HasError);
                var host = result.Types.First(t => t.ClassType == "JmpHost");
                var method = host.Methods.First(m => m.Name == "Caller");
                Assert.Contains(method.Invocations, i => i.Invocation.Contains("Target"));
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        private static string CreateTempAssembly(Action<AssemblyDefinition> configure)
        {
            var name = new AssemblyNameDefinition("TestAssembly", new Version(1, 0, 0, 0));
            using var assembly = AssemblyDefinition.CreateAssembly(name, "TestAssembly.dll", ModuleKind.Dll);
            configure(assembly);
            var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".dll");
            assembly.Write(tempPath);
            return tempPath;
        }
    }
}
