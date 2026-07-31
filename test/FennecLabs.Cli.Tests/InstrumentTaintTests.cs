using System.Text.Json;
using FennecLabs.Cli.Commands;
using FennecLabs.Cli.Commands.Taint;
using FennecLabs.NuGet;
using FennecLabs.TestUtilities;

namespace FennecLabs.Cli.Tests;

public class InstrumentTaintTests
{
    [Trait("Category", "Integration")]
    [Fact]
    public async Task ExecuteAsync_TaintOnProjectInput_ProducesNonEmptyInventories()
    {
        var csprojPath = TestResources.GetTestProjectCsprojPath("BasicConsole");
        var output = UniqueTempDir();

        try
        {
            var handler = new InstrumentCommandHandler(new NuGetService());
            var taintOptions = new TaintOptions(
                Enabled: true,
                PolicyPath: null,
                MaxDepth: 8,
                TimeoutSeconds: 120,
                LlmHandoff: false,
                IncludeThirdParty: false,
                SecondPartyPrefixes: []);

            var exitCode = await handler.ExecuteAsync(
                csprojPath, null, null, output, "fxt", OutputMode.Human, taintOptions);

            Assert.Equal(0, exitCode);

            var resultFiles = Directory.GetFiles(output, "result.json", SearchOption.AllDirectories);
            Assert.Single(resultFiles);

            using var document = JsonDocument.Parse(File.ReadAllText(resultFiles[0]));
            var payload = document.RootElement.GetProperty("payload");

            var sourcesInventory = payload.GetProperty("sourcesInventory");
            var sinksInventory = payload.GetProperty("sinksInventory");

            Assert.True(sourcesInventory.GetArrayLength() > 0);
            Assert.True(sinksInventory.GetArrayLength() > 0);

            // Findings stay empty in this vertical slice — no CFG/propagation engine yet.
            Assert.Equal(0, payload.GetProperty("findings").GetArrayLength());
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task ExecuteAsync_WithoutTaint_DoesNotProduceTaintArtifacts()
    {
        var dllPath = TestResources.GetTestProjectAssembly("BasicConsole");
        var output = UniqueTempDir();

        try
        {
            var handler = new InstrumentCommandHandler(new NuGetService());

            var exitCode = await handler.ExecuteAsync(
                dllPath, null, null, output, "fxt", OutputMode.Human);

            Assert.Equal(0, exitCode);
            Assert.Empty(Directory.GetFiles(output, "result.json", SearchOption.AllDirectories));
            Assert.False(Directory.Exists(Path.Combine(output, "instrument", "taint")));
        }
        finally
        {
            if (Directory.Exists(output))
                Directory.Delete(output, recursive: true);
        }
    }

    private static string UniqueTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fennec-taint-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
