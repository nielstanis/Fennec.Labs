using System.CommandLine;
using System.CommandLine.Help;
using FennecLabs.Cli;

namespace FennecLabs.Cli.Tests;

public class HelpExamplesTests
{
    private static (RootCommand Root, Command Child) BuildRoot()
    {
        var root = new RootCommand("test root");
        root.Options.Add(new HelpOption());
        var child = new Command("child", "child command");
        root.Subcommands.Add(child);
        return (root, child);
    }

    private static string CaptureHelp(RootCommand root, params string[] args)
    {
        var writer = new StringWriter();
        var config = new InvocationConfiguration { Output = writer };
        root.Parse(args).Invoke(config);
        return writer.ToString();
    }

    [Fact]
    public void Install_AppendsExamplesSection_ForRootCommand()
    {
        var (root, _) = BuildRoot();
        root.WithExamples("fennec compare --nuget Newtonsoft.Json");
        HelpExamples.Install(root);

        var output = CaptureHelp(root, "--help");

        Assert.Contains("Examples:", output);
        Assert.Contains("fennec compare --nuget Newtonsoft.Json", output);
    }

    [Fact]
    public void Install_AppendsExamplesSection_ForSubcommand()
    {
        var (root, child) = BuildRoot();
        child.WithExamples("fennec child --flag");
        HelpExamples.Install(root);

        var output = CaptureHelp(root, "child", "--help");

        Assert.Contains("Examples:", output);
        Assert.Contains("fennec child --flag", output);
    }

    [Fact]
    public void Install_OmitsExamplesSection_WhenCommandHasNoExamples()
    {
        var (root, _) = BuildRoot();
        HelpExamples.Install(root);

        var output = CaptureHelp(root, "child", "--help");

        Assert.DoesNotContain("Examples:", output);
    }

    [Fact]
    public void Install_PreservesDefaultHelpOutput()
    {
        var (root, _) = BuildRoot();
        root.WithExamples("fennec something");
        HelpExamples.Install(root);

        var output = CaptureHelp(root, "--help");

        Assert.Contains("test root", output);
        Assert.Contains("Usage:", output);
    }

    [Fact]
    public void WithExamples_ReturnsSameCommandInstance()
    {
        var command = new Command("x", "x");
        Assert.Same(command, command.WithExamples("example"));
    }
}
