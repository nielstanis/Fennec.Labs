using FennecLabs.TestUtilities;

namespace FennecLabs.DotNetCli.Tests
{
    public class PackageListIntegrationTests
    {
        private static readonly string BasicMvcAppProject = TestResources.GetTestProjectCsprojPath("BasicMvcApp");

        [Fact]
        public async Task GetPackageList_FromBasicMvcApp_ReturnsTransitivePackages()
        {
            var packageList = await DotnetCliExecutor.GetPackageListAsync(BasicMvcAppProject);

            Assert.NotNull(packageList);
            Assert.NotEmpty(packageList.Projects);

            var project = packageList.Projects[0];
            Assert.Equal("BasicMvcApp.csproj", Path.GetFileName(project.Path));
            Assert.NotEmpty(project.Frameworks);

            var framework = project.Frameworks[0];
            Assert.NotEmpty(framework.TopLevelPackages);
            Assert.NotEmpty(framework.TransitivePackages);
            Assert.True(framework.TransitivePackages.Count > 0,
                "Expected transitive packages to be present");
        }

        [Fact]
        public async Task GetPackageList_FromBasicMvcApp_HasTopLevelPackages()
        {
            var packageList = await DotnetCliExecutor.GetPackageListAsync(BasicMvcAppProject);

            Assert.NotNull(packageList);
            var framework = packageList.Projects[0].Frameworks[0];

            Assert.True(framework.TopLevelPackages.Count > 0,
                "Expected top-level packages to be present");

            foreach (var package in framework.TopLevelPackages)
            {
                Assert.NotNull(package.Id);
                Assert.NotEmpty(package.Id);
            }
        }

        [Fact]
        public async Task GetPackageList_FromBasicMvcApp_TransitivePackagesHaveRequiredFields()
        {
            var packageList = await DotnetCliExecutor.GetPackageListAsync(BasicMvcAppProject);

            Assert.NotNull(packageList);
            var framework = packageList.Projects[0].Frameworks[0];

            foreach (var package in framework.TransitivePackages)
            {
                Assert.NotNull(package.Id);
                Assert.NotEmpty(package.Id);
                Assert.NotNull(package.ResolvedVersion);
            }
        }
    }
}
