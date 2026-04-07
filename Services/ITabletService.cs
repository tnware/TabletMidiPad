using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TabletMidiPad.Services;

public interface ITabletService : IDisposable
{
    bool UseWintab { get; }
    string LastInputBackend { get; }
    string WintabDiagnostics { get; }
    string ExpressKeyDiagnostics { get; }
    int ExpressKeyCount { get; }
    string WacomServiceStatus { get; }

    event Action<string, string>? LoadingStateChanged;
    event Action<double>? LoadingProgressChanged;
    event Action<int, double, double, int, int, uint, string>? WintabPacketReceived;
    event Action<int, bool>? ExpressKeyPacketReceived;
    event Action? ProximityLost;

    void Initialize(string backendMode, IntPtr messageWindowHandle);
    void Shutdown();
    void Reinitialize();
    string GetWacomServiceStatus();
    Task StartWacomServiceAsync();
    IntPtr HandleWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled);
}
