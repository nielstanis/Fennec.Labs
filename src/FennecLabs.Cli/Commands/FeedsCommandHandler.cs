using System.Text.Json;
using FennecLabs.NuGet;
using Spectre.Console;

namespace FennecLabs.Cli.Commands;

internal class FeedsCommandHandler
{
    public async Task<int> ExecuteListAsync(OutputMode outputMode)
    {
        var feedService = new FeedService();
        var feeds = await feedService.GetAllFeedsAsync();

        if (outputMode == OutputMode.Json)
        {
            var output = new
            {
                feeds = feeds.Select(f => new
                {
                    name = f.Name,
                    source = f.Source,
                    isDefault = f.IsDefault,
                }),
            };
            Console.WriteLine(JsonSerializer.Serialize(output, Json.Options));
            return 0;
        }

        if (feeds.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No feeds configured.[/]");
            return 0;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("[bold]Name[/]"))
            .AddColumn(new TableColumn("[bold]Source[/]"))
            .AddColumn(new TableColumn("[bold]Default[/]").Centered());

        foreach (var feed in feeds)
        {
            table.AddRow(
                Markup.Escape(feed.Name),
                Markup.Escape(feed.Source),
                feed.IsDefault ? "[green]✓[/]" : "");
        }

        AnsiConsole.Write(table);
        return 0;
    }

    public async Task<int> ExecuteAddAsync(string? name, string? source, bool setDefault, OutputMode outputMode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("--name is required.");
            return 1;
        }
        if (string.IsNullOrWhiteSpace(source))
        {
            Console.Error.WriteLine("--source is required.");
            return 1;
        }

        try
        {
            var feedService = new FeedService();
            await feedService.AddFeedAsync(name, source, setDefault);

            if (outputMode == OutputMode.Json)
                Console.WriteLine(JsonSerializer.Serialize(new { status = $"Feed '{name}' added." }, Json.Options));
            else
                AnsiConsole.MarkupLine($"[green]✓[/] Feed '[bold]{Markup.Escape(name)}[/]' added.");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error adding feed: {ex.Message}");
            return 1;
        }
    }

    public async Task<int> ExecuteRemoveAsync(string? name, OutputMode outputMode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("--name is required.");
            return 1;
        }

        try
        {
            var configManager = new ConfigurationManager();
            await configManager.RemoveFeedAsync(name);

            if (outputMode == OutputMode.Json)
                Console.WriteLine(JsonSerializer.Serialize(new { status = $"Feed '{name}' removed." }, Json.Options));
            else
                AnsiConsole.MarkupLine($"[green]✓[/] Feed '[bold]{Markup.Escape(name)}[/]' removed.");

            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
