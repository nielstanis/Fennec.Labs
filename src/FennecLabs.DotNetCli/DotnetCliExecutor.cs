using System.Diagnostics;

namespace FennecLabs.DotNetCli;

public class DotnetCliExecutor
{
    public static async Task<DotnetCliResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };
        
        using var outputStream = new MemoryStream();
        using var errorStream = new MemoryStream();

        process.Start();

        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);
        var errorTask = process.StandardError.BaseStream.CopyToAsync(errorStream, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(outputTask, errorTask);

        outputStream.Position = 0;
        errorStream.Position = 0;

        using var outputReader = new StreamReader(outputStream);
        using var errorReader = new StreamReader(errorStream);

        return new DotnetCliResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = await outputReader.ReadToEndAsync(),
            StandardError = await errorReader.ReadToEndAsync()
        };
    }

    public static Task<PackageListResult?> GetPackageListAsync(string projectPath, CancellationToken cancellationToken = default) =>
        GetPackageListInternalAsync($"list \"{projectPath}\" package --include-transitive --format json", cancellationToken);

    public static Task<PackageListResult?> GetPackageListAsync(CancellationToken cancellationToken = default) =>
        GetPackageListInternalAsync("list package --include-transitive --format json", cancellationToken);

    private static async Task<PackageListResult?> GetPackageListInternalAsync(string arguments, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(arguments, cancellationToken);
        if (result.ExitCode != 0 && !string.IsNullOrWhiteSpace(result.StandardError))
        {
            throw new InvalidOperationException(result.StandardError.Trim());
        }

        return result.DeserializePackageList();
    }
}

