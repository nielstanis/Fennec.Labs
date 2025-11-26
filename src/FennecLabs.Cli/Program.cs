using System.CommandLine;

namespace FennecLabs;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Fennec Labs CLI");

        var filenameOption = new Option<string>(
            "--filename",
            "-f"
        );

        var instrumentCommand = new Command("instrument", "Instrument command");
        instrumentCommand.Options.Add(filenameOption);
        instrumentCommand.SetAction(async (ParseResult parseResult) =>
        {
            var filename = parseResult.GetValue(filenameOption);
            Console.WriteLine($"Executing instrument command with filename: {filename}");
        });

        var scorecardCommand = new Command("scorecard", "Scorecard command");
        scorecardCommand.Options.Add(filenameOption);
        scorecardCommand.SetAction((ParseResult parseResult) => 
        {
            var filename = parseResult.GetValue(filenameOption);
            Console.WriteLine($"Executing scorecard command with filename: {filename}");
        });

        rootCommand.Subcommands.Add(instrumentCommand);
        rootCommand.Subcommands.Add(scorecardCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }
}
