using System.Collections.ObjectModel;
using System.Globalization;

namespace TabletMidiPad.ViewModels;

internal sealed class ZoneEditorViewModel : ViewModelBase, IZoneEditorState
{
    private readonly IRuntimeConfigService _runtimeConfig;
    private readonly IZoneEditorService _zoneEditorService;
    private readonly IPageNavigationService _pageNavigationService;
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
    private string _zoneOutputMode = ZoneOutputModes.CcPad;
    private string _scaleBreathRootText = "0";
    private string _scaleBreathScale = ScaleBreathScaleNames.Major;
    private string _scaleBreathOctaveText = "4";
    private string _scaleBreathHoleCountText = "10";
    private string _scaleBreathRowSplitText = "0.5";
    private bool _scaleBreathSendCc = true;
    private string _scaleGridRootText = "0";
    private string _scaleGridScale = ScaleBreathScaleNames.Major;
    private string _scaleGridOctaveText = "4";
    private string _scaleGridRowsText = "4";
    private string _scaleGridColumnsText = "4";
    private bool _scaleGridSendCc = true;
    private string _maxZoneRowsText = string.Empty;
    private string _maxZonesPerRowText = string.Empty;
    private bool _isCanvasLocked;
    private int _selectedPageIndex;
    private string _pageName = string.Empty;

