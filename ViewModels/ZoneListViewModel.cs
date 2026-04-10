using System.Windows.Input;

namespace TabletMidiPad.ViewModels;

internal sealed class ZoneListViewModel : ViewModelBase
{
    private readonly IRuntimeConfigService _runtimeConfig;
    private readonly IZoneActionWorkflowCoordinator _zoneActions;
    private readonly IPageNavigationService _pageNavigationService;
    private readonly ZoneEditorViewModel _zoneEditor;

    public ZoneListViewModel(
        IRuntimeConfigService runtimeConfig,
        IZoneActionWorkflowCoordinator zoneActions,
        IPageNavigationService pageNavigationService,
        ZoneEditorViewModel zoneEditor)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _zoneActions = zoneActions ?? throw new ArgumentNullException(nameof(zoneActions));
        _pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));
        _zoneEditor = zoneEditor ?? throw new ArgumentNullException(nameof(zoneEditor));
        AddZoneCommand = new DelegateCommand(AddZone);
        RemoveZoneCommand = new DelegateCommand(RemoveZone);
        AutoLayoutCommand = new DelegateCommand(AutoLayoutZones);
        AddPageCommand = new DelegateCommand(AddPage);
        RemovePageCommand = new DelegateCommand(RemovePage);
        NextPageCommand = new DelegateCommand(NextPage);
        PreviousPageCommand = new DelegateCommand(PreviousPage);
    }

    public event Action? PadZoneOverlayRefreshRequested;

    public event Action<string, SettingsResultKind>? SettingsResultRequested;

    public ICommand AddZoneCommand { get; }

    public ICommand RemoveZoneCommand { get; }

    public ICommand AutoLayoutCommand { get; }

    public ICommand AddPageCommand { get; }

    public ICommand RemovePageCommand { get; }

    public ICommand NextPageCommand { get; }

    public ICommand PreviousPageCommand { get; }

    private void AutoLayoutZones()
    {
        _zoneEditor.CommitCurrentZoneEdits();
        ZoneActionWorkflowResult result = _zoneActions.AutoLayout(_runtimeConfig.Current, _zoneEditor.SelectedZoneIndex);
        ApplyZoneActionResult(result);
    }

    private void AddZone()
    {
        _zoneEditor.CommitCurrentZoneEdits();
        ZoneActionWorkflowResult result = _zoneActions.AddZone(_runtimeConfig.Current);
        ApplyZoneActionResult(result);
    }

    private void RemoveZone()
    {
        ZoneActionWorkflowResult result = _zoneActions.RemoveZone(_runtimeConfig.Current, _zoneEditor.SelectedZoneIndex);
        if (!result.Changed)
        {
            if (!string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                SettingsResultRequested?.Invoke(result.StatusMessage, SettingsResultKind.Error);
            }

            return;
        }

        ApplyZoneActionResult(result);
    }

    private void ApplyZoneActionResult(ZoneActionWorkflowResult result)
    {
        if (result.Changed)
        {
            _zoneEditor.RefreshZoneList();
            _zoneEditor.SelectedZoneIndex = result.SelectedZoneIndex;
            _zoneEditor.NotifySelectedZoneIndexChanged();
            _zoneEditor.LoadSelectedZoneIntoEditor();
            PadZoneOverlayRefreshRequested?.Invoke();
        }

        if (!string.IsNullOrWhiteSpace(result.StatusMessage))
        {
            SettingsResultKind kind = result.Changed
                ? SettingsResultKind.Success
                : SettingsResultKind.Error;
            SettingsResultRequested?.Invoke(result.StatusMessage, kind);
        }
    }

    private void AddPage()
    {
        _zoneEditor.CommitCurrentZoneEdits();
        _zoneEditor.CommitCurrentPageEdits();
        PageSelectionResult result = _pageNavigationService.AddPage(_runtimeConfig.Current);
        ApplyPageActionResult(result);
    }

    private void RemovePage()
    {
        _zoneEditor.CommitCurrentZoneEdits();
        _zoneEditor.CommitCurrentPageEdits();
        PageSelectionResult result = _pageNavigationService.RemovePage(_runtimeConfig.Current, _zoneEditor.SelectedPageIndex);
        if (!result.Changed)
        {
            if (!string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                SettingsResultRequested?.Invoke(result.StatusMessage, SettingsResultKind.Error);
            }

            return;
        }

        ApplyPageActionResult(result);
    }

    private void NextPage()
    {
        _zoneEditor.CommitCurrentZoneEdits();
        _zoneEditor.CommitCurrentPageEdits();
        ApplyPageNavigation(_pageNavigationService.SelectNext(_runtimeConfig.Current));
    }

    private void PreviousPage()
    {
        _zoneEditor.CommitCurrentZoneEdits();
        _zoneEditor.CommitCurrentPageEdits();
        ApplyPageNavigation(_pageNavigationService.SelectPrevious(_runtimeConfig.Current));
    }

    private void ApplyPageNavigation(PageSelectionResult result)
    {
        if (!result.Changed)
        {
            if (!string.IsNullOrWhiteSpace(result.StatusMessage))
            {
                SettingsResultRequested?.Invoke(result.StatusMessage, SettingsResultKind.Error);
            }

            return;
        }

        ApplyPageActionResult(result);
    }

    private void ApplyPageActionResult(PageSelectionResult result)
    {
        _zoneEditor.ApplyActivePageFromWorkflow(result.ActivePageIndex, resetZoneSelection: true);
        PadZoneOverlayRefreshRequested?.Invoke();
        if (!string.IsNullOrWhiteSpace(result.StatusMessage))
        {
            SettingsResultRequested?.Invoke(result.StatusMessage, SettingsResultKind.Success);
        }
    }
}
