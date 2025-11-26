using FennecLabs.DotNetCli;
using System.IO;
using Xunit;

namespace FennecLabs.DotNetCli.Tests
{
    public class PackageListIntegrationTests
    {
        [Fact]
        public async Task GetPackageList_FromBasicMvcApp_ReturnsTransitivePackages()
        {
            // Arrange
            var projectPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "TestProjects", "BasicMvcApp", "BasicMvcApp.csproj"));

            // Act
            var packageList = await DotnetCliExecutor.GetPackageListAsync(projectPath, includeTransitive: true);

            // Assert
            Assert.NotNull(packageList);
            Assert.NotEmpty(packageList.Projects);
            
            var project = packageList.Projects[0];
            Assert.Equal("BasicMvcApp.csproj", Path.GetFileName(project.Path));
            Assert.NotEmpty(project.Frameworks);
            
            var framework = project.Frameworks[0];
            Assert.NotEmpty(framework.TopLevelPackages);
            Assert.NotEmpty(framework.TransitivePackages);
            
            // Verify we have some transitive packages
            Assert.True(framework.TransitivePackages.Count > 0, 
                "Expected transitive packages to be present");
        }

        [Fact]
        public async Task GetPackageList_FromBasicMvcApp_HasTopLevelPackages()
        {
            // Arrange
            var projectPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "TestProjects", "BasicMvcApp", "BasicMvcApp.csproj"));

            // Act
            var packageList = await DotnetCliExecutor.GetPackageListAsync(projectPath, includeTransitive: true);

            // Assert
            Assert.NotNull(packageList);
            var framework = packageList.Projects[0].Frameworks[0];
            
            // Verify top-level packages exist
            Assert.True(framework.TopLevelPackages.Count > 0, 
                "Expected top-level packages to be present");
            
            // Verify package structure
            foreach (var package in framework.TopLevelPackages)
            {
                Assert.NotNull(package.Id);
                Assert.NotEmpty(package.Id);
            }
        }

        [Fact]
        public async Task GetPackageList_FromBasicMvcApp_TransitivePackagesHaveRequiredFields()
        {
            // Arrange
            var projectPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "TestProjects", "BasicMvcApp", "BasicMvcApp.csproj"));

            // Act
            var packageList = await DotnetCliExecutor.GetPackageListAsync(projectPath, includeTransitive: true);

            // Assert
            Assert.NotNull(packageList);
            var framework = packageList.Projects[0].Frameworks[0];
            
            // Verify transitive packages have required fields
            foreach (var package in framework.TransitivePackages)
            {
                Assert.NotNull(package.Id);
                Assert.NotEmpty(package.Id);
                // ResolvedVersion should be present for transitive packages
                Assert.NotNull(package.ResolvedVersion);
            }
        }
    }
}

