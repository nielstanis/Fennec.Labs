using Spectre.Console;

namespace FennecLabs.Cli;

internal static class StatusRunner
{
    internal static async Task<T> RunAsync<T>(OutputMode outputMode, string message, Func<Task<T>> work)
    {
        if (outputMode != OutputMode.Human)
            return await work();

        T result = default!;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("grey"))
            .StartAsync(message, async _ => { result = await work(); });
        return result;
    }

    internal static async Task RunAsync(OutputMode outputMode, string message, Func<Task> work)
    {
        if (outputMode != OutputMode.Human)
        {
            await work();
            return;
        }

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("grey"))
            .StartAsync(message, async _ => await work());
    }
}
