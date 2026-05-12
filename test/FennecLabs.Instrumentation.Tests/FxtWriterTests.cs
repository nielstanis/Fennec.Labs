using System.IO;
using System.Threading.Tasks;
using FennecLabs.Instrumentation.Output;
using FennecLabs.Instrumentation.Result;

namespace FennecLabs.Instrumentation.Tests
{
    public class FxtWriterTests : IDisposable
    {
        private readonly string _tempDir;

        public FxtWriterTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose() => Directory.Delete(_tempDir, recursive: true);

        private static AssemblyResult MakeResult(string filePath = "Test.dll")
        {
            var result = new AssemblyResult("Test.Assembly, Version=1.0.0.0", filePath);
            var type = new ClassTypeResult("MyNamespace.MyClass", filePath);
            var method = new MethodResult("MyMethod", "x");
            method.Invocations.Add(new InvocationResult("System.Console::WriteLine", "System.Void", 0));
            type.Methods.Add(method);
            result.Types.Add(type);
            return result;
        }

        [Fact]
        public async Task WriteOutputAsync_WritesFileToOutputFolder()
        {
            var writer = new FxtWriter(_tempDir);

            await writer.WriteOutputAsync(MakeResult());

            Assert.True(File.Exists(Path.Combine(_tempDir, "Test.fxt")));
        }

        [Fact]
        public async Task WriteOutputAsync_WithRelativePath_PreservesDirectoryStructure()
        {
            var writer = new FxtWriter(_tempDir);

            await writer.WriteOutputAsync(MakeResult(), "lib/net6.0/Test.dll");

            Assert.True(File.Exists(Path.Combine(_tempDir, "lib", "net6.0", "Test.fxt")));
        }

        [Fact]
        public async Task WriteOutputAsync_ContentMatchesExpectedFormat()
        {
            var writer = new FxtWriter(_tempDir);

            await writer.WriteOutputAsync(MakeResult());

            var lines = await File.ReadAllLinesAsync(Path.Combine(_tempDir, "Test.fxt"));
            Assert.Single(lines);
            Assert.Equal("MyNamespace.MyClass::MyMethod(x)::System.Console::WriteLine", lines[0]);
        }

        [Fact]
        public async Task WriteOutputAsync_CreatesOutputFolderIfNotExists()
        {
            var nestedDir = Path.Combine(_tempDir, "a", "b", "c");
            var writer = new FxtWriter(nestedDir);

            await writer.WriteOutputAsync(MakeResult());

            Assert.True(File.Exists(Path.Combine(nestedDir, "Test.fxt")));
        }

        [Fact]
        public async Task WriteOutputAsync_MultipleInvocations_WritesOneLine_PerInvocation()
        {
            var result = new AssemblyResult("Test.Assembly", "Multi.dll");
            var type = new ClassTypeResult("A.B", "Multi.dll");
            var method = new MethodResult("Go", "");
            method.Invocations.Add(new InvocationResult("X::Foo", "void", 0));
            method.Invocations.Add(new InvocationResult("Y::Bar", "void", 1));
            type.Methods.Add(method);
            result.Types.Add(type);

            var writer = new FxtWriter(_tempDir);
            await writer.WriteOutputAsync(result);

            var lines = await File.ReadAllLinesAsync(Path.Combine(_tempDir, "Multi.fxt"));
            Assert.Equal(2, lines.Length);
            Assert.Equal("A.B::Go()::X::Foo", lines[0]);
            Assert.Equal("A.B::Go()::Y::Bar", lines[1]);
        }
    }
}
