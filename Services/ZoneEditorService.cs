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
    private readonly IConfigValueParser _configValueParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZoneEditorService"/> class.
    /// </summary>
    public ZoneEditorService(IConfigValueParser configValueParser)
    {
        _configValueParser = configValueParser ?? throw new ArgumentNullException(nameof(configValueParser));
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void LoadSelectedZoneIntoEditor(IZoneEditorState state, AppConfig config, int selectedZoneIndex)
    {
        if (selectedZoneIndex < 0 || selectedZoneIndex >= config.Zones.Count)
        {
            state.ZoneName = string.Empty;
            state.ZoneEnabled = false;
            state.ZoneMidiChannel = 1;
            SetZoneSelectionSummary(state, null);
            state.ZoneDetailsEnabled = false;
            state.ZoneCcX = string.Empty;
            state.ZoneCcY = string.Empty;
            state.ZoneCcPressure = string.Empty;
            state.ZoneLeft = string.Empty;
            state.ZoneTop = string.Empty;
            state.ZoneWidth = string.Empty;
            state.ZoneHeight = string.Empty;
            state.ZoneSendX = false;
            state.ZoneSendY = false;
            state.ZoneInvertX = false;
            state.ZoneInvertY = false;
            state.ZoneSendPressure = false;
            state.ZoneReleaseBehavior = string.Empty;
            return;
        }

        ZoneConfig zone = config.Zones[selectedZoneIndex];
        state.ZoneDetailsEnabled = true;
        state.ZoneName = zone.Name;
        state.ZoneEnabled = zone.Enabled;
        state.ZoneMidiChannel = zone.MidiChannel;
        SetZoneSelectionSummary(state, zone);
        state.ZoneCcX = zone.CcX.ToString(CultureInfo.InvariantCulture);
        state.ZoneCcY = zone.CcY.ToString(CultureInfo.InvariantCulture);
        state.ZoneCcPressure = zone.CcPressure.ToString(CultureInfo.InvariantCulture);
        state.ZoneLeft = zone.Left.ToString("0.###", CultureInfo.InvariantCulture);
        state.ZoneTop = zone.Top.ToString("0.###", CultureInfo.InvariantCulture);
        state.ZoneWidth = zone.Width.ToString("0.###", CultureInfo.InvariantCulture);
        state.ZoneHeight = zone.Height.ToString("0.###", CultureInfo.InvariantCulture);
        state.ZoneSendX = zone.SendX;
        state.ZoneSendY = zone.SendY;
        state.ZoneInvertX = zone.InvertX;
        state.ZoneInvertY = zone.InvertY;
        state.ZoneSendPressure = zone.SendPressure;
        state.ZoneReleaseBehavior = _configValueParser.NormalizeReleaseBehavior(zone.ReleaseBehavior);
    }

    /// <inheritdoc />
    public void CommitCurrentZoneEdits(
        AppConfig config,
        IZoneEditorState state,
        int selectedZoneIndex,
        string? maxZoneRowsText,
        string? maxZonesPerRowText)
    {
        if (selectedZoneIndex < 0 || selectedZoneIndex >= config.Zones.Count)
        {
            return;
        }

        ZoneConfig zone = config.Zones[selectedZoneIndex];
        config.MaxZoneRows = _configValueParser.ReadInt(maxZoneRowsText, 1, 16, "Max Zone Rows");
        config.MaxZonesPerRow = _configValueParser.ReadInt(maxZonesPerRowText, 1, 32, "Max Zones Per Row");
        zone.Name = _configValueParser.RequireText(state.ZoneName, "Zone Name");
        zone.Enabled = state.ZoneEnabled;
        zone.MidiChannel = Math.Clamp(state.ZoneMidiChannel, 1, 16);
        zone.CcX = _configValueParser.ReadInt(state.ZoneCcX, 0, 127, "Zone CC X");
        zone.CcY = _configValueParser.ReadInt(state.ZoneCcY, 0, 127, "Zone CC Y");
        zone.CcPressure = _configValueParser.ReadInt(state.ZoneCcPressure, 0, 127, "Zone Pressure CC");
        zone.Left = _configValueParser.ReadDouble(state.ZoneLeft, 0.0, 1.0, "Zone Left");
        zone.Top = _configValueParser.ReadDouble(state.ZoneTop, 0.0, 1.0, "Zone Top");
        zone.Width = _configValueParser.ReadDouble(state.ZoneWidth, 0.0, 1.0, "Zone Width");
        zone.Height = _configValueParser.ReadDouble(state.ZoneHeight, 0.0, 1.0, "Zone Height");
        zone.SendX = state.ZoneSendX;
        zone.SendY = state.ZoneSendY;
        zone.InvertX = state.ZoneInvertX;
        zone.InvertY = state.ZoneInvertY;
        zone.SendPressure = state.ZoneSendPressure;
        zone.ReleaseBehavior = _configValueParser.NormalizeReleaseBehavior(state.ZoneReleaseBehavior);
        zone.ResetPressureOnRelease = string.Equals(zone.ReleaseBehavior, ZoneReleaseBehaviors.ResetPressureOnly, StringComparison.Ordinal);
        SetZoneSelectionSummary(state, zone);
    }

    /// <inheritdoc />
    public void SetZoneSelectionSummary(IZoneEditorState state, ZoneConfig? zone)
    {
        if (zone is null)
        {
            state.ZoneSelectionSummaryPrimaryText = "No zone selected";
            state.ZoneSelectionSummarySecondaryText = "Select a zone to edit its routing and bounds.";
            return;
        }

        string enabled = zone.Enabled ? "Enabled" : "Disabled";
        state.ZoneSelectionSummaryPrimaryText =
            $"Channel {zone.MidiChannel}  •  CC X/Y/P {zone.CcX}/{zone.CcY}/{zone.CcPressure}  •  {enabled}";
        state.ZoneSelectionSummarySecondaryText =
            $"Bounds {zone.Left:0.###}, {zone.Top:0.###}, {zone.Width:0.###}, {zone.Height:0.###}  •  " +
            $"Release {_configValueParser.NormalizeReleaseBehavior(zone.ReleaseBehavior)}";
    }
}
