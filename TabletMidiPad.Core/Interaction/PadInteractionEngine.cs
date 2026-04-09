using System;
using System.Collections.Generic;
using System.Globalization;
using PenButtonActions = TabletMidiPad.Core.Interaction.InteractionConstants.PenButtonActions;
using ExpressKeyActions = TabletMidiPad.Core.Interaction.InteractionConstants.ExpressKeyActions;
using ZoneReleaseBehaviors = TabletMidiPad.Core.Interaction.InteractionConstants.ZoneReleaseBehaviors;

namespace TabletMidiPad.Core.Interaction;

/// <summary>
/// Default implementation of the pad interaction engine.
/// </summary>
public sealed class PadInteractionEngine : IPadInteractionEngine
{
    /// <inheritdoc />
    public InteractionResult Process(
        PadInteractionConfig config,
        IReadOnlyList<ZoneSnapshot> zones,
        InteractionState state,
        InteractionInput input)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(state);

        string lowerAction = NormalizePenButtonAction(config.LowerPenButtonAction);
        string upperAction = NormalizePenButtonAction(config.UpperPenButtonAction);

        List<MidiMessage>? penButtonMessages = null;

        // Express key input is a separate path; it does not affect active zone / XY.
        if (input.Backend == InteractionBackend.ExpressKey)
        {
            IReadOnlyList<MidiMessage> ek = ProcessExpressKey(config, input, state);
            return new InteractionResult(
                ActiveZoneIndex: state.ActiveZoneIndex,
                ActiveZoneLabel: state.ActiveZoneIndex >= 0 && state.ActiveZoneIndex < zones.Count
                    ? $"Active zone: {zones[state.ActiveZoneIndex].Name}"
                    : "Active zone: none",
                ValueLabel: "Express key",
                ModifiersLabel: BuildModifiersLabel(config, state, lowerAction, upperAction),
                MidiMessages: ek);
        }

        penButtonMessages = UpdatePenButtonActionState(config, input.PenButtons, state, lowerAction, upperAction);

        if (input.IsHovering)
        {
            int hoverIndex = FindMatchingZoneIndex(zones, input.XNorm, input.YNorm);
            string hoverText = hoverIndex >= 0 ? $"Hovering: {zones[hoverIndex].Name}" : "Hovering: outside zones";
            return new InteractionResult(
                ActiveZoneIndex: state.ActiveZoneIndex,
                ActiveZoneLabel: hoverText,
                ValueLabel: hoverIndex >= 0 ? hoverText : "Outside configured zones",
                ModifiersLabel: BuildModifiersLabel(config, state, lowerAction, upperAction),
                MidiMessages: penButtonMessages is null ? Array.Empty<MidiMessage>() : penButtonMessages);
        }

        if (!input.IsDown)
        {
            // Release tick.
            var messages = new List<MidiMessage>();
            if (state.ActiveZoneIndex >= 0 && state.ActiveZoneIndex < zones.Count)
            {
                SendZoneRelease(config, zones[state.ActiveZoneIndex], messages);
            }

            state.ActiveZoneIndex = -1;
            state.HasLockedPosition = false;
            state.LowerPenButtonHeld = false;
            state.UpperPenButtonHeld = false;

            if (penButtonMessages is not null && penButtonMessages.Count > 0)
            {
                messages.AddRange(penButtonMessages);
            }

            return new InteractionResult(
                ActiveZoneIndex: -1,
                ActiveZoneLabel: "Active zone: none",
                ValueLabel: "X: -, Y: -, Pressure: -",
                ModifiersLabel: "Modifiers: none",
                MidiMessages: messages);
        }

        int zoneIndex = ResolveZoneForCurrentInteraction(config, zones, input.XNorm, input.YNorm, state.ActiveZoneIndex);
        if (zoneIndex != state.ActiveZoneIndex)
        {
            var switchMessages = new List<MidiMessage>();
            if (state.ActiveZoneIndex >= 0 && state.ActiveZoneIndex < zones.Count)
            {
                SendZoneRelease(config, zones[state.ActiveZoneIndex], switchMessages);
            }
            state.ActiveZoneIndex = zoneIndex;

            // If we switched zones, we still process the current sample for the new zone (and return both sets of messages).
            InteractionResult switched = ProcessActiveZone(config, zones, input, state, lowerAction, upperAction);
            if (switchMessages.Count == 0)
            {
                if (penButtonMessages is null || penButtonMessages.Count == 0)
                {
                    return switched;
                }

                var combined2 = new List<MidiMessage>(penButtonMessages.Count + switched.MidiMessages.Count);
                combined2.AddRange(penButtonMessages);
                combined2.AddRange(switched.MidiMessages);
                return switched with { MidiMessages = combined2 };
            }

            var combined = new List<MidiMessage>(switchMessages.Count + switched.MidiMessages.Count);
            combined.AddRange(switchMessages);
            combined.AddRange(switched.MidiMessages);

            if (penButtonMessages is not null && penButtonMessages.Count > 0)
            {
                combined.InsertRange(0, penButtonMessages);
            }

            return switched with { MidiMessages = combined };
        }

