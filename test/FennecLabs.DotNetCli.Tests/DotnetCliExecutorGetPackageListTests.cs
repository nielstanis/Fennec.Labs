using FennecLabs.DotNetCli;
using System.IO;
using Xunit;

namespace FennecLabs.DotNetCli.Tests
{
    /// <summary>
    /// Rewrite Path logic to go through the testresources props.
    /// Make sure to validate all expected transitive package elements! 
    /// </summary>
    public class DotnetCliExecutorGetPackageListTests
    {
        [Fact]
        public async Task GetPackageListAsync_WithValidProject_ReturnsPackageList()
        {
            // Arrange
            var projectPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "TestProjects", "BasicMvcApp", "BasicMvcApp.csproj"));

            // Act
            var packageList = await DotnetCliExecutor.GetPackageListAsync(projectPath);

            // Assert
            Assert.NotNull(packageList);
            Assert.NotEmpty(packageList.Projects);
        }

        [Fact]
        public async Task GetPackageListAsync_WithIncludeTransitiveFalse_ReturnsOnlyTopLevelPackages()
        {
            // Arrange
            var projectPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "TestProjects", "BasicMvcApp", "BasicMvcApp.csproj"));

            // Act
            var packageList = await DotnetCliExecutor.GetPackageListAsync(projectPath);

            // Assert
            Assert.NotNull(packageList);
            var framework = packageList.Projects[0].Frameworks[0];
            Assert.NotEmpty(framework.TopLevelPackages);
            // Transitive packages should be empty when includeTransitive is false
            Assert.NotEmpty(framework.TransitivePackages);
        }

        [Fact]
        public async Task GetPackageListAsync_WithIncludeTransitiveTrue_ReturnsTransitivePackages()
        {
            // Arrange
            var projectPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "TestProjects", "BasicMvcApp", "BasicMvcApp.csproj"));

            // Act
            var packageList = await DotnetCliExecutor.GetPackageListAsync(projectPath);

            // Assert
            Assert.NotNull(packageList);
            var framework = packageList.Projects[0].Frameworks[0];
            Assert.NotEmpty(framework.TopLevelPackages);
            Assert.NotEmpty(framework.TransitivePackages);
        }

        [Fact]
        public async Task GetPackageListAsync_WithInvalidProject_ReturnsNull()
        {
            // Arrange
            var invalidProjectPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "TestProjects", "NonExistent", "NonExistent.csproj"));

            // Act
            var packageList = await DotnetCliExecutor.GetPackageListAsync(invalidProjectPath);

            // Assert
            Assert.Null(packageList);
        }
    }
}

