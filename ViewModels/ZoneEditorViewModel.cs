using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

internal sealed class ZoneEditorViewModel : ViewModelBase, Services.IZoneEditorState
{
    private readonly ISettingsService _settingsService;
    private readonly IZoneEditorService _zoneEditorService;
    private readonly ZoneListViewModel _zoneListViewModel;

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
    private bool _zoneDetailsEnabled;
    private string _zoneSelectionSummaryPrimaryText = "No zone selected";
    private string _zoneSelectionSummarySecondaryText = "Select a zone to edit its routing and bounds.";

    public ZoneEditorViewModel(
        ISettingsService settingsService,
        IZoneEditorService zoneEditorService,
        ZoneListViewModel zoneListViewModel)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _zoneEditorService = zoneEditorService ?? throw new ArgumentNullException(nameof(zoneEditorService));
        _zoneListViewModel = zoneListViewModel ?? throw new ArgumentNullException(nameof(zoneListViewModel));

        for (int ch = 1; ch <= 16; ch++)
        {
            MidiChannels.Add(ch);
        }

        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.NoSendOnRelease);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.Hold);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.ResetPressureOnly);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.ResetAll);

        _zoneListViewModel.OnSelectedZoneChanged += (s, e) => LoadSelectedZone();
        _settingsService.SettingsUpdated += (s, e) => LoadSelectedZone();
    }

    public ObservableCollection<int> MidiChannels { get; } = new();
    public ObservableCollection<string> ReleaseBehaviorOptions { get; } = new();

    public string ZoneName { get => _zoneName; set => SetProperty(ref _zoneName, value); }
    public bool ZoneEnabled { get => _zoneEnabled; set => SetProperty(ref _zoneEnabled, value); }
    public int ZoneMidiChannel { get => _zoneMidiChannel; set => SetProperty(ref _zoneMidiChannel, value); }
    public string ZoneCcX { get => _zoneCcX; set => SetProperty(ref _zoneCcX, value); }
    public string ZoneCcY { get => _zoneCcY; set => SetProperty(ref _zoneCcY, value); }
    public string ZoneCcPressure { get => _zoneCcPressure; set => SetProperty(ref _zoneCcPressure, value); }
    public string ZoneLeft { get => _zoneLeft; set => SetProperty(ref _zoneLeft, value); }
    public string ZoneTop { get => _zoneTop; set => SetProperty(ref _zoneTop, value); }
    public string ZoneWidth { get => _zoneWidth; set => SetProperty(ref _zoneWidth, value); }
    public string ZoneHeight { get => _zoneHeight; set => SetProperty(ref _zoneHeight, value); }
    public bool ZoneSendX { get => _zoneSendX; set => SetProperty(ref _zoneSendX, value); }
    public bool ZoneSendY { get => _zoneSendY; set => SetProperty(ref _zoneSendY, value); }
    public bool ZoneInvertX { get => _zoneInvertX; set => SetProperty(ref _zoneInvertX, value); }
    public bool ZoneInvertY { get => _zoneInvertY; set => SetProperty(ref _zoneInvertY, value); }
    public bool ZoneSendPressure { get => _zoneSendPressure; set => SetProperty(ref _zoneSendPressure, value); }
    public string ZoneReleaseBehavior { get => _zoneReleaseBehavior; set => SetProperty(ref _zoneReleaseBehavior, value); }
    public bool ZoneDetailsEnabled { get => _zoneDetailsEnabled; set => SetProperty(ref _zoneDetailsEnabled, value); }
    public string ZoneSelectionSummaryPrimaryText { get => _zoneSelectionSummaryPrimaryText; set => SetProperty(ref _zoneSelectionSummaryPrimaryText, value); }
    public string ZoneSelectionSummarySecondaryText { get => _zoneSelectionSummarySecondaryText; set => SetProperty(ref _zoneSelectionSummarySecondaryText, value); }

    public void LoadSelectedZone()
    {
        _zoneEditorService.LoadSelectedZoneIntoEditor(this, _settingsService.Config, _zoneListViewModel.SelectedZoneIndex);
    }

    public void Commit(string maxZoneRowsText, string maxZonesPerRowText)
    {
        _zoneEditorService.CommitCurrentZoneEdits(_settingsService.Config, this, _zoneListViewModel.SelectedZoneIndex, maxZoneRowsText, maxZonesPerRowText);
    }
}
