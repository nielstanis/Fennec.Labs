using FennecLabs.Cli.Rendering;

namespace FennecLabs.Cli.Tests;

public class ColorThemeTests
{
    [Fact]
    public void ForScore_ReturnsGreen_WhenScoreAboveSeven()
        => Assert.Equal("green", ColorTheme.ForScore(8m));

    [Fact]
    public void ForScore_ReturnsGreen_AtSevenBoundary()
        => Assert.Equal("green", ColorTheme.ForScore(7m));

    [Fact]
    public void ForScore_ReturnsYellow_WhenScoreBetweenFourAndSeven()
        => Assert.Equal("yellow", ColorTheme.ForScore(5.5m));

    [Fact]
    public void ForScore_ReturnsYellow_AtFourBoundary()
        => Assert.Equal("yellow", ColorTheme.ForScore(4m));

    [Fact]
    public void ForScore_ReturnsRed_WhenScoreJustBelowFour()
        => Assert.Equal("red", ColorTheme.ForScore(3.9m));

    [Fact]
    public void ForScore_ReturnsRed_AtZero()
        => Assert.Equal("red", ColorTheme.ForScore(0m));
}
