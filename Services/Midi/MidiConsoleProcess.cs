using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace TabletMidiPad.Services.Midi;

/// <summary>
/// Builds and launches Windows MIDI Services console commands without command-line string parsing.
/// </summary>
internal static class MidiConsoleProcess
{
    private const string MidiConsoleEnvOverride = "TABLETMIDIPAD_MIDI_EXE_PATH";
    private const string MidiServicesRegistryKey = @"SOFTWARE\Microsoft\Windows MIDI Services";
    private const string MidiConsoleRegistryValue = "MidiConsole";
    private const string MidiConsoleRelativePath = @"Windows MIDI Services\Tools\Console\midi.exe";

    public static IReadOnlyList<string> CreateLoopbackArguments(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return
        [
            "loopback",
            "create",
            "--name-a",
            config.AppPortName,
            "--name-b",
            config.DawPortName,
            "--association-id",
            config.LoopbackAssociationId,
        ];
    }

    public static IReadOnlyList<string> RemoveLoopbackArguments(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return ["loopback", "remove", "--association-id", config.LoopbackAssociationId];
    }

    public static ProcessStartInfo BuildStartInfo(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    public static string ResolveInstalledPath()
    {
        string? registeredPath = null;
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(MidiServicesRegistryKey);
            registeredPath = key?.GetValue(MidiConsoleRegistryValue) as string;
        }
        catch
        {
            // Continue to the documented Program Files location.
        }

        return ResolvePath(
            Environment.GetEnvironmentVariable(MidiConsoleEnvOverride),
            registeredPath,
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            File.Exists);
    }

    internal static string ResolvePath(
        string? overridePath,
        string? registeredPath,
        string programFilesPath,
        Func<string, bool> fileExists)
    {
        ArgumentNullException.ThrowIfNull(fileExists);

        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return ValidateCandidate(overridePath, "environment override", fileExists);
        }

        if (!string.IsNullOrWhiteSpace(registeredPath))
        {
            string registeredCandidate = Directory.Exists(registeredPath)
                ? Path.Combine(registeredPath, "midi.exe")
                : registeredPath;
            if (TryValidateCandidate(registeredCandidate, fileExists, out string? validRegisteredPath))
            {
                return validRegisteredPath;
            }
        }

        if (!string.IsNullOrWhiteSpace(programFilesPath))
        {
            string defaultPath = Path.Combine(programFilesPath, MidiConsoleRelativePath);
            if (TryValidateCandidate(defaultPath, fileExists, out string? validDefaultPath))
            {
                return validDefaultPath;
            }
        }

        throw new InvalidOperationException(
            "Could not locate the trusted Windows MIDI Services console. Install Windows MIDI Services Tools, or set TABLETMIDIPAD_MIDI_EXE_PATH to the absolute path of midi.exe.");
    }

    private static string ValidateCandidate(string candidate, string source, Func<string, bool> fileExists)
    {
        if (!TryValidateCandidate(candidate, fileExists, out string? validPath))
        {
            throw new InvalidOperationException(
                $"The Windows MIDI Services {source} must be an existing absolute path to midi.exe.");
        }

        return validPath;
    }

    private static bool TryValidateCandidate(string candidate, Func<string, bool> fileExists, out string validPath)
    {
        validPath = string.Empty;
        string expanded = Environment.ExpandEnvironmentVariables(candidate.Trim().Trim('"'));
        if (!Path.IsPathFullyQualified(expanded) ||
            !string.Equals(Path.GetFileName(expanded), "midi.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(expanded);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        if (!fileExists(fullPath))
        {
            return false;
        }

        validPath = fullPath;
        return true;
    }
}
