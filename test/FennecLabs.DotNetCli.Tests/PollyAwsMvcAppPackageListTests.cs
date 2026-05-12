using FennecLabs.TestUtilities;

namespace FennecLabs.DotNetCli.Tests
{
    public class PollyAwsMvcAppPackageListTests
    {
        private static readonly string PollyAwsMvcAppProject = TestResources.GetTestProjectCsprojPath("PollyAwsMvcApp");

        [Fact]
        public async Task GetPackageList_FromPollyAwsMvcApp_ReturnsExpectedTopLevelPackages()
        {
            var packageList = await DotnetCliExecutor.GetPackageListAsync(PollyAwsMvcAppProject);

            Assert.NotNull(packageList);
            Assert.NotEmpty(packageList.Projects);

            var project = packageList.Projects[0];
            Assert.Equal("PollyAwsMvcApp.csproj", Path.GetFileName(project.Path));

            var framework = project.Frameworks[0];
            var topLevelIds = framework.TopLevelPackages.Select(p => p.Id).ToList();

            Assert.Contains("Polly", topLevelIds);
            Assert.Contains("AWSSDK.Core", topLevelIds);
        }

        [Fact]
        public async Task GetPackageList_FromPollyAwsMvcApp_ReturnsAllTransitivePackages()
        {
            var packageList = await DotnetCliExecutor.GetPackageListAsync(PollyAwsMvcAppProject);

            Assert.NotNull(packageList);
            var framework = packageList.Projects[0].Frameworks[0];
            var transitiveIds = framework.TransitivePackages.Select(p => p.Id).ToList();

            // Polly 8.x pulls in Polly.Core as its only transitive dependency
            Assert.Contains("Polly.Core", transitiveIds);

            // Exact count guards against silent truncation in the parser
            Assert.Single(framework.TransitivePackages);
        }

        [Fact]
        public async Task GetPackageList_FromPollyAwsMvcApp_AllPackagesHaveResolvedVersion()
        {
            var packageList = await DotnetCliExecutor.GetPackageListAsync(PollyAwsMvcAppProject);

            Assert.NotNull(packageList);
            var framework = packageList.Projects[0].Frameworks[0];

            foreach (var package in framework.TopLevelPackages)
            {
                Assert.NotNull(package.Id);
                Assert.NotEmpty(package.Id);
            }

            foreach (var package in framework.TransitivePackages)
            {
                Assert.NotNull(package.Id);
                Assert.NotEmpty(package.Id);
                Assert.NotNull(package.ResolvedVersion);
                Assert.NotEmpty(package.ResolvedVersion);
            }
        }
    }
}
