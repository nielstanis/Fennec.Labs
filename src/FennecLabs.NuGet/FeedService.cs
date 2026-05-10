using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace FennecLabs.NuGet;

public class FeedService
{
    private readonly ConfigurationManager _configManager;

    public FeedService(ConfigurationManager? configManager = null)
    {
        _configManager = configManager ?? new ConfigurationManager();
    }

    public async Task<List<FeedConfiguration>> GetAllFeedsAsync()
    {
        var settings = await _configManager.LoadSettingsAsync();
        return settings.Feeds;
    }

    public async Task AddFeedAsync(string name, string source, bool setAsDefault = false)
    {
        // Validate the feed source by attempting to connect
        await ValidateFeedSourceAsync(source);

        var feed = new FeedConfiguration
        {
            Name = name,
            Source = source,
            IsDefault = setAsDefault
        };

        await _configManager.AddFeedAsync(feed);

        if (setAsDefault)
        {
            await _configManager.SetDefaultFeedAsync(name);
        }
    }

    public async Task SetDefaultFeedAsync(string feedName)
    {
        await _configManager.SetDefaultFeedAsync(feedName);
    }

    private static async Task ValidateFeedSourceAsync(string source, CancellationToken cancellationToken = default)
    {
        try
        {
            var repository = Repository.Factory.GetCoreV3(source);
            var resource = await repository.GetResourceAsync<ServiceIndexResourceV3>(cancellationToken) 
                ?? throw new InvalidOperationException("Unable to access the feed service index");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to validate feed source '{source}': {ex.Message}", ex);
        }
    }

    public async Task<SourceRepository> GetRepositoryAsync(string? feedName = null)
    {
        FeedConfiguration? feed;

        if (!string.IsNullOrEmpty(feedName))
        {
            feed = await _configManager.GetFeedByNameAsync(feedName);
            if (feed == null)
            {
                throw new ArgumentException($"Feed '{feedName}' not found.");
            }
        }
        else
        {
            feed = await _configManager.GetDefaultFeedAsync();
            if (feed == null)
            {
                throw new InvalidOperationException("No default feed configured.");
            }
        }

        return Repository.Factory.GetCoreV3(feed.Source);
    }
}

