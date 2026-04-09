using System.Collections.ObjectModel;
using System.Globalization;

namespace TabletMidiPad.ViewModels;

internal sealed class ZoneEditorViewModel : ViewModelBase, IZoneEditorState
{
    private readonly IRuntimeConfigService _runtimeConfig;
    private readonly IZoneEditorService _zoneEditorService;
    private string _zoneSelectionSummaryPrimaryText = "No zone selected";
    private string _zoneSelectionSummarySecondaryText = "Select a zone to edit its routing and bounds.";
    private int _selectedZoneIndex = -1;
    private bool _zoneDetailsEnabled;
    private string _zoneName = string.Empty;
    private bool _zoneEnabled;
    private int _zoneMidiChannel = 1;
    private string _zoneCcX = string.Empty;
    private string _zoneCcY = string.Empty;
    private string _zoneCcPressure = string.Empty;
    private string _zoneLeft = string.Empty;
    private string _zoneTop = string.Empty;
    private string _zoneWidth = string.Empty;
    private string _zoneHeight = string.Empty;
    private bool _zoneSendX = true;
    private bool _zoneSendY = true;
    private bool _zoneInvertX;
    private bool _zoneInvertY;
    private bool _zoneSendPressure = true;
    private string _zoneReleaseBehavior = string.Empty;
    private string _maxZoneRowsText = string.Empty;
    private string _maxZonesPerRowText = string.Empty;

    public ZoneEditorViewModel(IRuntimeConfigService runtimeConfig, IZoneEditorService zoneEditorService)
    {
        _runtimeConfig = runtimeConfig;
        _zoneEditorService = zoneEditorService;

        for (int ch = 1; ch <= 16; ch++)
        {
            MidiChannels.Add(ch);
        }

        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.NoSendOnRelease);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.Hold);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.ResetPressureOnly);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.ResetAll);
    }

    public ObservableCollection<int> MidiChannels { get; } = new();

    public ObservableCollection<string> ReleaseBehaviorOptions { get; } = new();

    public ObservableCollection<ZoneListItemViewModel> Zones { get; } = new();

    public bool IsLoadingUi { get; private set; }

    public string MaxZoneRowsText
    {
        get => _maxZoneRowsText;
        set => SetProperty(ref _maxZoneRowsText, value);
    }

    public string MaxZonesPerRowText
    {
        get => _maxZonesPerRowText;
        set => SetProperty(ref _maxZonesPerRowText, value);
    }

    public string ZoneSelectionSummaryPrimaryText
    {
        get => _zoneSelectionSummaryPrimaryText;
        set => SetProperty(ref _zoneSelectionSummaryPrimaryText, value);
    }

    public string ZoneSelectionSummarySecondaryText
    {
        get => _zoneSelectionSummarySecondaryText;
        set => SetProperty(ref _zoneSelectionSummarySecondaryText, value);
    }

    public int SelectedZoneIndex
    {
        get => _selectedZoneIndex;
        set => SetProperty(ref _selectedZoneIndex, value);
    }

    public bool ZoneDetailsEnabled
    {
        get => _zoneDetailsEnabled;
        set => SetProperty(ref _zoneDetailsEnabled, value);
    }

    public string ZoneName
    {
        get => _zoneName;
        set => SetProperty(ref _zoneName, value);
    }

    public bool ZoneEnabled
    {
        get => _zoneEnabled;
        set => SetProperty(ref _zoneEnabled, value);
    }

    public int ZoneMidiChannel
    {
        get => _zoneMidiChannel;
        set => SetProperty(ref _zoneMidiChannel, value);
    }

    public string ZoneCcX
    {
        get => _zoneCcX;
        set => SetProperty(ref _zoneCcX, value);
    }

    public string ZoneCcY
    {
        get => _zoneCcY;
        set => SetProperty(ref _zoneCcY, value);
    }

    public string ZoneCcPressure
    {
        get => _zoneCcPressure;
        set => SetProperty(ref _zoneCcPressure, value);
    }

    public string ZoneLeft
    {
        get => _zoneLeft;
        set => SetProperty(ref _zoneLeft, value);
    }

    public string ZoneTop
    {
        get => _zoneTop;
        set => SetProperty(ref _zoneTop, value);
    }

    public string ZoneWidth
    {
        get => _zoneWidth;
        set => SetProperty(ref _zoneWidth, value);
    }

    public string ZoneHeight
    {
        get => _zoneHeight;
        set => SetProperty(ref _zoneHeight, value);
    }

    public bool ZoneSendX
    {
        get => _zoneSendX;
        set => SetProperty(ref _zoneSendX, value);
    }

    public bool ZoneSendY
    {
        get => _zoneSendY;
        set => SetProperty(ref _zoneSendY, value);
    }

    public bool ZoneInvertX
    {
        get => _zoneInvertX;
        set => SetProperty(ref _zoneInvertX, value);
    }

    public bool ZoneInvertY
    {
        get => _zoneInvertY;
        set => SetProperty(ref _zoneInvertY, value);
    }

    public bool ZoneSendPressure
    {
        get => _zoneSendPressure;
        set => SetProperty(ref _zoneSendPressure, value);
    }

    public string ZoneReleaseBehavior
    {
        get => _zoneReleaseBehavior;
        set => SetProperty(ref _zoneReleaseBehavior, value);
    }

    public void NotifySelectedZoneIndexChanged() => OnPropertyChanged(nameof(SelectedZoneIndex));

    public void LoadConfigIntoUi()
    {
        AppConfig config = _runtimeConfig.Current;
        IsLoadingUi = true;
        MaxZoneRowsText = config.MaxZoneRows.ToString(CultureInfo.InvariantCulture);
        MaxZonesPerRowText = config.MaxZonesPerRow.ToString(CultureInfo.InvariantCulture);
        RefreshZoneList();
        SelectedZoneIndex = config.Zones.Count > 0 ? 0 : -1;
        NotifySelectedZoneIndexChanged();
        LoadSelectedZoneIntoEditor();
        IsLoadingUi = false;
    }

    public void LoadSelectedZoneIntoEditor()
    {
        IsLoadingUi = true;
        _zoneEditorService.LoadSelectedZoneIntoEditor(this, _runtimeConfig.Current, SelectedZoneIndex);
        IsLoadingUi = false;
    }

    public void CommitCurrentZoneEdits()
    {
        _zoneEditorService.CommitCurrentZoneEdits(
            _runtimeConfig.Current,
            this,
            SelectedZoneIndex,
            MaxZoneRowsText,
            MaxZonesPerRowText);
        RefreshZoneList();
        NotifySelectedZoneIndexChanged();
    }

    public void RefreshZoneList()
        => _zoneEditorService.RefreshZoneList(Zones, _runtimeConfig.Current);

    public bool OnZoneListSelectionChanged()
    {
        if (IsLoadingUi)
        {
            return false;
        }

        LoadSelectedZoneIntoEditor();
        return true;
    }
}
