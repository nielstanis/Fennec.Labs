using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FennecLabs.Instrumentation.Output;
using FennecLabs.Instrumentation.Result;

namespace FennecLabs.Instrumentation.Tests
{
    public class JsonWriterTests : IDisposable
    {
        private readonly string _tempDir;

        public JsonWriterTests()
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
        public async Task WriteOutputAsync_WritesJsonFileToOutputFolder()
        {
            var writer = new JsonWriter(_tempDir);

            await writer.WriteOutputAsync(MakeResult());

            Assert.True(File.Exists(Path.Combine(_tempDir, "Test.json")));
        }

        [Fact]
        public async Task WriteOutputAsync_WithRelativePath_PreservesDirectoryStructure()
        {
            var writer = new JsonWriter(_tempDir);

            await writer.WriteOutputAsync(MakeResult(), "lib/net8.0/Test.dll");

            Assert.True(File.Exists(Path.Combine(_tempDir, "lib", "net8.0", "Test.json")));
        }

        [Fact]
        public async Task WriteOutputAsync_JsonContainsAssemblyAndTypes()
        {
            var writer = new JsonWriter(_tempDir);
            await writer.WriteOutputAsync(MakeResult());

            var json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "Test.json"));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            Assert.Equal("Test.Assembly, Version=1.0.0.0", root.GetProperty("Assembly").GetString());
            Assert.Equal(1, root.GetProperty("Types").GetArrayLength());
        }

        [Fact]
        public async Task WriteOutputAsync_JsonContainsInvocationData()
        {
            var writer = new JsonWriter(_tempDir);
            await writer.WriteOutputAsync(MakeResult());

            var json = await File.ReadAllTextAsync(Path.Combine(_tempDir, "Test.json"));
            using var doc = JsonDocument.Parse(json);

            var invocation = doc.RootElement
                .GetProperty("Types")[0]
                .GetProperty("Methods")[0]
                .GetProperty("Invocations")[0];

            Assert.Equal("System.Console::WriteLine", invocation.GetProperty("Invocation").GetString());
            Assert.Equal("System.Void", invocation.GetProperty("ReturnType").GetString());
            Assert.Equal(0, invocation.GetProperty("Sequence").GetInt32());
        }

        [Fact]
        public async Task WriteOutputAsync_CreatesOutputFolderIfNotExists()
        {
            var nestedDir = Path.Combine(_tempDir, "x", "y");
            var writer = new JsonWriter(nestedDir);

            await writer.WriteOutputAsync(MakeResult());

            Assert.True(File.Exists(Path.Combine(nestedDir, "Test.json")));
        }
    }
}
