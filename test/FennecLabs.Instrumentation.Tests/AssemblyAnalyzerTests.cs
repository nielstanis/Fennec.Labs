using System.Linq;
using FennecLabs.Instrumentation;
using FennecLabs.Instrumentation.Result;
using FennecLabs.TestUtilities;
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
            AssemblyResult assemblyResult = result.Analyse();
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
        public void Analyse_WithNonExistentFile_ReturnsHasError()
        {
            var result = new AssemblyAnalyzer("/nonexistent/path/Missing.dll");

            var assemblyResult = result.Analyse();

            Assert.True(assemblyResult.HasError);
            Assert.NotNull(assemblyResult.ExceptionOccurred);
            Assert.Equal("NotAvailable", assemblyResult.Assembly);
        }
    }
}

