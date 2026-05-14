namespace FennecLabs.Cli.Rendering;

internal static class ColorTheme
{
    internal static string ForScore(decimal score) =>
        score >= 7m ? "green" : score >= 4m ? "yellow" : "red";
}
