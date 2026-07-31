namespace FennecLabs.Cli.Commands.Taint;

/// <summary>
/// Thrown by <see cref="BuildGraphReader"/> when a project's build output DLL
/// cannot be located (e.g. the project has not been built yet).
/// </summary>
internal class BuildOutputNotFoundException : Exception
{
    public BuildOutputNotFoundException(string message) : base(message)
    {
    }

    public BuildOutputNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static BuildOutputNotFoundException ForProject(string csprojPath) =>
        new($"No build output found for '{csprojPath}'. Run `dotnet build` first.");
}
