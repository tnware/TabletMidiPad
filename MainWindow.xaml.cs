using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TabletMidiPad;

public partial class MainWindow : Window
{
    private const string LoopbackAppPortName = "Tablet XY (App)";
    private const string LoopbackDawPortName = "Tablet XY (DAW)";
    private const string LoopbackAssociationId = "{7d6e1f6b-6b0d-4d6c-a1f3-44ddefb7df4d}";
    private const string MidiConsolePath = @"C:\Program Files\Windows MIDI Services\Tools\Console\midi.exe";

    private const int Channel = 0; // 0 = MIDI channel 1
    private const int CcX = 16;
    private const int CcY = 17;
    private const int CcPressure = 18;

    private IntPtr _midiOutHandle;
    private bool _isActive;

    private int _lastX = -1;
    private int _lastY = -1;
    private int _lastPressure = -1;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => InitMidi();
        Closed += (_, _) => ShutdownMidi();
    }

    private void InitMidi()
    {
        try
        {
            RemoveLoopbackPair();
            CreateLoopbackPair();
            OpenMidiOutWithRetry(LoopbackAppPortName);

            StatusText.Text = $"Send to '{LoopbackDawPortName}' in Ableton/Waveform";
            ValueText.Text = "X: -, Y: -, Pressure: -";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Windows MIDI Services init failed";
            ValueText.Text = ex.Message;
            MessageBox.Show(
                ex.ToString(),
                "Windows MIDI Services init failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
    }

    private void ShutdownMidi()
    {
        try
        {
            CloseMidiOut();
            RemoveLoopbackPair();
        }
        catch
        {
        }
    }

    private void Pad_StylusDown(object sender, StylusDownEventArgs e)
    {
        _isActive = true;
        Pad.CaptureStylus();
        HandleStylus(e);
    }

    private void Pad_StylusMove(object sender, StylusEventArgs e)
    {
        if (!_isActive) return;
        HandleStylus(e);
    }

    private void Pad_StylusUp(object sender, StylusEventArgs e)
    {
        if (!_isActive) return;
        HandleStylus(e);
        _isActive = false;
        Pad.ReleaseStylusCapture();
        CursorDot.Visibility = Visibility.Collapsed;
    }

    private void Pad_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        _isActive = true;
        Pad.CaptureMouse();
        HandleMouse(e);
    }

    private void Pad_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isActive || e.LeftButton != MouseButtonState.Pressed) return;
        HandleMouse(e);
    }

    private void Pad_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isActive) return;
        HandleMouse(e);
        _isActive = false;
        Pad.ReleaseMouseCapture();
        CursorDot.Visibility = Visibility.Collapsed;
    }

    private void HandleStylus(StylusEventArgs e)
    {
        var point = e.GetPosition(Pad);
        double width = Pad.ActualWidth;
        double height = Pad.ActualHeight;

        if (width <= 0 || height <= 0) return;

        int x = ToCc(point.X / width);
        int y = ToCc(1.0 - (point.Y / height));

        int pressure = 127;
        var stylusPoints = e.GetStylusPoints(Pad);
        if (stylusPoints.Count > 0)
        {
            var sp = stylusPoints[0];
            if (sp.HasProperty(StylusPointProperties.NormalPressure))
            {
                int raw = sp.GetPropertyValue(StylusPointProperties.NormalPressure);
                int max = sp.Description.GetPropertyInfo(StylusPointProperties.NormalPressure).Maximum;
                pressure = max > 0 ? (int)Math.Round(raw * 127.0 / max) : 127;
            }
        }

        UpdateVisual(point.X, point.Y, x, y, pressure);
        SendCcIfChanged(CcX, x, ref _lastX);
        SendCcIfChanged(CcY, y, ref _lastY);
        SendCcIfChanged(CcPressure, pressure, ref _lastPressure);
    }

    private void HandleMouse(MouseEventArgs e)
    {
        var point = e.GetPosition(Pad);
        double width = Pad.ActualWidth;
        double height = Pad.ActualHeight;

        if (width <= 0 || height <= 0) return;

        int x = ToCc(point.X / width);
        int y = ToCc(1.0 - (point.Y / height));
        int pressure = 127;

        UpdateVisual(point.X, point.Y, x, y, pressure);
        SendCcIfChanged(CcX, x, ref _lastX);
        SendCcIfChanged(CcY, y, ref _lastY);
        SendCcIfChanged(CcPressure, pressure, ref _lastPressure);
    }

    private void UpdateVisual(double xPx, double yPx, int x, int y, int pressure)
    {
        CursorDot.Visibility = Visibility.Visible;
        Canvas.SetLeft(CursorDot, xPx - CursorDot.Width / 2);
        Canvas.SetTop(CursorDot, yPx - CursorDot.Height / 2);
        ValueText.Text = $"X: {x}, Y: {y}, Pressure: {pressure}";
    }

    private void SendCcIfChanged(int cc, int value, ref int lastValue)
    {
        if (_midiOutHandle == IntPtr.Zero) return;
        if (value == lastValue) return;

        value = Math.Clamp(value, 0, 127);

        byte status = (byte)(0xB0 | (Channel & 0x0F));
        byte controller = (byte)(cc & 0x7F);
        byte data = (byte)(value & 0x7F);

        int packedMessage = status | (controller << 8) | (data << 16);
        int result = midiOutShortMsg(_midiOutHandle, packedMessage);
        if (result != 0)
        {
            throw new Win32Exception(result, $"midiOutShortMsg failed with code {result}.");
        }

        lastValue = value;
    }

    private static void CreateLoopbackPair()
    {
        RunMidiConsole(
            $"loopback create --name-a \"{LoopbackAppPortName}\" --name-b \"{LoopbackDawPortName}\" --association-id \"{LoopbackAssociationId}\""
        );
    }

    private static void RemoveLoopbackPair()
    {
        RunMidiConsole(
            $"loopback remove --association-id \"{LoopbackAssociationId}\"",
            allowFailure: true
        );
    }

    private void OpenMidiOutWithRetry(string portName)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            if (TryOpenMidiOut(portName, out _midiOutHandle))
            {
                return;
            }

            Thread.Sleep(100);
        }

        string available = string.Join(", ", EnumerateMidiOutPortNames());
        throw new InvalidOperationException(
            $"Could not open MIDI destination '{portName}'. Available MIDI outputs: {available}."
        );
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

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the Windows MIDI Services console.");

        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (allowFailure || process.ExitCode == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"midi.exe {arguments} failed with exit code {process.ExitCode}. {output} {error}".Trim()
        );
    }

    private static int ToCc(double normalized)
    {
        normalized = Math.Clamp(normalized, 0.0, 1.0);
        return (int)Math.Round(normalized * 127.0);
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
