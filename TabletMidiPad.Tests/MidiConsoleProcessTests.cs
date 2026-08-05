using System.Diagnostics;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MidiConsoleProcessTests
{
    [Fact]
    public void CreateLoopbackArgumentsKeepConfiguredNamesAsSingleArguments()
    {
        var config = new AppConfig
        {
            AppPortName = "App Port\" --association-id injected",
            DawPortName = "DAW Port",
            LoopbackAssociationId = "{11111111-1111-1111-1111-111111111111}",
        };

        IReadOnlyList<string> arguments = MidiConsoleProcess.CreateLoopbackArguments(config);

        Assert.Equal(
            [
                "loopback",
                "create",
                "--name-a",
                config.AppPortName,
                "--name-b",
                config.DawPortName,
                "--association-id",
                config.LoopbackAssociationId,
            ],
            arguments);
    }

    [Fact]
    public void BuildStartInfoUsesArgumentListInsteadOfCommandLineString()
    {
        ProcessStartInfo startInfo = MidiConsoleProcess.BuildStartInfo(
            @"C:\Program Files\Windows MIDI Services\Tools\Console\midi.exe",
            ["loopback", "create", "--name-a", "name with spaces and \"quotes\""]);

        Assert.Equal(string.Empty, startInfo.Arguments);
        Assert.Equal(
            ["loopback", "create", "--name-a", "name with spaces and \"quotes\""],
            startInfo.ArgumentList);
        Assert.False(startInfo.UseShellExecute);
        Assert.True(startInfo.RedirectStandardOutput);
        Assert.True(startInfo.RedirectStandardError);
    }

    [Fact]
    public void ResolvePathRejectsRelativeEnvironmentOverride()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            MidiConsoleProcess.ResolvePath(
                @"tools\midi.exe",
                registeredPath: null,
                programFilesPath: @"C:\Program Files",
                fileExists: _ => true));

        Assert.Contains("absolute", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolvePathUsesExistingRegisteredExecutable()
    {
        const string registered = @"C:\Program Files\Windows MIDI Services\Tools\Console\midi.exe";

        string result = MidiConsoleProcess.ResolvePath(
            overridePath: null,
            registeredPath: registered,
            programFilesPath: @"C:\Program Files",
            fileExists: path => string.Equals(path, registered, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(registered, result);
    }
}
