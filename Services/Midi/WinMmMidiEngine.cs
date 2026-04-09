using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TabletMidiPad.Services.Midi;

/// <summary>
/// Windows MIDI engine backed by winmm and Windows MIDI Services console.
/// </summary>
internal sealed class WinMmMidiEngine : IMidiEngine
{
    private const string MidiConsoleEnvOverride = "TABLETMIDIPAD_MIDI_EXE_PATH";
    private readonly Dictionary<string, int> _lastSentValues = new();
    private readonly object _sync = new();
    private IntPtr _midiOutHandle;

    public bool IsReady
    {
        get
        {
            lock (_sync)
            {
                return _midiOutHandle != IntPtr.Zero;
            }
        }
    }

    public void Initialize(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        lock (_sync)
        {
            CloseMidiOut();
            RemoveLoopbackPair(config);
            CreateLoopbackPair(config);
            _midiOutHandle = OpenMidiOutWithRetry(config.AppPortName);
            _lastSentValues.Clear();
        }
    }

    public async Task InitializeAsync(AppConfig config, Action<string>? onProgress = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        await Task.Run(() =>
        {
            lock (_sync)
            {
                onProgress?.Invoke("Closing existing MIDI connection...");
                CloseMidiOut();

                onProgress?.Invoke("Removing stale MIDI pair...");
                RemoveLoopbackPair(config);

                onProgress?.Invoke("Creating MIDI loopback...");
                CreateLoopbackPair(config);

                onProgress?.Invoke($"Opening '{config.AppPortName}'...");
                _midiOutHandle = OpenMidiOutWithRetry(config.AppPortName);
                _lastSentValues.Clear();
            }
        });
    }

    public void Restart(AppConfig config)
    {
        Initialize(config);
    }

    public Task RestartAsync(AppConfig config, Action<string>? onProgress = null)
    {
        return InitializeAsync(config, onProgress);
    }

    public void Shutdown(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        try
        {
            lock (_sync)
            {
                CloseMidiOut();
                RemoveLoopbackPair(config);
            }
        }
        catch
        {
        }
    }

    public void SendCcIfChanged(int channel, int cc, int value, bool force = false)
    {
        lock (_sync)
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
            byte controller = (byte)(cc & 0x7F);
            byte data = (byte)(value & 0x7F);

            int packedMessage = status | (controller << 8) | (data << 16);
            int result = midiOutShortMsg(_midiOutHandle, packedMessage);
            if (result != 0)
            {
                throw new Win32Exception(result, $"midiOutShortMsg failed with code {result}.");
            }

            _lastSentValues[key] = value;
        }
    }

    public void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false)
    {
        lock (_sync)
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
            byte noteByte = (byte)(note & 0x7F);
            byte velocityByte = (byte)(velocity & 0x7F);

            int packedMessage = status | (noteByte << 8) | (velocityByte << 16);
            int result = midiOutShortMsg(_midiOutHandle, packedMessage);
            if (result != 0)
            {
                throw new Win32Exception(result, $"midiOutShortMsg failed with code {result}.");
            }

            _lastSentValues[key] = state;
        }
    }

    public void ClearCachedValues()
    {
        lock (_sync)
        {
            _lastSentValues.Clear();
        }
    }

    private static void CreateLoopbackPair(AppConfig config)
    {
        RunMidiConsole(
            $"loopback create --name-a \"{config.AppPortName}\" --name-b \"{config.DawPortName}\" --association-id \"{config.LoopbackAssociationId}\"");
        Thread.Sleep(300);
    }

    private static void RemoveLoopbackPair(AppConfig config)
    {
        RunMidiConsole(
            $"loopback remove --association-id \"{config.LoopbackAssociationId}\"",
            allowFailure: true);
    }

    private static IntPtr OpenMidiOutWithRetry(string portName)
    {
        for (int attempt = 0; attempt < 60; attempt++)
        {
            if (TryOpenMidiOut(portName, out IntPtr handle))
            {
                return handle;
            }

            Thread.Sleep(100);
        }

        string available = string.Join(", ", EnumerateMidiOutPortNames());
        throw new InvalidOperationException(
            $"Could not open MIDI destination '{portName}'. Available MIDI outputs: {available}.");
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

            int result = midiOutOpen(out handle, deviceId, IntPtr.Zero, IntPtr.Zero, 0);
            return result == 0;
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

        return [.. names.Where(name => !string.IsNullOrWhiteSpace(name))];
    }

    private static void RunMidiConsole(string arguments, bool allowFailure = false)
    {
        string midiConsolePath = ResolveMidiConsolePath();
        var startInfo = new ProcessStartInfo
        {
            FileName = midiConsolePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Could not find 'midi.exe'. Ensure Windows MIDI Services is installed and 'midi.exe' is in PATH, or set TABLETMIDIPAD_MIDI_EXE_PATH.",
                ex);
        }

        if (process is null)
        {
            throw new InvalidOperationException("Could not start the Windows MIDI Services console.");
        }

        using (process)
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (allowFailure || process.ExitCode == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                $"midi.exe {arguments} failed with exit code {process.ExitCode}. {output} {error}".Trim());
        }
    }

    private static string ResolveMidiConsolePath()
    {
        string? overridePath = Environment.GetEnvironmentVariable(MidiConsoleEnvOverride);
        return string.IsNullOrWhiteSpace(overridePath) ? "midi.exe" : overridePath;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
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

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
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
