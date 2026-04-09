using System.Collections.ObjectModel;
using System.Globalization;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Zone;

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
    public void LoadSelectedZoneIntoEditor(IZoneEditorState viewModel, AppConfig config, int selectedZoneIndex)
    {
        if (selectedZoneIndex < 0 || selectedZoneIndex >= config.Zones.Count)
        {
            viewModel.ZoneName = string.Empty;
            viewModel.ZoneEnabled = false;
            viewModel.ZoneMidiChannel = 1;
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
        viewModel.ZoneMidiChannel = zone.MidiChannel;
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
        viewModel.ZoneReleaseBehavior = _configValueParser.NormalizeReleaseBehavior(zone.ReleaseBehavior);
    }

    /// <inheritdoc />
    public void CommitCurrentZoneEdits(
        AppConfig config,
        IZoneEditorState viewModel,
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
        zone.Name = _configValueParser.RequireText(viewModel.ZoneName, "Zone Name");
        zone.Enabled = viewModel.ZoneEnabled;
        zone.MidiChannel = Math.Clamp(viewModel.ZoneMidiChannel, 1, 16);
        zone.CcX = _configValueParser.ReadInt(viewModel.ZoneCcX, 0, 127, "Zone CC X");
        zone.CcY = _configValueParser.ReadInt(viewModel.ZoneCcY, 0, 127, "Zone CC Y");
        zone.CcPressure = _configValueParser.ReadInt(viewModel.ZoneCcPressure, 0, 127, "Zone Pressure CC");
        zone.Left = _configValueParser.ReadDouble(viewModel.ZoneLeft, 0.0, 1.0, "Zone Left");
        zone.Top = _configValueParser.ReadDouble(viewModel.ZoneTop, 0.0, 1.0, "Zone Top");
        zone.Width = _configValueParser.ReadDouble(viewModel.ZoneWidth, 0.0, 1.0, "Zone Width");
        zone.Height = _configValueParser.ReadDouble(viewModel.ZoneHeight, 0.0, 1.0, "Zone Height");
        zone.SendX = viewModel.ZoneSendX;
        zone.SendY = viewModel.ZoneSendY;
        zone.InvertX = viewModel.ZoneInvertX;
        zone.InvertY = viewModel.ZoneInvertY;
        zone.SendPressure = viewModel.ZoneSendPressure;
        zone.ReleaseBehavior = _configValueParser.NormalizeReleaseBehavior(viewModel.ZoneReleaseBehavior);
        zone.ResetPressureOnRelease = string.Equals(zone.ReleaseBehavior, ZoneReleaseBehaviors.ResetPressureOnly, StringComparison.Ordinal);
        SetZoneSelectionSummary(viewModel, zone);
    }

    /// <inheritdoc />
    public void SetZoneSelectionSummary(IZoneEditorState viewModel, ZoneConfig? zone)
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
            $"Release {_configValueParser.NormalizeReleaseBehavior(zone.ReleaseBehavior)}";
    }
}
