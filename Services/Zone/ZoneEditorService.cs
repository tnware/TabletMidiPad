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
    private readonly IPageNavigationService _pageNavigationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZoneEditorService"/> class.
    /// </summary>
    public ZoneEditorService(
        IConfigValueParser configValueParser,
        IPageNavigationService pageNavigationService)
    {
        _configValueParser = configValueParser ?? throw new ArgumentNullException(nameof(configValueParser));
        _pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));
    }

    /// <inheritdoc />
    public void RefreshZoneList(ObservableCollection<ZoneListItemViewModel> target, AppConfig config)
    {
        PageConfig page = _pageNavigationService.GetActivePage(config);
        target.Clear();
        for (int i = 0; i < page.Zones.Count; i++)
        {
            ZoneConfig zone = page.Zones[i];
            string enabled = zone.Enabled ? "on" : "off";
            target.Add(new ZoneListItemViewModel
            {
                DisplayIndex = i + 1,
                Index = i,
                Name = zone.Name,
                IsEnabled = zone.Enabled,
                DisplayText = $"{i + 1}. {zone.Name} [{enabled}]",
            });
        }
    }

    /// <inheritdoc />
    public void LoadSelectedZoneIntoEditor(IZoneEditorState viewModel, AppConfig config, int selectedZoneIndex)
    {
        PageConfig page = _pageNavigationService.GetActivePage(config);
        if (selectedZoneIndex < 0 || selectedZoneIndex >= page.Zones.Count)
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
            viewModel.ZoneOutputMode = ZoneOutputModes.CcPad;
            viewModel.ScaleBreathRootText = "0";
            viewModel.ScaleBreathScale = ScaleBreathScaleNames.Major;
            viewModel.ScaleBreathOctaveText = "4";
            viewModel.ScaleBreathHoleCountText = "10";
            viewModel.ScaleBreathRowSplitText = "0.5";
            viewModel.ScaleBreathSendCc = true;
            viewModel.ScaleGridRootText = "0";
            viewModel.ScaleGridScale = ScaleBreathScaleNames.Major;
            viewModel.ScaleGridOctaveText = "4";
            viewModel.ScaleGridRowsText = "4";
            viewModel.ScaleGridColumnsText = "4";
            viewModel.ScaleGridSendCc = true;
            return;
        }

        ZoneConfig zone = page.Zones[selectedZoneIndex];
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
        viewModel.ZoneOutputMode = _configValueParser.NormalizeZoneOutputMode(zone.OutputMode);
        viewModel.ScaleBreathRootText = Math.Clamp(zone.ScaleBreathRoot, 0, 11).ToString(CultureInfo.InvariantCulture);
        viewModel.ScaleBreathScale = _configValueParser.NormalizeScaleBreathScale(zone.ScaleBreathScale);
        viewModel.ScaleBreathOctaveText = Math.Clamp(zone.ScaleBreathOctave, 0, 8).ToString(CultureInfo.InvariantCulture);
        viewModel.ScaleBreathHoleCountText = Math.Clamp(zone.ScaleBreathHoleCount, 4, 16).ToString(CultureInfo.InvariantCulture);
        viewModel.ScaleBreathRowSplitText = Math.Clamp(zone.ScaleBreathRowSplit, 0.1, 0.9).ToString("0.###", CultureInfo.InvariantCulture);
        viewModel.ScaleBreathSendCc = zone.ScaleBreathSendCc;
        viewModel.ScaleGridRootText = Math.Clamp(zone.ScaleGridRoot, 0, 11).ToString(CultureInfo.InvariantCulture);
        viewModel.ScaleGridScale = _configValueParser.NormalizeScaleBreathScale(zone.ScaleGridScale);
        viewModel.ScaleGridOctaveText = Math.Clamp(zone.ScaleGridOctave, 0, 8).ToString(CultureInfo.InvariantCulture);
        viewModel.ScaleGridRowsText = Math.Clamp(zone.ScaleGridRows, 1, 16).ToString(CultureInfo.InvariantCulture);
        viewModel.ScaleGridColumnsText = Math.Clamp(zone.ScaleGridColumns, 1, 16).ToString(CultureInfo.InvariantCulture);
        viewModel.ScaleGridSendCc = zone.ScaleGridSendCc;
    }

    /// <inheritdoc />
    public void CommitCurrentZoneEdits(
        AppConfig config,
        IZoneEditorState viewModel,
        int selectedZoneIndex,
        string? maxZoneRowsText,
        string? maxZonesPerRowText)
    {
        PageConfig page = _pageNavigationService.GetActivePage(config);
        if (selectedZoneIndex < 0 || selectedZoneIndex >= page.Zones.Count)
        {
            return;
        }

        ZoneConfig zone = page.Zones[selectedZoneIndex];
        page.MaxZoneRows = _configValueParser.ReadInt(maxZoneRowsText, 1, 16, "Max Zone Rows");
        page.MaxZonesPerRow = _configValueParser.ReadInt(maxZonesPerRowText, 1, 32, "Max Zones Per Row");
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
        zone.OutputMode = _configValueParser.NormalizeZoneOutputMode(viewModel.ZoneOutputMode);
        zone.ScaleBreathRoot = _configValueParser.ReadInt(viewModel.ScaleBreathRootText, 0, 11, "Scale Breath Root");
        zone.ScaleBreathScale = _configValueParser.NormalizeScaleBreathScale(viewModel.ScaleBreathScale);
        zone.ScaleBreathOctave = _configValueParser.ReadInt(viewModel.ScaleBreathOctaveText, 0, 8, "Scale Breath Octave");
        zone.ScaleBreathHoleCount = _configValueParser.ReadInt(viewModel.ScaleBreathHoleCountText, 4, 16, "Scale Breath Hole Count");
        zone.ScaleBreathRowSplit = _configValueParser.ReadDouble(viewModel.ScaleBreathRowSplitText, 0.1, 0.9, "Scale Breath Row Split");
        zone.ScaleBreathSendCc = viewModel.ScaleBreathSendCc;
        zone.ScaleGridRoot = _configValueParser.ReadInt(viewModel.ScaleGridRootText, 0, 11, "Scale Grid Root");
        zone.ScaleGridScale = _configValueParser.NormalizeScaleBreathScale(viewModel.ScaleGridScale);
        zone.ScaleGridOctave = _configValueParser.ReadInt(viewModel.ScaleGridOctaveText, 0, 8, "Scale Grid Octave");
        zone.ScaleGridRows = _configValueParser.ReadInt(viewModel.ScaleGridRowsText, 1, 16, "Scale Grid Rows");
        zone.ScaleGridColumns = _configValueParser.ReadInt(viewModel.ScaleGridColumnsText, 1, 16, "Scale Grid Columns");
        zone.ScaleGridSendCc = viewModel.ScaleGridSendCc;
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
            $"Channel {zone.MidiChannel}  •  Mode {_configValueParser.NormalizeZoneOutputMode(zone.OutputMode)}  •  {enabled}";
        viewModel.ZoneSelectionSummarySecondaryText =
            $"Bounds {zone.Left:0.###}, {zone.Top:0.###}, {zone.Width:0.###}, {zone.Height:0.###}  •  " +
            $"CC X/Y/P {zone.CcX}/{zone.CcY}/{zone.CcPressure}  •  " +
            $"Release {_configValueParser.NormalizeReleaseBehavior(zone.ReleaseBehavior)}";
    }
}
