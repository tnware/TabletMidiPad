using System;
using System.Collections.Generic;
using TabletMidiPad;

namespace TabletMidiPad.Views;

public partial class MainWindow
{
    private void MarkInputBackend(string backend)
    {
        _lastInputBackend = backend;
        UpdateInputBackendStatus();
    }

    private void UpdateInputBackendStatus()
    {
        string ekPart = string.IsNullOrEmpty(_expressKeyDiagnostics) ? "" : $" | {_expressKeyDiagnostics}";
        string configured = _useWintab
            ? $"Wintab enabled, {_wintabContexts.Count} context(s){ekPart}"
            : $"Stylus/mouse fallback | {_wintabDiagnostics}";

        _viewModel.InputBackendText = $"Input: {_lastInputBackend} | {configured}";
    }

    private void RefreshWacomServiceStatus()
    {
        (string header, bool canStart) = _wacomService.GetStatus();
        _viewModel.WacomServiceStatusText = header;
        _viewModel.CanStartWacomService = canStart;
    }

    private void UpdatePenButtonsDebug(uint buttons)
    {
        _lastWintabButtons = buttons;
        uint buttonMask = buttons & 0xFFFF;
        uint transition = (buttons >> 16) & 0xFFFF;

        string transitionText = transition switch
        {
            1 => "up",
            2 => "down",
            _ => "none"
        };

        _viewModel.PenButtonsText = $"Pen buttons: mask=0x{buttonMask:X4} pressed={FormatPressedButtons(buttonMask)} transition={transitionText}";
    }

    private void UpdatePenButtonActionState(uint buttons)
    {
        HandlePenButtonAction(
            isHeld: (buttons & 0x0002) != 0,
            ref _lowerPenButtonHeld,
            ref _lowerPenButtonToggleState,
            _inputMappingService.NormalizePenButtonAction(_config.LowerPenButtonAction),
            _config.LowerPenButtonMidiChannel,
            _config.LowerPenButtonCc,
            _config.LowerPenButtonNote);

        HandlePenButtonAction(
            isHeld: (buttons & 0x0004) != 0,
            ref _upperPenButtonHeld,
            ref _upperPenButtonToggleState,
            _inputMappingService.NormalizePenButtonAction(_config.UpperPenButtonAction),
            _config.UpperPenButtonMidiChannel,
            _config.UpperPenButtonCc,
            _config.UpperPenButtonNote);

        UpdateModifiersText();
    }

    private void HandlePenButtonAction(
        bool isHeld,
        ref bool heldState,
        ref bool toggleState,
        string action,
        int midiChannel,
        int cc,
        int note)
    {
        bool justPressed = isHeld && !heldState;
        bool justReleased = !isHeld && heldState;

        if (justPressed)
        {
            _hasLockedPosition = false;
        }

        if (string.Equals(action, PenButtonActions.CcMomentary, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                SendCcIfChanged(midiChannel, cc, 127, force: true);
            }
            else if (justReleased)
            {
                SendCcIfChanged(midiChannel, cc, 0, force: true);
            }
        }
        else if (string.Equals(action, PenButtonActions.CcToggle, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                toggleState = !toggleState;
                SendCcIfChanged(midiChannel, cc, toggleState ? 127 : 0, force: true);
            }
        }
        else if (string.Equals(action, PenButtonActions.NoteMomentary, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                SendNoteIfChanged(midiChannel, note, true, 127, force: true);
            }
            else if (justReleased)
            {
                SendNoteIfChanged(midiChannel, note, false, 0, force: true);
            }
        }
        else if (string.Equals(action, PenButtonActions.NoteToggle, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                toggleState = !toggleState;
                SendNoteIfChanged(midiChannel, note, toggleState, toggleState ? 127 : 0, force: true);
            }
        }

        if (justReleased)
        {
            _hasLockedPosition = false;
        }

        heldState = isHeld;
    }

    private void UpdateModifiersText()
    {
        var parts = new List<string>();

        AppendPenButtonModifier(parts, "lower", _inputMappingService.NormalizePenButtonAction(_config.LowerPenButtonAction), _lowerPenButtonHeld, _lowerPenButtonToggleState);
        AppendPenButtonModifier(parts, "upper", _inputMappingService.NormalizePenButtonAction(_config.UpperPenButtonAction), _upperPenButtonHeld, _upperPenButtonToggleState);

        _viewModel.ModifiersText = parts.Count > 0
            ? $"Modifiers: {string.Join(" | ", parts)}"
            : "Modifiers: none";
    }

    private static void AppendPenButtonModifier(List<string> parts, string label, string action, bool held, bool toggled)
    {
        string? text = action switch
        {
            var a when held && string.Equals(a, PenButtonActions.LockXY, StringComparison.Ordinal) => $"{label} XY lock",
            var a when held && string.Equals(a, PenButtonActions.LockX, StringComparison.Ordinal) => $"{label} X lock",
            var a when held && string.Equals(a, PenButtonActions.LockY, StringComparison.Ordinal) => $"{label} Y lock",
            var a when held && string.Equals(a, PenButtonActions.DisablePressure, StringComparison.Ordinal) => $"{label} pressure off",
            var a when held && string.Equals(a, PenButtonActions.EnablePressure, StringComparison.Ordinal) => $"{label} pressure on",
            var a when string.Equals(a, PenButtonActions.CcToggle, StringComparison.Ordinal) => $"{label} CC {(toggled ? "on" : "off")}",
            var a when string.Equals(a, PenButtonActions.NoteToggle, StringComparison.Ordinal) => $"{label} note {(toggled ? "on" : "off")}",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(text))
        {
            parts.Add(text);
        }
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
                pressed.Add((bit + 1).ToString());
            }
        }

        return string.Join(",", pressed);
    }

}