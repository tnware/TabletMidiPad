using System.Collections.Generic;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Builds structured outcomes for Wintab packet and express-key paths so the view applies side effects in order.
/// </summary>
internal interface IMainWindowWintabInputBridge
{
    /// <summary>
    /// Returns false when the packet should be ignored (e.g. invalid pad dimensions).
    /// </summary>
    bool TryBuildPacketBridge(
        WintabPacketEventArgs packet,
        double padWidth,
        double padHeight,
        bool isActiveBefore,
        out WintabPacketBridgeOutcome outcome);

    /// <summary>
    /// Always succeeds for valid express-key events.
    /// </summary>
    WintabExpressKeyBridgeOutcome BuildExpressKeyBridge(
        WintabExpressKeyEventArgs expressKey,
        IReadOnlyList<ExpressKeyConfig> configuredExpressKeys);
}

/// <summary>
/// Wintab packet path: diagnostics + flow for release/tick (matches legacy MainWindow ordering inputs).
/// </summary>
internal readonly record struct WintabPacketBridgeOutcome(
    string BackendLabel,
    uint PenButtons,
    string PacketDiagnostics,
    WintabPacketFlowResult Flow);

/// <summary>
/// Express-key path: engine tick input and status strings for VM/backend display.
/// </summary>
internal readonly record struct WintabExpressKeyBridgeOutcome(
    int RequiredExpressKeyCount,
    InteractionInput EngineInput,
    string ExpressKeyDiagnostics,
    string BackendLabel);
