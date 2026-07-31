using FennecLabs.TaintAnalysis.Models;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace FennecLabs.TaintAnalysis;

/// <summary>
/// Loads an assembly's debug symbols (Portable PDB, embedded or co-located) and correlates
/// IL instructions to source file/line/column positions.
/// </summary>
/// <remarks>
/// Symbol loading is best-effort: a missing PDB, or a PDB that does not match the assembly
/// (wrong MVID), never causes a load failure. In both cases <see cref="PdbPresent"/> is
/// <c>false</c> and every <see cref="Map"/> call returns an <c>unresolved</c> <see cref="SourceRef"/>.
/// </remarks>
public sealed class SymbolMapper : IDisposable
{
    /// <summary>The loaded assembly definition (with symbols, when available).</summary>
    public AssemblyDefinition Assembly { get; }

    /// <summary>Whether debug symbols were successfully loaded for this assembly.</summary>
    public bool PdbPresent { get; }

    public SymbolMapper(string assemblyPath)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
        {
            throw new ArgumentException("Assembly path must not be null or empty.", nameof(assemblyPath));
        }

        var readerParameters = new ReaderParameters
        {
            ReadSymbols = true,
            SymbolReaderProvider = new DefaultSymbolReaderProvider(throwIfNoSymbol: false),
            ThrowIfSymbolsAreNotMatching = false,
        };

        try
        {
            Assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
        }
        catch (SymbolsNotFoundException)
        {
            Assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadSymbols = false });
        }
        catch (SymbolsNotMatchingException)
        {
            Assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadSymbols = false });
        }

        PdbPresent = Assembly.MainModule.HasSymbols;
    }

    /// <summary>
    /// Maps an IL instruction within <paramref name="method"/> to a <see cref="SourceRef"/>.
    /// </summary>
    /// <remarks>
    /// Resolution order: an exact, non-hidden sequence point at the instruction's offset
    /// (fidelity <c>exact</c>); otherwise the nearest preceding non-hidden sequence point
    /// (fidelity <c>approximate</c>); otherwise <c>unresolved</c>, with
    /// <see cref="SourceRef.MetadataToken"/> set to the method's metadata token.
    /// </remarks>
    public SourceRef Map(Instruction instruction, MethodDefinition method)
    {
        if (instruction is null)
        {
            throw new ArgumentNullException(nameof(instruction));
        }

        if (method is null)
        {
            throw new ArgumentNullException(nameof(method));
        }

        var debugInfo = method.DebugInformation;

        if (PdbPresent && debugInfo is { HasSequencePoints: true })
        {
            SequencePoint? nearestPreceding = null;

            foreach (var sequencePoint in debugInfo.SequencePoints)
            {
                if (sequencePoint.IsHidden || sequencePoint.Offset > instruction.Offset)
                {
                    continue;
                }

                if (sequencePoint.Offset == instruction.Offset)
                {
                    return ToSourceRef(sequencePoint, fidelity: "exact");
                }

                if (nearestPreceding is null || sequencePoint.Offset > nearestPreceding.Offset)
                {
                    nearestPreceding = sequencePoint;
                }
            }

            if (nearestPreceding is not null)
            {
                return ToSourceRef(nearestPreceding, fidelity: "approximate");
            }
        }

        return new SourceRef
        {
            Fidelity = "unresolved",
            MetadataToken = FormatMetadataToken(method),
        };
    }

    private static SourceRef ToSourceRef(SequencePoint sequencePoint, string fidelity) => new()
    {
        File = sequencePoint.Document?.Url,
        StartLine = sequencePoint.StartLine,
        StartColumn = sequencePoint.StartColumn,
        EndLine = sequencePoint.EndLine,
        EndColumn = sequencePoint.EndColumn,
        Fidelity = fidelity,
    };

    private static string FormatMetadataToken(MethodDefinition method) =>
        $"0x{method.MetadataToken.ToUInt32():X8}";

    public void Dispose() => Assembly.Dispose();
}
