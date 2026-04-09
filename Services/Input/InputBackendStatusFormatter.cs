using System.Globalization;

namespace TabletMidiPad.Services.Input;

/// <summary>
/// Default formatter for input backend status text shown in the main view-model.
/// </summary>
internal sealed class InputBackendStatusFormatter : IInputBackendStatusFormatter
{
    /// <inheritdoc />
    public string BuildStatusText(
        string lastInputBackend,
        bool useWintab,
        int contextCount,
        string wintabDiagnostics,
        string expressKeyDiagnostics)
    {
        string ekPart = string.IsNullOrEmpty(expressKeyDiagnostics)
            ? string.Empty
            : $" | {expressKeyDiagnostics}";

        string configured = useWintab
            ? $"Wintab enabled, {contextCount} context(s){ekPart}"
            : $"Stylus/mouse fallback | {wintabDiagnostics}";

        return $"Input: {lastInputBackend} | {configured}";
    }

    /// <inheritdoc />
    public string BuildPenButtonsText(uint buttons)
    {
        uint buttonMask = buttons & 0xFFFF;
        uint transition = (buttons >> 16) & 0xFFFF;

        string transitionText = transition switch
        {
            1 => "up",
            2 => "down",
            _ => "none"
        };

        return $"Pen buttons: mask=0x{buttonMask:X4} pressed={FormatPressedButtons(buttonMask)} transition={transitionText}";
    }

    private static string FormatPressedButtons(uint buttonMask)
    {
        if (buttonMask == 0)
        {
            return "none";
        }

        var pressed = new List<string>();
        for (int bit = 0; bit < 16; bit++)
        {
            if ((buttonMask & (1u << bit)) != 0)
            {
                pressed.Add((bit + 1).ToString(CultureInfo.InvariantCulture));
            }
        }

        return string.Join(",", pressed);
    }
}
