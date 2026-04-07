using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace TabletMidiPad.Services;

public sealed class MidiService : IMidiService
{
    private const string MidiConsolePath = @"C:\Program Files\Windows MIDI Services\Tools\Console\midi.exe";
    private readonly Dictionary<string, int> _lastSentValues = new();
    private IntPtr _midiOutHandle;
    private string _associationId = "{7d6e1f6b-6b0d-4d6c-a1f3-44ddefb7df4d}";

    public bool IsConnected => _midiOutHandle != IntPtr.Zero;

    public async Task InitializeAsync(AppConfig config)
    {
        _associationId = config.LoopbackAssociationId;
        await Task.Run(RemoveLoopbackPair);
        await Task.Run(() => CreateLoopbackPair(config));
        await Task.Run(() => OpenMidiOutWithRetry(config.AppPortName));
        _lastSentValues.Clear();
    }

    public void Restart(AppConfig config)
    {
        _associationId = config.LoopbackAssociationId;
        CloseMidiOut();
        RemoveLoopbackPair();
        CreateLoopbackPair(config);
        OpenMidiOutWithRetry(config.AppPortName);
        _lastSentValues.Clear();
    }

    public void SendCcIfChanged(int channel, int cc, int value, bool force = false)
    {
        if (_midiOutHandle == IntPtr.Zero)
        {
            return;
        }

        string key = $"{channel}:{cc}";
        value = Math.Clamp(value, 0, 127);
        if (!force && _lastSentValues.TryGetValue(key, out int lastValue) && lastValue == value)
        {
            return;
        }

        byte status = (byte)(0xB0 | ((channel - 1) & 0x0F));
        int packedMessage = status | ((cc & 0x7F) << 8) | ((value & 0x7F) << 16);
        int result = midiOutShortMsg(_midiOutHandle, packedMessage);
        if (result != 0)
        {
            throw new Win32Exception(result, $"midiOutShortMsg failed with code {result}.");
        }

        _lastSentValues[key] = value;
    }

    public void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false)
    {
        if (_midiOutHandle == IntPtr.Zero)
        {
            return;
        }

        string key = $"note:{channel}:{note}";
        int state = on ? 1 : 0;
        velocity = Math.Clamp(velocity, 0, 127);
        if (!force && _lastSentValues.TryGetValue(key, out int lastState) && lastState == state)
        {
            return;
        }

        byte status = (byte)((on ? 0x90 : 0x80) | ((channel - 1) & 0x0F));
        int packedMessage = status | ((note & 0x7F) << 8) | ((velocity & 0x7F) << 16);
        int result = midiOutShortMsg(_midiOutHandle, packedMessage);
        if (result != 0)
        {
            throw new Win32Exception(result, $"midiOutShortMsg failed with code {result}.");
        }

        _lastSentValues[key] = state;
    }

    public void Shutdown()
    {
        CloseMidiOut();
        RemoveLoopbackPair();
    }

    public void Dispose() => Shutdown();

    private static void CreateLoopbackPair(AppConfig config)
    {
        RunMidiConsole(
            $"loopback create --name-a \"{config.AppPortName}\" --name-b \"{config.DawPortName}\" --association-id \"{config.LoopbackAssociationId}\"");
        Thread.Sleep(300);
    }

    private void RemoveLoopbackPair()
        => RunMidiConsole($"loopback remove --association-id \"{_associationId}\"", allowFailure: true);

    private void OpenMidiOutWithRetry(string portName)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            if (TryOpenMidiOut(portName, out _midiOutHandle))
            {
                return;
            }

            Thread.Sleep(100);
        }

        string available = string.Join(", ", EnumerateMidiOutPortNames());
        throw new InvalidOperationException($"Could not open MIDI destination '{portName}'. Available MIDI outputs: {available}.");
    }

    private static bool TryOpenMidiOut(string targetName, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        int count = midiOutGetNumDevs();
        for (int deviceId = 0; deviceId < count; deviceId++)
        {
            if (midiOutGetDevCaps((UIntPtr)deviceId, out MidiOutCaps caps, (uint)Marshal.SizeOf<MidiOutCaps>()) != 0)
            {
                continue;
            }

            if (!string.Equals(caps.Name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return midiOutOpen(out handle, deviceId, IntPtr.Zero, IntPtr.Zero, 0) == 0;
        }

        return false;
    }

    private void CloseMidiOut()
    {
        if (_midiOutHandle == IntPtr.Zero)
        {
            return;
        }

        midiOutReset(_midiOutHandle);
        midiOutClose(_midiOutHandle);
        _midiOutHandle = IntPtr.Zero;
    }

    private static string[] EnumerateMidiOutPortNames()
    {
        int count = midiOutGetNumDevs();
        var names = new string[count];
        for (int deviceId = 0; deviceId < count; deviceId++)
        {
            names[deviceId] = midiOutGetDevCaps((UIntPtr)deviceId, out MidiOutCaps caps, (uint)Marshal.SizeOf<MidiOutCaps>()) == 0
                ? caps.Name
                : $"<error:{deviceId}>";
        }

        return names.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
    }

    private static void RunMidiConsole(string arguments, bool allowFailure = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = MidiConsolePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Windows MIDI Services console.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (!allowFailure && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"midi.exe {arguments} failed with exit code {process.ExitCode}. {output} {error}".Trim());
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MidiOutCaps
    {
        public ushort ManufacturerId;
        public ushort ProductId;
        public uint DriverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string Name;
        public ushort Technology;
        public ushort Voices;
        public ushort Notes;
        public ushort ChannelMask;
        public uint Support;
    }

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int midiOutGetDevCaps(UIntPtr uDeviceId, out MidiOutCaps caps, uint cbMidiOutCaps);
    [DllImport("winmm.dll")]
    private static extern int midiOutGetNumDevs();
    [DllImport("winmm.dll")]
    private static extern int midiOutOpen(out IntPtr handle, int deviceId, IntPtr callback, IntPtr instance, int flags);
    [DllImport("winmm.dll")]
    private static extern int midiOutShortMsg(IntPtr handle, int message);
    [DllImport("winmm.dll")]
    private static extern int midiOutReset(IntPtr handle);
    [DllImport("winmm.dll")]
    private static extern int midiOutClose(IntPtr handle);
}
