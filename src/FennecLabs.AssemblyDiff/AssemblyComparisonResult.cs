using System.Text;

namespace FennecLabs.AssemblyDiff;

public class AssemblyComparisonResult
{
    public string Assembly1Name { get; set; } = string.Empty;
    public string Assembly2Name { get; set; } = string.Empty;
    public List<DiffEvent> Events { get; } = [];

    public bool AreEqual => Events.Count == 0;

    public IEnumerable<string> TypesOnlyInAssembly1 =>
        Events.OfType<TypePresenceDiff>()
              .Where(e => e.Kind == DiffKind.Removed)
              .Select(e => e.TypeName);

    public IEnumerable<string> TypesOnlyInAssembly2 =>
        Events.OfType<TypePresenceDiff>()
              .Where(e => e.Kind == DiffKind.Added)
              .Select(e => e.TypeName);

    public IEnumerable<MethodBodyInstructionsDiff> MethodBodyChanges =>
        Events.OfType<MethodBodyInstructionsDiff>();

    public string GenerateReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("===== Assembly Comparison Report =====");
        sb.AppendLine();
        sb.AppendLine($"Assembly 1: {Assembly1Name}");
        sb.AppendLine($"Assembly 2: {Assembly2Name}");
        sb.AppendLine();

        if (AreEqual)
        {
            sb.AppendLine("✓ Assemblies are identical");
        }
        else
        {
            sb.AppendLine($"✗ Found {Events.Count} difference(s)");
            sb.AppendLine();

            var typesOnly1 = TypesOnlyInAssembly1.ToList();
            if (typesOnly1.Count > 0)
            {
                sb.AppendLine($"Types only in Assembly 1: {typesOnly1.Count}");
                foreach (var type in typesOnly1.Take(10))
                    sb.AppendLine($"  - {type}");
                if (typesOnly1.Count > 10)
                    sb.AppendLine($"  ... and {typesOnly1.Count - 10} more");
                sb.AppendLine();
            }

            var typesOnly2 = TypesOnlyInAssembly2.ToList();
            if (typesOnly2.Count > 0)
            {
                sb.AppendLine($"Types only in Assembly 2: {typesOnly2.Count}");
                foreach (var type in typesOnly2.Take(10))
                    sb.AppendLine($"  - {type}");
                if (typesOnly2.Count > 10)
                    sb.AppendLine($"  ... and {typesOnly2.Count - 10} more");
                sb.AppendLine();
            }

            sb.AppendLine("All Differences:");
            foreach (var evt in Events)
                sb.AppendLine($"  - {evt.FormatMessage()}");

            var bodyChanges = MethodBodyChanges.ToList();
            if (bodyChanges.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Methods with body differences: {bodyChanges.Count}");
                sb.AppendLine("(Use MethodBodyChanges property for detailed IL comparison)");
            }
        }

        return sb.ToString();
    }

    public override string ToString() => GenerateReport();
}