    public ZoneEditorViewModel(
        IRuntimeConfigService runtimeConfig,
        IZoneEditorService zoneEditorService,
        IPageNavigationService pageNavigationService)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _zoneEditorService = zoneEditorService ?? throw new ArgumentNullException(nameof(zoneEditorService));
        _pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));

        for (int ch = 1; ch <= 16; ch++)
        {
            MidiChannels.Add(ch);
        }

        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.NoSendOnRelease);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.Hold);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.ResetPressureOnly);
        ReleaseBehaviorOptions.Add(ZoneReleaseBehaviors.ResetAll);
        ZoneOutputModeOptions.Add(ZoneOutputModes.CcPad);
        ZoneOutputModeOptions.Add(ZoneOutputModes.ScaleBreath);
        ZoneOutputModeOptions.Add(ZoneOutputModes.ScaleGrid);
        ScaleBreathScaleOptions.Add(ScaleBreathScaleNames.Major);
        ScaleBreathScaleOptions.Add(ScaleBreathScaleNames.NaturalMinor);
        ScaleBreathScaleOptions.Add(ScaleBreathScaleNames.MajorPentatonic);
        ScaleBreathScaleOptions.Add(ScaleBreathScaleNames.MinorPentatonic);
    }

    public ObservableCollection<int> MidiChannels { get; } = new();

    public ObservableCollection<string> ReleaseBehaviorOptions { get; } = new();

    public ObservableCollection<string> ZoneOutputModeOptions { get; } = new();

    public ObservableCollection<string> ScaleBreathScaleOptions { get; } = new();

    public ObservableCollection<PageListItemViewModel> Pages { get; } = new();

    public ObservableCollection<ZoneListItemViewModel> Zones { get; } = new();

    public bool IsLoadingUi { get; private set; }

    public int SelectedPageIndex
    {
        get => _selectedPageIndex;
        set => SetProperty(ref _selectedPageIndex, value);
    }

    public string PageName
    {
        get => _pageName;
        set => SetProperty(ref _pageName, value);
    }

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

    public bool IsCanvasLocked
    {
        get => _isCanvasLocked;
        set
        {
            if (!SetProperty(ref _isCanvasLocked, value))
            {
                return;
            }

            if (value && SelectedZoneIndex >= 0)
            {
                SelectedZoneIndex = -1;
                LoadSelectedZoneIntoEditor();
            }
        }
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

    public string ZoneOutputMode
    {
        get => _zoneOutputMode;
        set
        {
            if (!SetProperty(ref _zoneOutputMode, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsCcPadMode));
            OnPropertyChanged(nameof(IsScaleBreathMode));
            OnPropertyChanged(nameof(IsScaleGridMode));
        }
    }

    public string ScaleBreathRootText
    {
        get => _scaleBreathRootText;
        set => SetProperty(ref _scaleBreathRootText, value);
    }

    public string ScaleBreathScale
    {
        get => _scaleBreathScale;
        set => SetProperty(ref _scaleBreathScale, value);
    }

    public string ScaleBreathOctaveText
    {
        get => _scaleBreathOctaveText;
        set => SetProperty(ref _scaleBreathOctaveText, value);
    }

    public string ScaleBreathHoleCountText
    {
        get => _scaleBreathHoleCountText;
        set => SetProperty(ref _scaleBreathHoleCountText, value);
    }

    public string ScaleBreathRowSplitText
    {
        get => _scaleBreathRowSplitText;
        set => SetProperty(ref _scaleBreathRowSplitText, value);
    }

    public bool ScaleBreathSendCc
    {
        get => _scaleBreathSendCc;
        set => SetProperty(ref _scaleBreathSendCc, value);
    }

    public string ScaleGridRootText
    {
        get => _scaleGridRootText;
        set => SetProperty(ref _scaleGridRootText, value);
    }

    public string ScaleGridScale
    {
        get => _scaleGridScale;
        set => SetProperty(ref _scaleGridScale, value);
    }

    public string ScaleGridOctaveText
    {
        get => _scaleGridOctaveText;
        set => SetProperty(ref _scaleGridOctaveText, value);
    }

    public string ScaleGridRowsText
    {
        get => _scaleGridRowsText;
        set => SetProperty(ref _scaleGridRowsText, value);
    }

    public string ScaleGridColumnsText
    {
        get => _scaleGridColumnsText;
        set => SetProperty(ref _scaleGridColumnsText, value);
    }

    public bool ScaleGridSendCc
    {
        get => _scaleGridSendCc;
        set => SetProperty(ref _scaleGridSendCc, value);
    }

    public bool IsCcPadMode => string.Equals(ZoneOutputMode, ZoneOutputModes.CcPad, StringComparison.Ordinal);

    public bool IsScaleBreathMode => string.Equals(ZoneOutputMode, ZoneOutputModes.ScaleBreath, StringComparison.Ordinal);

    public bool IsScaleGridMode => string.Equals(ZoneOutputMode, ZoneOutputModes.ScaleGrid, StringComparison.Ordinal);

    public void NotifySelectedZoneIndexChanged() => OnPropertyChanged(nameof(SelectedZoneIndex));

    public void LoadConfigIntoUi()
    {
        AppConfig config = _runtimeConfig.Current;
        _pageNavigationService.Initialize(config);
        IsLoadingUi = true;
        RefreshPageList();
        SelectedPageIndex = _pageNavigationService.ActivePageIndex;
        NotifySelectedPageIndexChanged();
        LoadSelectedPageIntoEditor();
        RefreshZoneList();
        PageConfig page = _pageNavigationService.GetActivePage(config);
        SelectedZoneIndex = page.Zones.Count > 0 ? 0 : -1;
        NotifySelectedZoneIndexChanged();
        LoadSelectedZoneIntoEditor();
        IsLoadingUi = false;
    }

    public void NotifySelectedPageIndexChanged() => OnPropertyChanged(nameof(SelectedPageIndex));

    public void RefreshPageList()
    {
        Pages.Clear();
        AppConfig config = _runtimeConfig.Current;
        for (int i = 0; i < config.Pages.Count; i++)
        {
            PageConfig page = AppConfig.GetPage(config, i);
            Pages.Add(new PageListItemViewModel
            {
                Index = i,
                DisplayIndex = i + 1,
                Name = page.Name,
                DisplayText = $"{i + 1}. {page.Name}"
            });
        }
    }

    public void LoadSelectedPageIntoEditor()
    {
        IsLoadingUi = true;
        PageConfig page = _pageNavigationService.GetActivePage(_runtimeConfig.Current);
        SelectedPageIndex = _pageNavigationService.ActivePageIndex;
        PageName = page.Name;
        MaxZoneRowsText = page.MaxZoneRows.ToString(CultureInfo.InvariantCulture);
        MaxZonesPerRowText = page.MaxZonesPerRow.ToString(CultureInfo.InvariantCulture);
        IsLoadingUi = false;
    }

    public void CommitCurrentPageEdits()
    {
        AppConfig config = _runtimeConfig.Current;
        PageConfig page = _pageNavigationService.GetActivePage(config);
        page.Name = string.IsNullOrWhiteSpace(PageName)
            ? $"Page {_pageNavigationService.ActivePageIndex + 1}"
            : PageName.Trim();
    }

    public void LoadSelectedZoneIntoEditor()
    {
        IsLoadingUi = true;
        _zoneEditorService.LoadSelectedZoneIntoEditor(this, _runtimeConfig.Current, SelectedZoneIndex);
        IsLoadingUi = false;
    }

    public void CommitCurrentZoneEdits()
    {
        CommitCurrentPageEdits();
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

    public bool OnPageListSelectionChanged()
    {
        if (IsLoadingUi)
        {
            return false;
        }

        CommitCurrentPageEdits();
        CommitCurrentZoneEdits();
        _pageNavigationService.Select(_runtimeConfig.Current, SelectedPageIndex);
        ApplyActivePageFromWorkflow(_pageNavigationService.ActivePageIndex, resetZoneSelection: true);
        return true;
    }

    public void ApplyActivePageFromWorkflow(int activePageIndex, bool resetZoneSelection)
    {
        IsLoadingUi = true;
        SelectedPageIndex = activePageIndex;
        NotifySelectedPageIndexChanged();
        RefreshPageList();
        LoadSelectedPageIntoEditor();
        RefreshZoneList();
        if (resetZoneSelection)
        {
            PageConfig page = _pageNavigationService.GetActivePage(_runtimeConfig.Current);
            SelectedZoneIndex = page.Zones.Count > 0 ? 0 : -1;
        }

        NotifySelectedZoneIndexChanged();
        LoadSelectedZoneIntoEditor();
        IsLoadingUi = false;
    }
}
