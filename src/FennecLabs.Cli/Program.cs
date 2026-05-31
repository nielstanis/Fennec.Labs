using System.CommandLine;
using FennecLabs.Cli;
using FennecLabs.Cli.Commands;
using FennecLabs.NuGet;
using FennecLabs.Scorecard;

namespace FennecLabs;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Fennec Labs CLI");

        var globalJsonOption = new Option<bool>("--json", "-j")
        {
            Description = "Write output as JSON",
            Recursive = true,
        };
        rootCommand.Options.Add(globalJsonOption);

        var globalOutputOption = new Option<string>("--output", "-o")
        {
            Description = "Root folder for all file output (default: .fennec)",
            DefaultValueFactory = _ => ".fennec",
            Recursive = true,
        };
        rootCommand.Options.Add(globalOutputOption);

        var globalNoCacheOption = new Option<bool>("--no-cache", "-C")
        {
            Description = "Bypass cached results and force a fresh run",
            Recursive = true,
        };
        rootCommand.Options.Add(globalNoCacheOption);

        // instrument command
        var filenameOption = new Option<string>("--filename", "-f")
        {
            Description = "Path to the assembly file to instrument"
        };
        var nugetOption = new Option<string>("--nuget", "-n")
        {
            Description = "NuGet package ID to download and instrument"
        };
        var versionOption = new Option<string>("--version", "-v")
        {
            Description = "Version of the NuGet package (optional, uses latest if not specified)"
        };
        var fileFormatOption = new Option<string>("--file-format", "-F")
        {
            Description = "File output format: fxt or json (default: fxt)",
            DefaultValueFactory = _ => "fxt"
        };

        var instrumentCommand = new Command("instrument", "Instrument assembly files or NuGet packages");
        instrumentCommand.Options.Add(filenameOption);
        instrumentCommand.Options.Add(nugetOption);
        instrumentCommand.Options.Add(versionOption);
        instrumentCommand.Options.Add(fileFormatOption);
        instrumentCommand.SetAction(async (ParseResult parseResult) =>
        {
            var filename = parseResult.GetValue(filenameOption);
            var nuget = parseResult.GetValue(nugetOption);

            if (string.IsNullOrWhiteSpace(filename) && string.IsNullOrWhiteSpace(nuget))
            {
                Console.Error.WriteLine("Either --filename or --nuget is required.");
                await rootCommand.Parse(["instrument", "--help"]).InvokeAsync();
                return 1;
            }

            var handler = new InstrumentCommandHandler(new NuGetService());
            return await handler.ExecuteAsync(
                filename,
                nuget,
                parseResult.GetValue(versionOption),
                parseResult.GetValue(globalOutputOption) ?? ".fennec",
                parseResult.GetValue(fileFormatOption) ?? "fxt",
                ResolveOutputMode(parseResult.GetValue(globalJsonOption)));
        });

        // scorecard command
        var projectPathOption = new Option<string>("--project", "-p")
        {
            Description = "Path to the .csproj file"
        };
        var reportFormatOption = new Option<string>("--report-format", "-r")
        {
            Description = "Generate a report in the specified format(s): html, md, or html,md"
        };

        var scorecardCommand = new Command("scorecard", "Get security scorecards for packages in a project");
        scorecardCommand.Options.Add(projectPathOption);
        scorecardCommand.Options.Add(reportFormatOption);
        scorecardCommand.SetAction(async (ParseResult parseResult) =>
        {
            var handler = new ScorecardCommandHandler(new ScorecardClient());
            return await handler.ExecuteAsync(
                parseResult.GetValue(projectPathOption),
                parseResult.GetValue(reportFormatOption),
                ResolveOutputMode(parseResult.GetValue(globalJsonOption)),
                parseResult.GetValue(globalOutputOption) ?? ".fennec");
        });

        // compare command
        var compareNugetOption = new Option<string>("--nuget", "-n")
        {
            Description = "NuGet package ID to compare"
        };
        var compareVersionOption = new Option<string>("--version", "-v")
        {
            Description = "Version to compare (optional, uses latest if not specified)"
        };
        var compareFileOption = new Option<string[]>("--file", "-f")
        {
            Description = "Two .dll or .nupkg files to compare",
            Arity = new ArgumentArity(2, 2),
            AllowMultipleArgumentsPerToken = true,
        };

        var compareCommand = new Command("compare",
            "Compare assemblies between two NuGet versions or two local .dll/.nupkg files");
        compareCommand.Options.Add(compareNugetOption);
        compareCommand.Options.Add(compareVersionOption);
        compareCommand.Options.Add(compareFileOption);
        compareCommand.SetAction(async (ParseResult parseResult) =>
        {
            var nuget = parseResult.GetValue(compareNugetOption);
            var files = parseResult.GetValue(compareFileOption);

            if (files is { Length: 2 })
            {
                if (!string.IsNullOrWhiteSpace(nuget))
                {
                    Console.Error.WriteLine("--file and --nuget are mutually exclusive.");
                    return 1;
                }

                var localHandler = new CompareLocalFilesCommandHandler();
                return await localHandler.ExecuteAsync(
                    files[0],
                    files[1],
                    ResolveOutputMode(parseResult.GetValue(globalJsonOption)));
            }

            if (string.IsNullOrWhiteSpace(nuget))
            {
                Console.Error.WriteLine("Either --nuget or --file is required.");
                await rootCommand.Parse(["compare", "--help"]).InvokeAsync();
                return 1;
            }

            var handler = new CompareCommandHandler(new NuGetService());
            return await handler.ExecuteAsync(
                nuget,
                parseResult.GetValue(compareVersionOption),
                ResolveOutputMode(parseResult.GetValue(globalJsonOption)),
                parseResult.GetValue(globalOutputOption) ?? ".fennec",
                parseResult.GetValue(globalNoCacheOption));
        });

        // reproduce command
        var reproduceFilenameOption = new Option<string>("--filename", "-f")
        {
            Description = "Path to the .nupkg file to compare"
        };
        var reproduceDirOption = new Option<string>("--directory", "-d")
        {
            Description = "Path to a build output directory of .dll files to compare"
        };
        var reproduceTfmOption = new Option<string>("--tfm", "-t")
        {
            Description = "Target framework moniker (e.g. net8.0); derived from directory name if omitted"
        };
        var reproduceNugetOption = new Option<string>("--nuget", "-n")
        {
            Description = "NuGet package ID to compare against",
            Required = true,
        };
        var reproduceVersionOption = new Option<string>("--version", "-v")
        {
            Description = "Version to compare against (optional, uses latest if not specified)"
        };

        var reproduceCommand = new Command("reproduce", "Compare a local .nupkg file or directory with a NuGet package from the feed");
        reproduceCommand.Options.Add(reproduceFilenameOption);
        reproduceCommand.Options.Add(reproduceDirOption);
        reproduceCommand.Options.Add(reproduceTfmOption);
        reproduceCommand.Options.Add(reproduceNugetOption);
        reproduceCommand.Options.Add(reproduceVersionOption);
        reproduceCommand.SetAction(async (ParseResult parseResult) =>
        {
            var directory = parseResult.GetValue(reproduceDirOption);
            var filename = parseResult.GetValue(reproduceFilenameOption);
            var tfm = parseResult.GetValue(reproduceTfmOption);

            if (!string.IsNullOrWhiteSpace(tfm) && string.IsNullOrWhiteSpace(directory))
            {
                Console.Error.WriteLine("--tfm requires --directory.");
                await rootCommand.Parse(["reproduce", "--help"]).InvokeAsync();
                return 1;
            }

            if (string.IsNullOrWhiteSpace(directory) && string.IsNullOrWhiteSpace(filename))
            {
                Console.Error.WriteLine("Either --filename or --directory is required.");
                await rootCommand.Parse(["reproduce", "--help"]).InvokeAsync();
                return 1;
            }

            var handler = new ReproduceCommandHandler(new NuGetService());
            return await handler.ExecuteAsync(
                string.IsNullOrWhiteSpace(directory) ? filename : null,
                string.IsNullOrWhiteSpace(directory) ? null : directory,
                tfm,
                parseResult.GetValue(reproduceNugetOption)!,
                parseResult.GetValue(reproduceVersionOption),
                ResolveOutputMode(parseResult.GetValue(globalJsonOption)),
                parseResult.GetValue(globalOutputOption) ?? ".fennec",
                parseResult.GetValue(globalNoCacheOption));
        });

        // feeds command
        var feedsCommand = new Command("feeds", "Manage NuGet feed sources");

        var feedsListCommand = new Command("list", "List configured NuGet feeds");
        feedsListCommand.SetAction(async (ParseResult parseResult) =>
        {
            var handler = new FeedsCommandHandler();
            return await handler.ExecuteListAsync(
                ResolveOutputMode(parseResult.GetValue(globalJsonOption)));
        });
        feedsCommand.Subcommands.Add(feedsListCommand);

        var feedAddNameOption = new Option<string>("--name", "-n") { Description = "Feed name", Required = true };
        var feedAddSourceOption = new Option<string>("--source", "-s") { Description = "Feed source URL", Required = true };
        var feedAddDefaultOption = new Option<bool>("--default", "-d") { Description = "Set as default feed" };

        var feedsAddCommand = new Command("add", "Add a NuGet feed source");
        feedsAddCommand.Options.Add(feedAddNameOption);
        feedsAddCommand.Options.Add(feedAddSourceOption);
        feedsAddCommand.Options.Add(feedAddDefaultOption);
        feedsAddCommand.SetAction(async (ParseResult parseResult) =>
        {
            var handler = new FeedsCommandHandler();
            return await handler.ExecuteAddAsync(
                parseResult.GetValue(feedAddNameOption)!,
                parseResult.GetValue(feedAddSourceOption)!,
                parseResult.GetValue(feedAddDefaultOption),
                ResolveOutputMode(parseResult.GetValue(globalJsonOption)));
        });
        feedsCommand.Subcommands.Add(feedsAddCommand);

        var feedRemoveNameOption = new Option<string>("--name", "-n") { Description = "Name of the feed to remove", Required = true };

        var feedsRemoveCommand = new Command("remove", "Remove a NuGet feed source");
        feedsRemoveCommand.Options.Add(feedRemoveNameOption);
        feedsRemoveCommand.SetAction(async (ParseResult parseResult) =>
        {
            var handler = new FeedsCommandHandler();
            return await handler.ExecuteRemoveAsync(
                parseResult.GetValue(feedRemoveNameOption)!,
                ResolveOutputMode(parseResult.GetValue(globalJsonOption)));
        });
        feedsCommand.Subcommands.Add(feedsRemoveCommand);

        rootCommand.Subcommands.Add(instrumentCommand);
        rootCommand.Subcommands.Add(scorecardCommand);
        rootCommand.Subcommands.Add(compareCommand);
        rootCommand.Subcommands.Add(reproduceCommand);
        rootCommand.Subcommands.Add(feedsCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static OutputMode ResolveOutputMode(bool json) =>
        json ? OutputMode.Json : OutputMode.Human;
}
