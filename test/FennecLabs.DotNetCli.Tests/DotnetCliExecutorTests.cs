using FennecLabs.DotNetCli;
using Xunit;

namespace FennecLabs.DotNetCli.Tests
{
    public class DotnetCliExecutorTests
    {
        [Fact]
        public async Task ExecuteAsync_WithValidCommand_ReturnsResult()
        {
            // Arrange
            var arguments = "--version";

            // Act
            var result = await DotnetCliExecutor.ExecuteAsync(arguments);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(0, result.ExitCode);
            Assert.NotEmpty(result.StandardOutput);
        }

        [Fact]
        public async Task ExecuteAsync_WithInvalidCommand_ReturnsNonZeroExitCode()
        {
            // Arrange
            var arguments = "invalid-command-that-does-not-exist";

            // Act
            var result = await DotnetCliExecutor.ExecuteAsync(arguments);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(0, result.ExitCode);
        }

        [Fact]
        public async Task ExecuteAsync_WithHelpCommand_ReturnsOutput()
        {
            // Arrange
            var arguments = "--help";

            // Act
            var result = await DotnetCliExecutor.ExecuteAsync(arguments);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.StandardOutput.Contains("Usage:") || result.StandardOutput.Contains("dotnet"));
        }
    }
}

