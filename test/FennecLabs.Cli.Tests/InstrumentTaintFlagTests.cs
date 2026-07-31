using System.Diagnostics;
using FennecLabs.Cli.Commands;
using FennecLabs.Cli.Commands.Taint;
using FennecLabs.NuGet;

namespace FennecLabs.Cli.Tests;

public class InstrumentTaintFlagTests
{
    // --- AC-1: `instrument` without --taint is completely unchanged ---

    [Fact]
    public async Task ExecuteAsync_WithoutTaint_ReturnsZero_AndProducesNoTaintFiles()
    {
        var dllPath = typeof(InstrumentTaintFlagTests).Assembly.Location;
        var outputRoot = UniqueTempDirectory();
        Directory.CreateDirectory(outputRoot);

        try
        {
            var handler = new InstrumentCommandHandler(new NuGetService());
            var exitCode = await handler.ExecuteAsync(dllPath, null, null, outputRoot, "fxt", OutputMode.Human);

            Assert.Equal(0, exitCode);

            var instrumentDir = Path.Combine(outputRoot, "instrument");
            Assert.True(Directory.Exists(instrumentDir));

            var taintPaths = Directory
                .EnumerateFileSystemEntries(outputRoot, "*", SearchOption.AllDirectories)
                .Where(path => path.Contains("taint", StringComparison.OrdinalIgnoreCase))
                .ToList();
            Assert.Empty(taintPaths);
        }
        finally
        {
            Directory.Delete(outputRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ImplicitTaintOptions_MatchesExplicitDisabled()
    {
        var dllPath = typeof(InstrumentTaintFlagTests).Assembly.Location;
        var defaultOutputRoot = UniqueTempDirectory();
        var explicitOutputRoot = UniqueTempDirectory();
        Directory.CreateDirectory(defaultOutputRoot);
        Directory.CreateDirectory(explicitOutputRoot);

        try
        {
            var handler = new InstrumentCommandHandler(new NuGetService());

            var defaultExitCode = await handler.ExecuteAsync(
                dllPath, null, null, defaultOutputRoot, "fxt", OutputMode.Human);
            var explicitExitCode = await handler.ExecuteAsync(
                dllPath, null, null, explicitOutputRoot, "fxt", OutputMode.Human, TaintOptions.Disabled);

            Assert.Equal(0, defaultExitCode);
            Assert.Equal(explicitExitCode, defaultExitCode);

            var defaultFiles = RelativeFileNames(defaultOutputRoot);
            var explicitFiles = RelativeFileNames(explicitOutputRoot);
            Assert.Equal(defaultFiles, explicitFiles);
        }
        finally
        {
            Directory.Delete(defaultOutputRoot, recursive: true);
            Directory.Delete(explicitOutputRoot, recursive: true);
        }
    }

    // --- AC-5: `--help` lists all 7 taint flags ---

    [Fact]
    public async Task InstrumentHelp_ListsAllTaintFlags()
    {
        var (exitCode, stdOut, _) = await RunCliAsync("instrument", "--help");

        Assert.Equal(0, exitCode);
        Assert.Contains("--taint", stdOut);
        Assert.Contains("--taint-policy", stdOut);
        Assert.Contains("--taint-max-depth", stdOut);
        Assert.Contains("--taint-timeout", stdOut);
        Assert.Contains("--taint-llm-handoff", stdOut);
        Assert.Contains("--taint-include-third-party", stdOut);
        Assert.Contains("--taint-second-party-prefix", stdOut);

        // Existing flags remain functional.
        Assert.Contains("--filename", stdOut);
        Assert.Contains("--nuget", stdOut);
        Assert.Contains("--version", stdOut);
        Assert.Contains("--file-format", stdOut);
    }

    // --- AC-6: `.csproj` with missing `bin/` fails with an actionable message ---

    [Fact]
    public void BuildGraphReader_Resolve_Csproj_WithoutBuildOutput_ThrowsActionableException()
    {
        var projectDir = UniqueTempDirectory();
        Directory.CreateDirectory(projectDir);
        var csprojPath = Path.Combine(projectDir, "App.csproj");
        File.WriteAllText(csprojPath, MinimalCsproj());

        try
        {
            var ex = Assert.Throws<BuildOutputNotFoundException>(() => BuildGraphReader.Resolve(csprojPath));
            Assert.Contains("dotnet build", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Fact]
    public async Task InstrumentTaintCsproj_WithoutBuildOutput_ReturnsNonZero_WithActionableMessage()
    {
        var projectDir = UniqueTempDirectory();
        Directory.CreateDirectory(projectDir);
        var csprojPath = Path.Combine(projectDir, "App.csproj");
        File.WriteAllText(csprojPath, MinimalCsproj());

        try
        {
            var (exitCode, _, stdErr) = await RunCliAsync("instrument", "--taint", "--filename", csprojPath);

            Assert.NotEqual(0, exitCode);
            Assert.Contains("dotnet build", stdErr, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    // --- AC-2/3/4: BuildGraphReader resolves .csproj / .sln / .slnx ---

    [Fact]
    public void BuildGraphReader_Resolve_Csproj_ResolvesBuildOutputDll()
    {
        var projectDir = UniqueTempDirectory();
        var csprojPath = Path.Combine(projectDir, "App.csproj");
        var expectedDll = CreateBuiltProject(projectDir, "App");

        try
        {
            var resolved = BuildGraphReader.Resolve(csprojPath);
            Assert.Single(resolved);
            Assert.Equal(expectedDll, resolved[0]);
        }
        finally
        {
            Directory.Delete(projectDir, recursive: true);
        }
    }

    [Fact]
    public void BuildGraphReader_Resolve_Sln_ResolvesAllProjectDlls()
    {
        var solutionDir = UniqueTempDirectory();
        Directory.CreateDirectory(solutionDir);

        var app1Dir = Path.Combine(solutionDir, "App1");
        var app2Dir = Path.Combine(solutionDir, "App2");
        var app1Dll = CreateBuiltProject(app1Dir, "App1");
        var app2Dll = CreateBuiltProject(app2Dir, "App2");

        var slnPath = Path.Combine(solutionDir, "MySolution.sln");
        var slnContent = "Microsoft Visual Studio Solution File, Format Version 12.00\n" +
            $"Project(\"{{{Guid.NewGuid()}}}\") = \"App1\", \"App1\\App1.csproj\", \"{{{Guid.NewGuid()}}}\"\n" +
            "EndProject\n" +
            $"Project(\"{{{Guid.NewGuid()}}}\") = \"App2\", \"App2\\App2.csproj\", \"{{{Guid.NewGuid()}}}\"\n" +
            "EndProject\n";
        File.WriteAllText(slnPath, slnContent);

        try
        {
            var resolved = BuildGraphReader.Resolve(slnPath);
            Assert.Equal(2, resolved.Count);
            Assert.Contains(app1Dll, resolved);
            Assert.Contains(app2Dll, resolved);
        }
        finally
        {
            Directory.Delete(solutionDir, recursive: true);
        }
    }

    [Fact]
    public void BuildGraphReader_Resolve_Slnx_ResolvesAllProjectDlls()
    {
        var solutionDir = UniqueTempDirectory();
        Directory.CreateDirectory(solutionDir);

        var app1Dir = Path.Combine(solutionDir, "App1");
        var app2Dir = Path.Combine(solutionDir, "App2");
        var app1Dll = CreateBuiltProject(app1Dir, "App1");
        var app2Dll = CreateBuiltProject(app2Dir, "App2");

        var slnxPath = Path.Combine(solutionDir, "MySolution.slnx");
        File.WriteAllText(slnxPath, """
            <Solution>
              <Project Path="App1/App1.csproj" />
              <Project Path="App2/App2.csproj" />
            </Solution>
            """);

        try
        {
            var resolved = BuildGraphReader.Resolve(slnxPath);
            Assert.Equal(2, resolved.Count);
            Assert.Contains(app1Dll, resolved);
            Assert.Contains(app2Dll, resolved);
        }
        finally
        {
            Directory.Delete(solutionDir, recursive: true);
        }
    }

    // --- Helpers ---

    private static string MinimalCsproj() => """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    /// <summary>Creates a minimal .csproj plus a fake bin/Debug/net10.0/{name}.dll build output.</summary>
    private static string CreateBuiltProject(string projectDir, string name)
    {
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, $"{name}.csproj"), MinimalCsproj());

        var binDir = Path.Combine(projectDir, "bin", "Debug", "net10.0");
        Directory.CreateDirectory(binDir);
        var dllPath = Path.Combine(binDir, $"{name}.dll");
        File.WriteAllBytes(dllPath, [0x4D, 0x5A]); // fake PE header bytes; reader only checks existence.
        return Path.GetFullPath(dllPath);
    }

    private static List<string> RelativeFileNames(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(params string[] args)
    {
        var fennecDllPath = Path.Combine(AppContext.BaseDirectory, "Fennec.dll");
        var startInfo = new ProcessStartInfo(Environment.ProcessPath ?? "dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add(fennecDllPath);
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo)!;
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await stdOutTask, await stdErrTask);
    }

    private static string UniqueTempDirectory() =>
        Path.Combine(Path.GetTempPath(), "fennec-tests", Guid.NewGuid().ToString("N"));
}
