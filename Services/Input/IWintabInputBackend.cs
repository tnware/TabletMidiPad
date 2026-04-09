namespace TabletMidiPad.Services.Input;

/// <summary>
/// Manages Wintab lifecycle and emits normalized input backend events.
/// </summary>
internal interface IWintabInputBackend : IDisposable
{
    /// <summary>
    /// Raised when a Wintab packet is received and normalized for consumers.
    /// </summary>
    event EventHandler<WintabPacketEventArgs>? PacketReceived;

    /// <summary>
    /// Raised when an express key packet is received.
    /// </summary>
    event EventHandler<WintabExpressKeyEventArgs>? ExpressKeyReceived;

    /// <summary>
    /// Raised when proximity state changes.
    /// </summary>
    event EventHandler<WintabProximityEventArgs>? ProximityChanged;

    /// <summary>
    /// Raised when backend diagnostics change.
    /// </summary>
    event EventHandler<string>? DiagnosticsChanged;

    /// <summary>
    /// Gets whether Wintab input is active.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Gets the detected express-key count for the active context.
    /// </summary>
    int ExpressKeyCount { get; }

    /// <summary>
    /// Gets read-only context information keyed by context handle.
    /// </summary>
    IReadOnlyDictionary<uint, WintabContextInfo> Contexts { get; }

    /// <summary>
    /// Initializes backend state and attempts to open contexts.
    /// </summary>
    void Initialize(int selectedDeviceIndex);

    /// <summary>
    /// Reinitializes open contexts.
    /// </summary>
    void Reinitialize(int selectedDeviceIndex);

    /// <summary>
    /// Shuts down contexts and releases resources.
    /// </summary>
    void Shutdown();

    /// <summary>
    /// Gets current list of tablet devices from Wintab.
    /// </summary>
    IReadOnlyList<WintabTabletDevice> GetAvailableDevices();
}

/// <summary>
/// Normalized Wintab packet data for pad interaction.
/// </summary>
internal sealed record WintabPacketEventArgs(
    uint ContextHandle,
    int DeviceIndex,
    double XNorm,
    double YNorm,
    int Pressure127,
    bool IsHovering,
    uint Buttons,
    string PacketDiagnostics);

/// <summary>
/// Express key packet data.
/// </summary>
internal sealed record WintabExpressKeyEventArgs(
    int Control,
    bool IsPressed,
    string Diagnostics);

/// <summary>
/// Proximity transition data.
/// </summary>
internal sealed record WintabProximityEventArgs(bool Entering, string Diagnostics);

/// <summary>
/// Tablet device item exposed by Wintab backend.
/// </summary>
internal sealed record WintabTabletDevice(int DeviceIndex, string Label);

/// <summary>
/// Open context metadata.
/// </summary>
internal readonly record struct WintabContextInfo(
    int DeviceIndex,
    int OutputMinX,
    int OutputMaxX,
    int OutputMinY,
    int OutputMaxY,
    int PressureMax,
    bool IsIntegrated);
