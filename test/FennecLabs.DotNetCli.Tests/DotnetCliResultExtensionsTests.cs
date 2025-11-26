using FennecLabs.DotNetCli;
using System.Text.Json;
using Xunit;

namespace FennecLabs.DotNetCli.Tests
{
    public class DotnetCliResultExtensionsTests
    {
        [Fact]
        public void DeserializePackageList_WithValidJson_ReturnsPackageListResult()
        {
            // Arrange
            var json = @"{
                ""version"": 1,
                ""parameters"": ""--include-transitive"",
                ""projects"": [
                    {
                        ""path"": ""test.csproj"",
                        ""frameworks"": [
                            {
                                ""framework"": ""net10.0"",
                                ""topLevelPackages"": [
                                    {
                                        ""id"": ""TestPackage"",
                                        ""requestedVersion"": ""1.0.0"",
                                        ""resolvedVersion"": ""1.0.0""
                                    }
                                ],
                                ""transitivePackages"": []
                            }
                        ]
                    }
                ]
            }";

            var result = new DotnetCliResult
            {
                ExitCode = 0,
                StandardOutput = json,
                StandardError = ""
            };

            // Act
            var packageList = result.DeserializePackageList();

            // Assert
            Assert.NotNull(packageList);
            Assert.Equal(1, packageList.Version);
            Assert.Single(packageList.Projects);
            Assert.Equal("test.csproj", packageList.Projects[0].Path);
            Assert.Single(packageList.Projects[0].Frameworks);
            Assert.Equal("net10.0", packageList.Projects[0].Frameworks[0].FrameworkName);
            Assert.Single(packageList.Projects[0].Frameworks[0].TopLevelPackages);
            Assert.Equal("TestPackage", packageList.Projects[0].Frameworks[0].TopLevelPackages[0].Id);
        }

        [Fact]
        public void DeserializePackageList_WithNonZeroExitCode_ReturnsNull()
        {
            // Arrange
            var result = new DotnetCliResult
            {
                ExitCode = 1,
                StandardOutput = "Error occurred",
                StandardError = "Error message"
            };

            // Act
            var packageList = result.DeserializePackageList();

            // Assert
            Assert.Null(packageList);
        }

        [Fact]
        public void DeserializePackageList_WithEmptyOutput_ReturnsNull()
        {
            // Arrange
            var result = new DotnetCliResult
            {
                ExitCode = 0,
                StandardOutput = "",
                StandardError = ""
            };

            // Act
            var packageList = result.DeserializePackageList();

            // Assert
            Assert.Null(packageList);
        }

        [Fact]
        public void DeserializePackageList_WithInvalidJson_ReturnsNull()
        {
            // Arrange
            var result = new DotnetCliResult
            {
                ExitCode = 0,
                StandardOutput = "invalid json {",
                StandardError = ""
            };

            // Act
            var packageList = result.DeserializePackageList();

            // Assert
            Assert.Null(packageList);
        }
    }
}

