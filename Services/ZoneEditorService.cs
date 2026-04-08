using System;
using System.Collections.ObjectModel;
using System.Globalization;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Default implementation for zone editor projection and commit logic.
/// </summary>
internal sealed class ZoneEditorService : IZoneEditorService
{
    public void RefreshZoneList(ObservableCollection<ZoneListItemViewModel> target, AppConfig config)
    {
        target.Clear();
        for (int i = 0; i < config.Zones.Count; i++)
        {
            ZoneConfig zone = config.Zones[i];
            string enabled = zone.Enabled ? "on" : "off";
            target.Add(new ZoneListItemViewModel
            {
                Index = i,
                DisplayText = $"{i + 1}. {zone.Name} [{enabled}]",
            });
        }
    }

    public void LoadSelectedZoneIntoEditor(MainViewModel viewModel, AppConfig config, int selectedZoneIndex)
    {
        if (selectedZoneIndex < 0 || selectedZoneIndex >= config.Zones.Count)
        {
            viewModel.ZoneName = string.Empty;
            viewModel.ZoneEnabled = false;
            viewModel.ZoneChannel = string.Empty;
            SetZoneSelectionSummary(viewModel, null);
            viewModel.ZoneDetailsEnabled = false;
            viewModel.ZoneCcX = string.Empty;
            viewModel.ZoneCcY = string.Empty;
            viewModel.ZoneCcPressure = string.Empty;
            viewModel.ZoneLeft = string.Empty;
            viewModel.ZoneTop = string.Empty;
            viewModel.ZoneWidth = string.Empty;
            viewModel.ZoneHeight = string.Empty;
            viewModel.ZoneSendX = false;
            viewModel.ZoneSendY = false;
            viewModel.ZoneInvertX = false;
            viewModel.ZoneInvertY = false;
            viewModel.ZoneSendPressure = false;
            viewModel.ZoneReleaseBehavior = string.Empty;
            return;
        }

        ZoneConfig zone = config.Zones[selectedZoneIndex];
        viewModel.ZoneDetailsEnabled = true;
        viewModel.ZoneName = zone.Name;
        viewModel.ZoneEnabled = zone.Enabled;
        viewModel.ZoneChannel = zone.MidiChannel.ToString(CultureInfo.InvariantCulture);
        SetZoneSelectionSummary(viewModel, zone);
        viewModel.ZoneCcX = zone.CcX.ToString(CultureInfo.InvariantCulture);
        viewModel.ZoneCcY = zone.CcY.ToString(CultureInfo.InvariantCulture);
        viewModel.ZoneCcPressure = zone.CcPressure.ToString(CultureInfo.InvariantCulture);
        viewModel.ZoneLeft = zone.Left.ToString("0.###", CultureInfo.InvariantCulture);
        viewModel.ZoneTop = zone.Top.ToString("0.###", CultureInfo.InvariantCulture);
        viewModel.ZoneWidth = zone.Width.ToString("0.###", CultureInfo.InvariantCulture);
        viewModel.ZoneHeight = zone.Height.ToString("0.###", CultureInfo.InvariantCulture);
        viewModel.ZoneSendX = zone.SendX;
        viewModel.ZoneSendY = zone.SendY;
        viewModel.ZoneInvertX = zone.InvertX;
        viewModel.ZoneInvertY = zone.InvertY;
        viewModel.ZoneSendPressure = zone.SendPressure;
        viewModel.ZoneReleaseBehavior = NormalizeReleaseBehavior(zone.ReleaseBehavior);
    }

    public void CommitCurrentZoneEdits(
        AppConfig config,
        MainViewModel viewModel,
        int selectedZoneIndex,
        string? maxZoneRowsText,
        string? maxZonesPerRowText)
    {
        if (selectedZoneIndex < 0 || selectedZoneIndex >= config.Zones.Count)
        {
            return;
        }

        ZoneConfig zone = config.Zones[selectedZoneIndex];
        config.MaxZoneRows = ReadInt(maxZoneRowsText, 1, 16, "Max Zone Rows");
        config.MaxZonesPerRow = ReadInt(maxZonesPerRowText, 1, 32, "Max Zones Per Row");
        zone.Name = RequireText(viewModel.ZoneName, "Zone Name");
        zone.Enabled = viewModel.ZoneEnabled;
        zone.MidiChannel = ReadInt(viewModel.ZoneChannel, 1, 16, "Zone MIDI Channel");
        zone.CcX = ReadInt(viewModel.ZoneCcX, 0, 127, "Zone CC X");
        zone.CcY = ReadInt(viewModel.ZoneCcY, 0, 127, "Zone CC Y");
        zone.CcPressure = ReadInt(viewModel.ZoneCcPressure, 0, 127, "Zone Pressure CC");
        zone.Left = ReadDouble(viewModel.ZoneLeft, 0.0, 1.0, "Zone Left");
        zone.Top = ReadDouble(viewModel.ZoneTop, 0.0, 1.0, "Zone Top");
        zone.Width = ReadDouble(viewModel.ZoneWidth, 0.0, 1.0, "Zone Width");
        zone.Height = ReadDouble(viewModel.ZoneHeight, 0.0, 1.0, "Zone Height");
        zone.SendX = viewModel.ZoneSendX;
        zone.SendY = viewModel.ZoneSendY;
        zone.InvertX = viewModel.ZoneInvertX;
        zone.InvertY = viewModel.ZoneInvertY;
        zone.SendPressure = viewModel.ZoneSendPressure;
        zone.ReleaseBehavior = NormalizeReleaseBehavior(viewModel.ZoneReleaseBehavior);
        zone.ResetPressureOnRelease = string.Equals(zone.ReleaseBehavior, ZoneReleaseBehaviors.ResetPressureOnly, StringComparison.Ordinal);
        SetZoneSelectionSummary(viewModel, zone);
    }

    public void SetZoneSelectionSummary(MainViewModel viewModel, ZoneConfig? zone)
    {
        if (zone is null)
        {
            viewModel.ZoneSelectionSummaryPrimaryText = "No zone selected";
            viewModel.ZoneSelectionSummarySecondaryText = "Select a zone to edit its routing and bounds.";
            return;
        }

        string enabled = zone.Enabled ? "Enabled" : "Disabled";
        viewModel.ZoneSelectionSummaryPrimaryText =
            $"Channel {zone.MidiChannel}  •  CC X/Y/P {zone.CcX}/{zone.CcY}/{zone.CcPressure}  •  {enabled}";
        viewModel.ZoneSelectionSummarySecondaryText =
            $"Bounds {zone.Left:0.###}, {zone.Top:0.###}, {zone.Width:0.###}, {zone.Height:0.###}  •  " +
            $"Release {NormalizeReleaseBehavior(zone.ReleaseBehavior)}";
    }

    private static int ReadInt(string? text, int min, int max, string fieldName)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
        {
            throw new InvalidOperationException($"{fieldName} must be a whole number.");
        }

        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{fieldName} must be between {min} and {max}.");
        }

        return value;
    }

    private static double ReadDouble(string? text, double min, double max, string fieldName)
    {
        if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double value))
        {
            throw new InvalidOperationException($"{fieldName} must be a number.");
        }

        if (value < min || value > max)
        {
            throw new InvalidOperationException($"{fieldName} must be between {min:0.###} and {max:0.###}.");
        }

        return value;
    }

    private static string RequireText(string? text, string fieldName)
    {
        string value = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return value;
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
            _ => ZoneReleaseBehaviors.ResetPressureOnly,
        };
    }
}
