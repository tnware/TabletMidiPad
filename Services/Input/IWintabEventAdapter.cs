namespace TabletMidiPad.Services.Input;

/// <summary>
/// Adapts raw Wintab backend events into interaction-friendly payloads.
/// </summary>
internal interface IWintabEventAdapter
{
    /// <summary>
    /// Builds an express-key interaction update from raw event data.
    /// </summary>
    WintabExpressKeyUpdate BuildExpressKeyUpdate(
        WintabExpressKeyEventArgs expressKeyEvent,
        IReadOnlyList<ExpressKeyConfig> configuredExpressKeys);

    /// <summary>
    /// Attempts to build a normalized packet interaction update.
    /// </summary>
    bool TryBuildPacketUpdate(
        WintabPacketEventArgs packetEvent,
        double padWidth,
        double padHeight,
        out WintabPacketUpdate update);
}

/// <summary>
/// Express-key translation payload consumed by main-window orchestration.
/// </summary>
internal readonly record struct WintabExpressKeyUpdate(
    int Control,
    bool IsPressed,
    int RequiredExpressKeyCount,
    uint EncodedPenButtons,
    string ActionLabel,
    string BackendLabel,
    string ExpressKeyDiagnostics);

/// <summary>
/// Normalized packet payload consumed by main-window orchestration.
/// </summary>
internal readonly record struct WintabPacketUpdate(
    double XPixel,
    double YPixel,
    double XNorm,
    double YNorm,
    int Pressure127,
    bool IsHovering,
    uint PenButtons,
    string BackendLabel,
    string PacketDiagnostics,
    double CursorOpacity,
    bool IsDown);
