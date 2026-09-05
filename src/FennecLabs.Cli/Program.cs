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
            Description = "Write output as JSON instead of the human-readable console view (suppresses progress output)",
            Recursive = true,
        };
        rootCommand.Options.Add(globalJsonOption);

        var globalOutputOption = new Option<string>("--output", "-o")
        {
            Description = "Root folder for all file output: cached results, reports, instrumentation dumps (default: .fennec)",
            DefaultValueFactory = _ => ".fennec",
            Recursive = true,
        };
        rootCommand.Options.Add(globalOutputOption);

        var globalNoCacheOption = new Option<bool>("--no-cache", "-C")
        {
            Description = "Bypass any cached result.json for this input and force a fresh run",
            Recursive = true,
        };
        rootCommand.Options.Add(globalNoCacheOption);

        // instrument command
        var filenameOption = new Option<string>("--filename", "-f")
        {
            Description = "Path to a local assembly file (.dll) to instrument. Required unless --nuget is used."
        };
        var nugetOption = new Option<string>("--nuget", "-n")
        {
            Description = "NuGet package ID to download and instrument; all library DLLs in the package are analyzed. Required unless --filename is used."
        };
        var versionOption = new Option<string>("--version", "-v")
        {
            Description = "Version of the NuGet package to use with --nuget (optional, uses latest if not specified)"
        };
        var fileFormatOption = new Option<string>("--file-format", "-F")
        {
            Description = "Output file format: fxt or json (default: fxt). Ignored when --json is used (writes to stdout instead of files).",
            DefaultValueFactory = _ => "fxt"
        };

        var instrumentCommand = new Command("instrument",
            "Extract IL-level method invocations from a local assembly or NuGet package. See docs/commands/instrument.md.");
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
            Description = "Path to the .csproj file to analyze (required)"
        };
        var reportFormatOption = new Option<string>("--report-format", "-r")
        {
            Description = "Generate a report in the specified format(s), co-located with result.json: html, md, or html,md"
        };

        var scorecardCommand = new Command("scorecard",
            "Fetch OpenSSF Scorecard results for a project's direct and transitive NuGet dependencies. See docs/commands/scorecard.md.");
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

        // dependencies command
        var dependenciesProjectPathOption = new Option<string>("--project", "-p")
        {
            Description = "Path to the .csproj file to analyze (required)"
        };

        var dependenciesCommand = new Command("dependencies",
            "Emit a normalized, canonical dependency graph artifact for a project's direct and transitive NuGet dependencies. See docs/commands/dependencies.md.");
        dependenciesCommand.Options.Add(dependenciesProjectPathOption);
        dependenciesCommand.SetAction(async (ParseResult parseResult) =>
        {
            var handler = new DependencyGraphCommandHandler();
            return await handler.ExecuteAsync(
                parseResult.GetValue(dependenciesProjectPathOption),
                ResolveOutputMode(parseResult.GetValue(globalJsonOption)),
                parseResult.GetValue(globalOutputOption) ?? ".fennec");
        });

        // compare command
        var compareNugetOption = new Option<string>("--nuget", "-n")
        {
            Description = "NuGet package ID to compare. Required unless --file is used; mutually exclusive with --file."
        };
        var compareVersionOption = new Option<string>("--version", "-v")
        {
            Description = "Version to compare against latest, used with --nuget (optional, compares the two most recent published versions if not specified)"
        };
        var compareFileOption = new Option<string[]>("--file", "-f")
        {
            Description = "Two local .dll or .nupkg files to compare directly (exactly two paths, no NuGet lookup, not cached). Mutually exclusive with --nuget.",
            Arity = new ArgumentArity(2, 2),
            AllowMultipleArgumentsPerToken = true,
        };

        var compareCommand = new Command("compare",
            "Diff assemblies structurally between two NuGet package versions, or between two local .dll/.nupkg files. See docs/commands/compare.md.");
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
            Description = "Path to the local .nupkg file to compare. Required unless --directory is used."
        };
        var reproduceDirOption = new Option<string>("--directory", "-d")
        {
            Description = "Path to a build output directory of .dll files to compare instead of a .nupkg. Required unless --filename is used."
        };
        var reproduceTfmOption = new Option<string>("--tfm", "-t")
        {
            Description = "Target framework moniker (e.g. net8.0) for --directory; auto-derived from the directory name/single TFM subdir when omitted, or prompted for interactively when ambiguous. Requires --directory."
        };
        var reproduceNugetOption = new Option<string>("--nuget", "-n")
        {
            Description = "NuGet package ID to compare against (required)",
            Required = true,
        };
        var reproduceVersionOption = new Option<string>("--version", "-v")
        {
            Description = "Version to compare against (optional, uses latest if not specified)"
        };

        var reproduceCommand = new Command("reproduce",
            "Verify a local .nupkg or build output directory reproduces the published NuGet.org package. See docs/commands/reproduce.md.");
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
        var feedsCommand = new Command("feeds",
            "Manage NuGet feed sources used to resolve packages for instrument/compare/reproduce. See docs/commands/feeds.md.");

        var feedsListCommand = new Command("list", "List configured NuGet feeds");
        feedsListCommand.SetAction(async (ParseResult parseResult) =>
        {
            var handler = new FeedsCommandHandler();
            return await handler.ExecuteListAsync(
                ResolveOutputMode(parseResult.GetValue(globalJsonOption)));
        });
        feedsCommand.Subcommands.Add(feedsListCommand);

        var feedAddNameOption = new Option<string>("--name", "-n") { Description = "Name to register the feed under (required)", Required = true };
        var feedAddSourceOption = new Option<string>("--source", "-s") { Description = "Feed source URL, a NuGet v3 index endpoint (required)", Required = true };
        var feedAddDefaultOption = new Option<bool>("--default", "-d") { Description = "Mark this feed as the default used for package resolution" };

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

        var feedRemoveNameOption = new Option<string>("--name", "-n") { Description = "Name of the feed to remove (required)", Required = true };

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
        rootCommand.Subcommands.Add(dependenciesCommand);
        rootCommand.Subcommands.Add(compareCommand);
        rootCommand.Subcommands.Add(reproduceCommand);
        rootCommand.Subcommands.Add(feedsCommand);

        return await rootCommand.Parse(args).InvokeAsync();
    }

    private static OutputMode ResolveOutputMode(bool json) =>
        json ? OutputMode.Json : OutputMode.Human;
}
