using System.Collections.Generic;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Default Wintab input bridge for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class MainWindowWintabInputBridge : IMainWindowWintabInputBridge
{
    private readonly IWintabEventAdapter _wintabEventAdapter;
    private readonly IMainWindowInputBackendCoordinator _inputBackendCoordinator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowWintabInputBridge"/> class.
    /// </summary>
    public MainWindowWintabInputBridge(
        IWintabEventAdapter wintabEventAdapter,
        IMainWindowInputBackendCoordinator inputBackendCoordinator)
    {
        _wintabEventAdapter = wintabEventAdapter;
        _inputBackendCoordinator = inputBackendCoordinator;
    }

    /// <inheritdoc />
    public bool TryBuildPacketBridge(
        WintabPacketEventArgs packet,
        double padWidth,
        double padHeight,
        bool isActiveBefore,
        out WintabPacketBridgeOutcome outcome)
    {
        if (!_wintabEventAdapter.TryBuildPacketUpdate(packet, padWidth, padHeight, out WintabPacketUpdate update))
        {
            outcome = default;
            return false;
        }

        WintabPacketFlowResult flow = _inputBackendCoordinator.BuildWintabPacketFlow(isActiveBefore, update);
        outcome = new WintabPacketBridgeOutcome(
            BackendLabel: update.BackendLabel,
            PenButtons: update.PenButtons,
            PacketDiagnostics: update.PacketDiagnostics,
            Flow: flow);
        return true;
    }

    /// <inheritdoc />
    public WintabExpressKeyBridgeOutcome BuildExpressKeyBridge(
        WintabExpressKeyEventArgs expressKey,
        IReadOnlyList<ExpressKeyConfig> configuredExpressKeys)
    {
        WintabExpressKeyUpdate update = _wintabEventAdapter.BuildExpressKeyUpdate(expressKey, configuredExpressKeys);
        InteractionInput input = new(
            XNorm: 0,
            YNorm: 0,
            Pressure: 0,
            IsDown: false,
            IsHovering: false,
            PenButtons: update.EncodedPenButtons,
            Backend: InteractionBackend.ExpressKey);

        return new WintabExpressKeyBridgeOutcome(
            RequiredExpressKeyCount: update.RequiredExpressKeyCount,
            EngineInput: input,
            ExpressKeyDiagnostics: update.ExpressKeyDiagnostics,
            BackendLabel: update.BackendLabel);
    }
}
