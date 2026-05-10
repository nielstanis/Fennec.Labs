namespace FennecLabs.DotNetCli;

public record DotnetCliResult
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
}

