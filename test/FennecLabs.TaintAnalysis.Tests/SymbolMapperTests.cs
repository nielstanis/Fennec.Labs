using System.Linq;
using FennecLabs.TestUtilities;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace FennecLabs.TaintAnalysis.Tests;

public class SymbolMapperTests
{
    [Fact]
    public void Map_InstructionAtSequencePointOffset_ReturnsExactFidelity()
    {
        var assemblyPath = TestResources.GetTestProjectAssembly("BasicMvcApp");
        using var mapper = new SymbolMapper(assemblyPath);

        Assert.True(mapper.PdbPresent);

        var (method, instruction, sequencePoint) = FindInstructionAtSequencePoint(mapper.Assembly);

        var result = mapper.Map(instruction, method);

        Assert.Equal("exact", result.Fidelity);
        Assert.Equal(sequencePoint.Document.Url, result.File);
        Assert.Equal(sequencePoint.StartLine, result.StartLine);
        Assert.Equal(sequencePoint.StartColumn, result.StartColumn);
        Assert.Equal(sequencePoint.EndLine, result.EndLine);
        Assert.Equal(sequencePoint.EndColumn, result.EndColumn);
        Assert.Null(result.MetadataToken);
    }

    [Fact]
    public void Map_CompilerSynthesisedInstructionBetweenSequencePoints_ReturnsApproximateFidelity()
    {
        var assemblyPath = TestResources.GetTestProjectAssembly("BasicConsole");
        using var mapper = new SymbolMapper(assemblyPath);

        Assert.True(mapper.PdbPresent);

        var (method, instruction, precedingSequencePoint) = FindInstructionBetweenSequencePoints(mapper.Assembly);

        var result = mapper.Map(instruction, method);

        Assert.Equal("approximate", result.Fidelity);
        Assert.Equal(precedingSequencePoint.Document.Url, result.File);
        Assert.Equal(precedingSequencePoint.StartLine, result.StartLine);
        Assert.Equal(precedingSequencePoint.StartColumn, result.StartColumn);
        Assert.Null(result.MetadataToken);
    }

    [Fact]
    public void Map_AssemblyWithoutPdb_ReturnsUnresolvedFidelityWithMetadataToken()
    {
        var originalAssemblyPath = TestResources.GetTestProjectAssembly("BasicConsole");
        var tempDir = Directory.CreateTempSubdirectory("fennec-symbolmapper-nopdb-");
        try
        {
            var copiedDllPath = Path.Combine(tempDir.FullName, Path.GetFileName(originalAssemblyPath));
            File.Copy(originalAssemblyPath, copiedDllPath);

            using var mapper = new SymbolMapper(copiedDllPath);

            Assert.False(mapper.PdbPresent);

            var method = mapper.Assembly.MainModule.Types
                .Single(t => t.Name == "Program")
                .Methods.Single(m => m.Name == "Main");
            var instruction = method.Body.Instructions.First();

            var result = mapper.Map(instruction, method);

            Assert.Equal("unresolved", result.Fidelity);
            Assert.Null(result.File);
            Assert.Null(result.StartLine);
            Assert.Null(result.StartColumn);
            Assert.Null(result.EndLine);
            Assert.Null(result.EndColumn);
            Assert.NotNull(result.MetadataToken);
            Assert.Equal($"0x{method.MetadataToken.ToUInt32():X8}", result.MetadataToken);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void Map_PdbPresentButMismatched_HandlesGracefullyAndTreatsSymbolsAsAbsent()
    {
        var mvcAppDllPath = TestResources.GetTestProjectAssembly("BasicMvcApp");
        var consolePdbPath = Path.ChangeExtension(TestResources.GetTestProjectAssembly("BasicConsole"), ".pdb");

        var tempDir = Directory.CreateTempSubdirectory("fennec-symbolmapper-mismatch-");
        try
        {
            var copiedDllPath = Path.Combine(tempDir.FullName, Path.GetFileName(mvcAppDllPath));
            var copiedPdbPath = Path.ChangeExtension(copiedDllPath, ".pdb");
            File.Copy(mvcAppDllPath, copiedDllPath);
            File.Copy(consolePdbPath, copiedPdbPath);

            using var mapper = new SymbolMapper(copiedDllPath);

            Assert.False(mapper.PdbPresent);

            var method = mapper.Assembly.MainModule.Types
                .SelectMany(t => t.Methods)
                .First(m => m.HasBody);
            var instruction = method.Body.Instructions.First();

            var result = mapper.Map(instruction, method);

            Assert.Equal("unresolved", result.Fidelity);
            Assert.NotNull(result.MetadataToken);
        }
        finally
        {
            tempDir.Delete(recursive: true);
        }
    }

    private static (MethodDefinition Method, Instruction Instruction, SequencePoint SequencePoint) FindInstructionAtSequencePoint(AssemblyDefinition assembly)
    {
        foreach (var type in assembly.MainModule.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody || !method.DebugInformation.HasSequencePoints)
                {
                    continue;
                }

                foreach (var sequencePoint in method.DebugInformation.SequencePoints)
                {
                    if (sequencePoint.IsHidden)
                    {
                        continue;
                    }

                    var instruction = method.Body.Instructions.FirstOrDefault(i => i.Offset == sequencePoint.Offset);
                    if (instruction is not null)
                    {
                        return (method, instruction, sequencePoint);
                    }
                }
            }
        }

        throw new InvalidOperationException("No method with a resolvable sequence point was found in the fixture assembly.");
    }

    private static (MethodDefinition Method, Instruction Instruction, SequencePoint PrecedingSequencePoint) FindInstructionBetweenSequencePoints(AssemblyDefinition assembly)
    {
        foreach (var type in assembly.MainModule.Types)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody || !method.DebugInformation.HasSequencePoints)
                {
                    continue;
                }

                var nonHiddenSequencePoints = method.DebugInformation.SequencePoints
                    .Where(sp => !sp.IsHidden)
                    .OrderBy(sp => sp.Offset)
                    .ToList();

                if (nonHiddenSequencePoints.Count == 0)
                {
                    continue;
                }

                var sequencePointOffsets = nonHiddenSequencePoints.Select(sp => sp.Offset).ToHashSet();

                foreach (var instruction in method.Body.Instructions)
                {
                    if (sequencePointOffsets.Contains(instruction.Offset))
                    {
                        continue;
                    }

                    var preceding = nonHiddenSequencePoints.LastOrDefault(sp => sp.Offset < instruction.Offset);
                    if (preceding is not null)
                    {
                        return (method, instruction, preceding);
                    }
                }
            }
        }

        throw new InvalidOperationException("No compiler-synthesised instruction between sequence points was found in the fixture assembly.");
    }
}
