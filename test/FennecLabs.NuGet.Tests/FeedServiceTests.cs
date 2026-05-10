using FennecLabs.NuGet;
using NuGet.Protocol.Core.Types;
using Xunit;

namespace FennecLabs.NuGet.Tests;

public class FeedServiceTests
{
    [Fact]
    public async Task GetAllFeedsAsync_ReturnsDefaultFeed()
    {
        // Arrange
        var feedService = new FeedService();

        // Act
        var feeds = await feedService.GetAllFeedsAsync();

        // Assert
        Assert.NotNull(feeds);
        Assert.NotEmpty(feeds);
        Assert.Contains(feeds, f => f.Name == "nuget.org");
    }

    [Fact]
    public async Task GetRepositoryAsync_WithDefaultFeed_ReturnsRepository()
    {
        // Arrange
        var feedService = new FeedService();

        // Act
        var repository = await feedService.GetRepositoryAsync();

        // Assert
        Assert.NotNull(repository);
        Assert.NotNull(repository.PackageSource);
    }

    [Fact]
    public async Task GetRepositoryAsync_WithSpecificFeed_ReturnsRepository()
    {
        // Arrange
        var feedService = new FeedService();

        // Act
        var repository = await feedService.GetRepositoryAsync("nuget.org");

        // Assert
        Assert.NotNull(repository);
        Assert.NotNull(repository.PackageSource);
        Assert.Contains("nuget.org", repository.PackageSource.Source);
    }

    [Fact]
    public async Task AddFeedAsync_WithValidSource_AddsFeed()
    {
        // Arrange
        var feedService = new FeedService();
        var feedName = "test-feed";
        var feedSource = "https://api.nuget.org/v3/index.json";

        try
        {
            // Act
            await feedService.AddFeedAsync(feedName, feedSource);

            // Assert
            var feeds = await feedService.GetAllFeedsAsync();
            Assert.Contains(feeds, f => f.Name == feedName);
        }
        finally
        {
            // Cleanup - remove the test feed
            var configManager = new ConfigurationManager();
            try
            {
                await configManager.RemoveFeedAsync(feedName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task SetDefaultFeedAsync_WithValidFeed_SetsAsDefault()
    {
        // Arrange
        var feedService = new FeedService();
        var feedName = "nuget.org";

        // Act
        await feedService.SetDefaultFeedAsync(feedName);

        // Assert
        var configManager = new ConfigurationManager();
        var defaultFeed = await configManager.GetDefaultFeedAsync();
        Assert.NotNull(defaultFeed);
        Assert.Equal(feedName, defaultFeed.Name);
    }
}

