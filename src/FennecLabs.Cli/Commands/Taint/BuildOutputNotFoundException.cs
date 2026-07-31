namespace FennecLabs.Cli.Commands.Taint;

/// <summary>
/// Raised when a project/solution input resolves to zero build output DLLs (e.g. the project
/// has not been built yet). Carries an actionable hint for the user.
/// </summary>
internal sealed class BuildOutputNotFoundException : Exception
{
    public BuildOutputNotFoundException(string inputPath)
        : base($"No build output found for '{inputPath}'. Run 'dotnet build' first.")
    {
    }
}
