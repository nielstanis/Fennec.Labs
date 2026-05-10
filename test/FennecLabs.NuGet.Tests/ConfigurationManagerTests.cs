using FennecLabs.NuGet;
using Xunit;

namespace FennecLabs.NuGet.Tests;

public class ConfigurationManagerTests
{
    [Fact]
    public async Task LoadSettingsAsync_CreatesDefaultSettings_WhenNoConfigExists()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var configManager = new ConfigurationManager(tempDir);

        try
        {
            // Act
            var settings = await configManager.LoadSettingsAsync();

            // Assert
            Assert.NotNull(settings);
            Assert.NotNull(settings.Feeds);
            Assert.NotEmpty(settings.Feeds);
            Assert.Contains(settings.Feeds, f => f.Name == "nuget.org");
            Assert.Equal("nuget.org", settings.DefaultFeed);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveSettingsAsync_AndLoadSettingsAsync_PersistsSettings()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var configManager = new ConfigurationManager(tempDir);

        try
        {
            var settings = await configManager.LoadSettingsAsync();
            settings.DefaultFeed = "nuget.org";

            // Act
            await configManager.SaveSettingsAsync(settings);
            var loadedSettings = await configManager.LoadSettingsAsync();

            // Assert
            Assert.NotNull(loadedSettings);
            Assert.Equal(settings.DefaultFeed, loadedSettings.DefaultFeed);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GetDefaultFeedAsync_ReturnsDefaultFeed()
    {
        // Arrange
        var configManager = new ConfigurationManager();

        // Act
        var defaultFeed = await configManager.GetDefaultFeedAsync();

        // Assert
        Assert.NotNull(defaultFeed);
        Assert.Equal("nuget.org", defaultFeed.Name);
    }

    [Fact]
    public async Task GetFeedByNameAsync_WithValidName_ReturnsFeed()
    {
        // Arrange
        var configManager = new ConfigurationManager();

        // Act
        var feed = await configManager.GetFeedByNameAsync("nuget.org");

        // Assert
        Assert.NotNull(feed);
        Assert.Equal("nuget.org", feed.Name);
    }

    [Fact]
    public async Task GetFeedByNameAsync_WithInvalidName_ReturnsNull()
    {
        // Arrange
        var configManager = new ConfigurationManager();

        // Act
        var feed = await configManager.GetFeedByNameAsync("nonexistent-feed");

        // Assert
        Assert.Null(feed);
    }

    [Fact]
    public async Task AddFeedAsync_AddsNewFeed()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var configManager = new ConfigurationManager(tempDir);
        var newFeed = new FeedConfiguration
        {
            Name = "test-feed",
            Source = "https://api.nuget.org/v3/index.json",
            IsDefault = false
        };

        try
        {
            // Act
            await configManager.AddFeedAsync(newFeed);
            var feed = await configManager.GetFeedByNameAsync("test-feed");

            // Assert
            Assert.NotNull(feed);
            Assert.Equal("test-feed", feed.Name);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SetDefaultFeedAsync_SetsDefaultFeed()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var configManager = new ConfigurationManager(tempDir);
        var newFeed = new FeedConfiguration
        {
            Name = "test-default",
            Source = "https://api.nuget.org/v3/index.json",
            IsDefault = false
        };

        try
        {
            await configManager.AddFeedAsync(newFeed);

            // Act
            await configManager.SetDefaultFeedAsync("test-default");
            var defaultFeed = await configManager.GetDefaultFeedAsync();

            // Assert
            Assert.NotNull(defaultFeed);
            Assert.Equal("test-default", defaultFeed.Name);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RemoveFeedAsync_RemovesFeed()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var configManager = new ConfigurationManager(tempDir);
        var newFeed = new FeedConfiguration
        {
            Name = "test-remove",
            Source = "https://api.nuget.org/v3/index.json",
            IsDefault = false
        };

        try
        {
            await configManager.AddFeedAsync(newFeed);

            // Act
            await configManager.RemoveFeedAsync("test-remove");
            var feed = await configManager.GetFeedByNameAsync("test-remove");

            // Assert
            Assert.Null(feed);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }
}

