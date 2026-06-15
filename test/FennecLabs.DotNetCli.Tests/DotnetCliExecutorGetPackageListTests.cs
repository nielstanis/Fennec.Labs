using FennecLabs.TestUtilities;

namespace FennecLabs.DotNetCli.Tests
{
    public class DotnetCliExecutorGetPackageListTests
    {
        private static readonly string BasicMvcAppProject = TestResources.GetTestProjectCsprojPath("BasicMvcApp");

        [Fact]
        public async Task GetPackageListAsync_WithValidProject_ReturnsPackageList()
        {
            var packageList = await DotnetCliExecutor.GetPackageListAsync(BasicMvcAppProject);

            Assert.NotNull(packageList);
            Assert.NotEmpty(packageList.Projects);
        }

        [Fact]
        public async Task GetPackageListAsync_WithIncludeTransitiveFalse_ReturnsOnlyTopLevelPackages()
        {
            var packageList = await DotnetCliExecutor.GetPackageListAsync(BasicMvcAppProject);

            Assert.NotNull(packageList);
            var framework = packageList.Projects[0].Frameworks[0];
            Assert.NotEmpty(framework.TopLevelPackages);
            Assert.NotEmpty(framework.TransitivePackages);
        }

        [Fact]
        public async Task GetPackageListAsync_WithIncludeTransitiveTrue_ReturnsTransitivePackages()
        {
            var packageList = await DotnetCliExecutor.GetPackageListAsync(BasicMvcAppProject);

            Assert.NotNull(packageList);
            var framework = packageList.Projects[0].Frameworks[0];
            Assert.NotEmpty(framework.TopLevelPackages);
            Assert.NotEmpty(framework.TransitivePackages);
        }

        [Fact]
        public async Task GetPackageListAsync_WithInvalidProject_ThrowsInvalidOperationException()
        {
            var invalidProjectPath = "/nonexistent/path/NonExistent.csproj";

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => DotnetCliExecutor.GetPackageListAsync(invalidProjectPath));

            Assert.Contains("NonExistent.csproj", exception.Message);
        }
    }
}
