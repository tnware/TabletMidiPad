namespace TabletMidiPad.Services.Input;

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
            ? NormalizeExpressKeyAction(configuredExpressKeys[control].Action)
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

    private static string NormalizeExpressKeyAction(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            ExpressKeyActions.None => value,
            ExpressKeyActions.CcMomentary => value,
            ExpressKeyActions.CcToggle => value,
            ExpressKeyActions.NoteMomentary => value,
            ExpressKeyActions.NoteToggle => value,
            ExpressKeyActions.PrevPage => value,
            ExpressKeyActions.NextPage => value,
            ExpressKeyActions.GoToPage1 => value,
            ExpressKeyActions.GoToPage2 => value,
            ExpressKeyActions.GoToPage3 => value,
            ExpressKeyActions.GoToPage4 => value,
            ExpressKeyActions.GoToPage5 => value,
            ExpressKeyActions.GoToPage6 => value,
            ExpressKeyActions.GoToPage7 => value,
            ExpressKeyActions.GoToPage8 => value,
            _ => ExpressKeyActions.None
        };
    }
}
