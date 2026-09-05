using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

namespace FennecLabs.Cli;

/// <summary>
/// Appends an "Examples:" section to <c>--help</c> output for commands that register examples,
/// so users get copy-pasteable invocations without leaving the terminal.
/// </summary>
internal static class HelpExamples
{
    private static readonly Dictionary<Command, string[]> Registry = [];

    internal static Command WithExamples(this Command command, params string[] examples)
    {
        Registry[command] = examples;
        return command;
    }

    /// <summary>
    /// Wraps the recursive help option's action so every command's help output gains an
    /// Examples section when examples were registered for it.
    /// </summary>
    internal static void Install(RootCommand rootCommand)
    {
        var helpOption = rootCommand.Options.OfType<HelpOption>().FirstOrDefault();
        if (helpOption?.Action is not SynchronousCommandLineAction inner)
            return;

        helpOption.Action = new ExamplesHelpAction(inner);
    }

    private sealed class ExamplesHelpAction(SynchronousCommandLineAction inner) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            var result = inner.Invoke(parseResult);

            if (Registry.TryGetValue(parseResult.CommandResult.Command, out var examples) &&
                examples.Length > 0)
            {
                var output = parseResult.InvocationConfiguration.Output;
                output.WriteLine("Examples:");
                foreach (var example in examples)
                    output.WriteLine($"  {example}");
                output.WriteLine();
            }

            return result;
        }
    }
}