        InteractionResult result = ProcessActiveZone(config, zones, input, state, lowerAction, upperAction);
        if (penButtonMessages is null || penButtonMessages.Count == 0)
        {
            return result;
        }

        var combined3 = new List<MidiMessage>(penButtonMessages.Count + result.MidiMessages.Count);
        combined3.AddRange(penButtonMessages);
        combined3.AddRange(result.MidiMessages);
        return result with { MidiMessages = combined3 };
    }

    private static InteractionResult ProcessActiveZone(
        PadInteractionConfig config,
        IReadOnlyList<ZoneSnapshot> zones,
        InteractionInput input,
        InteractionState state,
        string lowerAction,
        string upperAction)
    {
        if (state.ActiveZoneIndex < 0 || state.ActiveZoneIndex >= zones.Count)
        {
            return new InteractionResult(
                ActiveZoneIndex: -1,
                ActiveZoneLabel: "Active zone: none",
                ValueLabel: "Outside configured zones",
                ModifiersLabel: BuildModifiersLabel(config, state, lowerAction, upperAction),
                MidiMessages: Array.Empty<MidiMessage>());
        }

        ZoneSnapshot zone = zones[state.ActiveZoneIndex];
        if (!zone.Enabled)
        {
            return new InteractionResult(
                ActiveZoneIndex: -1,
                ActiveZoneLabel: "Active zone: none",
                ValueLabel: "Outside configured zones",
                ModifiersLabel: BuildModifiersLabel(config, state, lowerAction, upperAction),
                MidiMessages: Array.Empty<MidiMessage>());
        }

        (double xLocal, double yLocal) = NormalizeWithinZone(zone, input.XNorm, input.YNorm);
        (xLocal, yLocal) = ApplyPositionConditioning(config, state, state.ActiveZoneIndex, xLocal, yLocal);

        bool lockXY =
            (state.LowerPenButtonHeld && string.Equals(lowerAction, PenButtonActions.LockXY, StringComparison.Ordinal)) ||
            (state.UpperPenButtonHeld && string.Equals(upperAction, PenButtonActions.LockXY, StringComparison.Ordinal));
        bool lockX =
            (state.LowerPenButtonHeld && string.Equals(lowerAction, PenButtonActions.LockX, StringComparison.Ordinal)) ||
            (state.UpperPenButtonHeld && string.Equals(upperAction, PenButtonActions.LockX, StringComparison.Ordinal));
        bool lockY =
            (state.LowerPenButtonHeld && string.Equals(lowerAction, PenButtonActions.LockY, StringComparison.Ordinal)) ||
            (state.UpperPenButtonHeld && string.Equals(upperAction, PenButtonActions.LockY, StringComparison.Ordinal));

        if (lockXY || lockX || lockY)
        {
            if (!state.HasLockedPosition)
            {
                state.LockedLocalX = xLocal;
                state.LockedLocalY = yLocal;
                state.HasLockedPosition = true;
            }

            xLocal = lockXY || lockX ? state.LockedLocalX : xLocal;
            yLocal = lockXY || lockY ? state.LockedLocalY : yLocal;
        }
        else
        {
            state.HasLockedPosition = false;
        }

        state.LastLocalByZone[state.ActiveZoneIndex] = (xLocal, yLocal);

        int xCc = ToCc(xLocal, zone.InvertX);
        int yCc = ToCc(yLocal, zone.InvertY);
        int curvedPressure = ApplyPressureCurve(config, input.Pressure);

        bool sendPressure = config.SendPressureGlobally && zone.SendPressure;
        if ((state.LowerPenButtonHeld && string.Equals(lowerAction, PenButtonActions.DisablePressure, StringComparison.Ordinal)) ||
            (state.UpperPenButtonHeld && string.Equals(upperAction, PenButtonActions.DisablePressure, StringComparison.Ordinal)))
        {
            sendPressure = false;
        }

        bool pressureEnabledByButton =
            (state.LowerPenButtonHeld && string.Equals(lowerAction, PenButtonActions.EnablePressure, StringComparison.Ordinal)) ||
            (state.UpperPenButtonHeld && string.Equals(upperAction, PenButtonActions.EnablePressure, StringComparison.Ordinal));
        bool anyEnablePressureAction =
            string.Equals(lowerAction, PenButtonActions.EnablePressure, StringComparison.Ordinal) ||
            string.Equals(upperAction, PenButtonActions.EnablePressure, StringComparison.Ordinal);
        if (anyEnablePressureAction)
        {
            sendPressure = sendPressure && pressureEnabledByButton;
        }

        string xText = zone.SendX ? xCc.ToString(CultureInfo.InvariantCulture) : "off";
        string yText = zone.SendY ? yCc.ToString(CultureInfo.InvariantCulture) : "off";
        string pressureText = sendPressure ? curvedPressure.ToString(CultureInfo.InvariantCulture) : "off";

        var messages = new List<MidiMessage>(capacity: 3);
        if (zone.SendX)
            messages.Add(MidiMessage.Cc(zone.MidiChannel, zone.CcX, xCc));
        if (zone.SendY)
            messages.Add(MidiMessage.Cc(zone.MidiChannel, zone.CcY, yCc));
        if (sendPressure)
            messages.Add(MidiMessage.Cc(zone.MidiChannel, zone.CcPressure, curvedPressure));

        return new InteractionResult(
            ActiveZoneIndex: state.ActiveZoneIndex,
            ActiveZoneLabel: $"Active zone: {zone.Name}",
            ValueLabel: $"{zone.Name} -> X: {xText}, Y: {yText}, Pressure: {pressureText}",
            ModifiersLabel: BuildModifiersLabel(config, state, lowerAction, upperAction),
            MidiMessages: messages);
    }

    private static IReadOnlyList<MidiMessage> ProcessExpressKey(PadInteractionConfig config, InteractionInput input, InteractionState state)
    {
        // Expected encoding: input.PenButtons lower 16 bits -> key index, upper 16 bits -> 1=pressed, 0=released.
        int control = unchecked((short)(input.PenButtons & 0xFFFF));
        bool isPressed = ((input.PenButtons >> 16) & 0xFFFF) != 0;

        state.ExpressKeyHeld.TryGetValue(control, out bool wasHeld);
        state.ExpressKeyToggle.TryGetValue(control, out bool toggleState);

        bool justPressed = isPressed && !wasHeld;
        bool justReleased = !isPressed && wasHeld;

        if (control < 0 || control >= config.ExpressKeys.Count)
        {
            state.ExpressKeyHeld[control] = isPressed;
            state.ExpressKeyToggle[control] = toggleState;
            return Array.Empty<MidiMessage>();
        }

        var keyConfig = config.ExpressKeys[control];
        string action = NormalizeExpressKeyAction(keyConfig.Action);
        int channel = keyConfig.MidiChannel;
        int cc = Math.Clamp(keyConfig.Cc, 0, 127);
        int note = Math.Clamp(keyConfig.Note, 0, 127);

        var messages = new List<MidiMessage>(capacity: 1);

        if (string.Equals(action, ExpressKeyActions.CcMomentary, StringComparison.Ordinal))
        {
            if (justPressed)
                messages.Add(MidiMessage.Cc(channel, cc, 127, force: true));
            else if (justReleased)
                messages.Add(MidiMessage.Cc(channel, cc, 0, force: true));
        }
        else if (string.Equals(action, ExpressKeyActions.CcToggle, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                toggleState = !toggleState;
                messages.Add(MidiMessage.Cc(channel, cc, toggleState ? 127 : 0, force: true));
            }
        }
        else if (string.Equals(action, ExpressKeyActions.NoteMomentary, StringComparison.Ordinal))
        {
            if (justPressed)
                messages.Add(MidiMessage.Note(channel, note, on: true, velocity: 127, force: true));
            else if (justReleased)
                messages.Add(MidiMessage.Note(channel, note, on: false, velocity: 0, force: true));
        }
        else if (string.Equals(action, ExpressKeyActions.NoteToggle, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                toggleState = !toggleState;
                messages.Add(MidiMessage.Note(channel, note, on: toggleState, velocity: toggleState ? 127 : 0, force: true));
            }
        }

        state.ExpressKeyHeld[control] = isPressed;
        state.ExpressKeyToggle[control] = toggleState;
        return messages;
    }

    private static List<MidiMessage>? UpdatePenButtonActionState(
        PadInteractionConfig config,
        uint buttons,
        InteractionState state,
        string lowerAction,
        string upperAction)
    {
        List<MidiMessage>? messages = null;

        HandlePenButtonAction(
            isHeld: (buttons & 0x0002) != 0,
            state: state,
            isLower: true,
            action: lowerAction,
            midiChannel: config.LowerPenButtonMidiChannel,
            cc: config.LowerPenButtonCc,
            note: config.LowerPenButtonNote,
            messages: ref messages);

        HandlePenButtonAction(
            isHeld: (buttons & 0x0004) != 0,
            state: state,
            isLower: false,
            action: upperAction,
            midiChannel: config.UpperPenButtonMidiChannel,
            cc: config.UpperPenButtonCc,
            note: config.UpperPenButtonNote,
            messages: ref messages);

        return messages;
    }

    private static void HandlePenButtonAction(
        bool isHeld,
        InteractionState state,
        bool isLower,
        string action,
        int midiChannel,
        int cc,
        int note,
        ref List<MidiMessage>? messages)
    {
        bool heldState = isLower ? state.LowerPenButtonHeld : state.UpperPenButtonHeld;
        bool toggleState = isLower ? state.LowerPenButtonToggle : state.UpperPenButtonToggle;

        bool justPressed = isHeld && !heldState;
        bool justReleased = !isHeld && heldState;

        if (justPressed)
        {
            state.HasLockedPosition = false;
        }

        if (string.Equals(action, PenButtonActions.CcMomentary, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                (messages ??= new List<MidiMessage>(capacity: 1)).Add(MidiMessage.Cc(midiChannel, cc, 127, force: true));
            }
            else if (justReleased)
            {
                (messages ??= new List<MidiMessage>(capacity: 1)).Add(MidiMessage.Cc(midiChannel, cc, 0, force: true));
            }
        }
        else if (string.Equals(action, PenButtonActions.CcToggle, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                toggleState = !toggleState;
                (messages ??= new List<MidiMessage>(capacity: 1)).Add(MidiMessage.Cc(midiChannel, cc, toggleState ? 127 : 0, force: true));
            }
        }
        else if (string.Equals(action, PenButtonActions.NoteMomentary, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                (messages ??= new List<MidiMessage>(capacity: 1)).Add(MidiMessage.Note(midiChannel, note, on: true, velocity: 127, force: true));
            }
            else if (justReleased)
            {
                (messages ??= new List<MidiMessage>(capacity: 1)).Add(MidiMessage.Note(midiChannel, note, on: false, velocity: 0, force: true));
            }
        }
        else if (string.Equals(action, PenButtonActions.NoteToggle, StringComparison.Ordinal))
        {
            if (justPressed)
            {
                toggleState = !toggleState;
                (messages ??= new List<MidiMessage>(capacity: 1)).Add(MidiMessage.Note(midiChannel, note, on: toggleState, velocity: toggleState ? 127 : 0, force: true));
            }
        }

        if (justReleased)
        {
            state.HasLockedPosition = false;
        }

        if (isLower)
        {
            state.LowerPenButtonHeld = isHeld;
            state.LowerPenButtonToggle = toggleState;
        }
        else
        {
            state.UpperPenButtonHeld = isHeld;
            state.UpperPenButtonToggle = toggleState;
        }
    }

    private static void SendZoneRelease(PadInteractionConfig config, ZoneSnapshot zone, List<MidiMessage> messages)
    {
        if (!zone.Enabled)
        {
            return;
        }

        string behavior = NormalizeReleaseBehavior(zone.ReleaseBehavior);
        if (string.Equals(behavior, ZoneReleaseBehaviors.Hold, StringComparison.Ordinal) ||
            string.Equals(behavior, ZoneReleaseBehaviors.NoSendOnRelease, StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(behavior, ZoneReleaseBehaviors.ResetAll, StringComparison.Ordinal))
        {
            if (zone.SendX)
                messages.Add(MidiMessage.Cc(zone.MidiChannel, zone.CcX, 0, force: true));
            if (zone.SendY)
                messages.Add(MidiMessage.Cc(zone.MidiChannel, zone.CcY, 0, force: true));
        }

        if (config.SendPressureGlobally && zone.SendPressure)
        {
            messages.Add(MidiMessage.Cc(zone.MidiChannel, zone.CcPressure, 0, force: true));
        }
    }

    private static int ResolveZoneForCurrentInteraction(
        PadInteractionConfig config,
        IReadOnlyList<ZoneSnapshot> zones,
        double xNorm,
        double yNorm,
        int currentActiveZoneIndex)
    {
        if (!config.AllowZoneSwitchWhilePressed && currentActiveZoneIndex >= 0 && currentActiveZoneIndex < zones.Count)
        {
            return currentActiveZoneIndex;
        }

        return FindMatchingZoneIndex(zones, xNorm, yNorm);
    }

    private static int FindMatchingZoneIndex(IReadOnlyList<ZoneSnapshot> zones, double xNorm, double yNorm)
    {
        for (int i = 0; i < zones.Count; i++)
        {
            ZoneSnapshot zone = zones[i];
            if (!zone.Enabled)
                continue;

            if (xNorm >= zone.Left &&
                xNorm <= zone.Left + zone.Width &&
                yNorm >= zone.Top &&
                yNorm <= zone.Top + zone.Height)
            {
                return i;
            }
        }

        return -1;
    }

    private static (double x, double y) NormalizeWithinZone(ZoneSnapshot zone, double xNorm, double yNorm)
    {
        double x = zone.Width <= 0 ? 0 : (xNorm - zone.Left) / zone.Width;
        double y = zone.Height <= 0 ? 0 : (yNorm - zone.Top) / zone.Height;
        return (Math.Clamp(x, 0.0, 1.0), Math.Clamp(y, 0.0, 1.0));
    }

    private static (double x, double y) ApplyPositionConditioning(
        PadInteractionConfig config,
        InteractionState state,
        int zoneIndex,
        double x,
        double y)
    {
        if (!state.SmoothedLocalByZone.TryGetValue(zoneIndex, out (double x, double y) last))
        {
            state.SmoothedLocalByZone[zoneIndex] = (x, y);
            return (x, y);
        }

        double deadZone = Math.Clamp(config.PositionDeadZone, 0.0, 0.1);
        double smoothing = Math.Clamp(config.PositionSmoothing, 0.0, 0.95);
        double alpha = 1.0 - smoothing;

        double targetX = Math.Abs(x - last.x) < deadZone ? last.x : x;
        double targetY = Math.Abs(y - last.y) < deadZone ? last.y : y;

        double filteredX = last.x + ((targetX - last.x) * alpha);
        double filteredY = last.y + ((targetY - last.y) * alpha);

        (double x, double y) filtered = (Math.Clamp(filteredX, 0.0, 1.0), Math.Clamp(filteredY, 0.0, 1.0));
        state.SmoothedLocalByZone[zoneIndex] = filtered;
        return filtered;
    }

    private static int ApplyPressureCurve(PadInteractionConfig config, int pressure)
    {
        double normalized = Math.Clamp(pressure / 127.0, 0.0, 1.0);
        double curved = config.PressureCurve switch
        {
            "Logarithmic" => Math.Sqrt(normalized),
            "Exponential" => Math.Pow(normalized, 2.0),
            _ => normalized
        };

        return Math.Clamp((int)Math.Round(curved * 127.0), 0, 127);
    }

    private static int ToCc(double normalized, bool invert)
    {
        normalized = Math.Clamp(normalized, 0.0, 1.0);
        if (invert)
        {
            normalized = 1.0 - normalized;
        }

        return (int)Math.Round(normalized * 127.0);
    }

    private static string NormalizeReleaseBehavior(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            "Hold Last Values" => ZoneReleaseBehaviors.Hold,
            ZoneReleaseBehaviors.Hold => value,
            ZoneReleaseBehaviors.ResetAll => value,
            ZoneReleaseBehaviors.NoSendOnRelease => value,
            _ => ZoneReleaseBehaviors.ResetPressureOnly
        };
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

    private static string BuildModifiersLabel(
        PadInteractionConfig config,
        InteractionState state,
        string lowerAction,
        string upperAction)
    {
        var parts = new List<string>();
        AppendPenButtonModifier(parts, "lower", lowerAction, state.LowerPenButtonHeld, state.LowerPenButtonToggle);
        AppendPenButtonModifier(parts, "upper", upperAction, state.UpperPenButtonHeld, state.UpperPenButtonToggle);
        return parts.Count > 0 ? $"Modifiers: {string.Join(" | ", parts)}" : "Modifiers: none";
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
}

