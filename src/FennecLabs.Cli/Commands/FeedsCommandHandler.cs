using FennecLabs.NuGet;

namespace FennecLabs.Cli.Commands;

internal class FeedsCommandHandler
{
    public async Task<int> ExecuteListAsync()
    {
        var feedService = new FeedService();
        var feeds = await feedService.GetAllFeedsAsync();

        if (feeds.Count == 0)
        {
            Console.WriteLine("No feeds configured.");
            return 0;
        }

        Console.WriteLine("Configured feeds:");
        foreach (var feed in feeds)
        {
            var marker = feed.IsDefault ? " (default)" : "";
            Console.WriteLine($"  {feed.Name}{marker} — {feed.Source}");
        }
        return 0;
    }

    public async Task<int> ExecuteAddAsync(string? name, string? source, bool setDefault)
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
            Console.WriteLine($"Feed '{name}' added.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error adding feed: {ex.Message}");
            return 1;
        }
    }

    public async Task<int> ExecuteRemoveAsync(string? name)
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
            Console.WriteLine($"Feed '{name}' removed.");
            return 0;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
