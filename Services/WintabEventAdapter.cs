using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Default adapter that translates Wintab backend events into interaction-engine inputs and UI diagnostics payloads.
/// </summary>
internal sealed class WintabEventAdapter : IWintabEventAdapter
{
    /// <inheritdoc />
    public WintabExpressKeyUpdate BuildExpressKeyUpdate(
        WintabExpressKeyEventArgs expressKeyEvent,
        IReadOnlyList<ExpressKeyConfig> configuredExpressKeys)
    {
        int control = expressKeyEvent.Control;
        bool isPressed = expressKeyEvent.IsPressed;
        uint encodedButtons = (uint)(control & 0xFFFF) | (isPressed ? 0x0001_0000u : 0u);

        string action = control >= 0 && control < configuredExpressKeys.Count
            ? NormalizePenButtonAction(configuredExpressKeys[control].Action)
            : "Unknown";

        string diagnostics = $"EK{control}: {(isPressed ? "pressed" : "released")} [{action}]";
        string backendLabel = $"Wintab EK{control}";
        return new WintabExpressKeyUpdate(
            Control: control,
            IsPressed: isPressed,
            RequiredExpressKeyCount: control + 1,
            EncodedPenButtons: encodedButtons,
            ActionLabel: action,
            BackendLabel: backendLabel,
            ExpressKeyDiagnostics: diagnostics);
    }

    /// <inheritdoc />
    public bool TryBuildPacketUpdate(
        WintabPacketEventArgs packetEvent,
        double padWidth,
        double padHeight,
        out WintabPacketUpdate update)
    {
        if (padWidth <= 0 || padHeight <= 0)
        {
            update = default;
            return false;
        }

        double xNorm = packetEvent.XNorm;
        double yNorm = packetEvent.YNorm;
        bool isHovering = packetEvent.IsHovering;
        update = new WintabPacketUpdate(
            XPixel: xNorm * padWidth,
            YPixel: yNorm * padHeight,
            XNorm: xNorm,
            YNorm: yNorm,
            Pressure127: packetEvent.Pressure127,
            IsHovering: isHovering,
            PenButtons: packetEvent.Buttons,
            BackendLabel: $"Wintab D{packetEvent.DeviceIndex + 1}",
            PacketDiagnostics: packetEvent.PacketDiagnostics,
            CursorOpacity: isHovering ? 0.35 : 0.85,
            IsDown: !isHovering);
        return true;
    }

    private static string NormalizePenButtonAction(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            PenButtonActions.None => value,
            PenButtonActions.LockXY => value,
            PenButtonActions.LockX => value,
            PenButtonActions.LockY => value,
            PenButtonActions.DisablePressure => value,
            PenButtonActions.EnablePressure => value,
            PenButtonActions.CcMomentary => value,
            PenButtonActions.CcToggle => value,
            PenButtonActions.NoteMomentary => value,
            PenButtonActions.NoteToggle => value,
            _ => PenButtonActions.None
        };
    }
}
