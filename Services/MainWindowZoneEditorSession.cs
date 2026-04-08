using System;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Default zone editor / config UI sync for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class MainWindowZoneEditorSession : IMainWindowZoneEditorSession
{
    private readonly IMainWindowZoneEditorSessionHost _host;
    private readonly IZoneEditorService _zoneEditorService;
    private readonly IMainWindowWorkflowCoordinator _workflowCoordinator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowZoneEditorSession"/> class.
    /// </summary>
    public MainWindowZoneEditorSession(
        IMainWindowZoneEditorSessionHost host,
        IZoneEditorService zoneEditorService,
        IMainWindowWorkflowCoordinator workflowCoordinator)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _zoneEditorService = zoneEditorService ?? throw new ArgumentNullException(nameof(zoneEditorService));
        _workflowCoordinator = workflowCoordinator ?? throw new ArgumentNullException(nameof(workflowCoordinator));
    }

    /// <inheritdoc />
    public void LoadConfigIntoUi(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _host.IsLoadingUi = true;

        MainViewModel vm = _host.ViewModel;
        vm.MaxZoneRowsText = config.MaxZoneRows.ToString();
        vm.MaxZonesPerRowText = config.MaxZonesPerRow.ToString();
        RefreshZoneList();
        _host.SelectedZoneIndex = config.Zones.Count > 0 ? 0 : -1;
        vm.NotifySelectedZoneIndexChanged();
        LoadSelectedZoneIntoEditor();
        _host.SyncToolbar();

        _host.IsLoadingUi = false;
        _host.RefreshPadZoneOverlay();
    }

    /// <inheritdoc />
    public void LoadSelectedZoneIntoEditor()
    {
        _host.IsLoadingUi = true;
        _zoneEditorService.LoadSelectedZoneIntoEditor(_host.ViewModel, _host.Config, _host.SelectedZoneIndex);
        _host.IsLoadingUi = false;
    }

    /// <inheritdoc />
    public void CommitCurrentZoneEdits()
    {
        MainViewModel vm = _host.ViewModel;
        _zoneEditorService.CommitCurrentZoneEdits(
            _host.Config,
            vm,
            _host.SelectedZoneIndex,
            vm.MaxZoneRowsText,
            vm.MaxZonesPerRowText);

        RefreshZoneList();
        vm.NotifySelectedZoneIndexChanged();
    }

    /// <inheritdoc />
    public void RefreshZoneList()
    {
        _zoneEditorService.RefreshZoneList(_host.ViewModel.Zones, _host.Config);
    }

    /// <inheritdoc />
    public void UpdateUiForCurrentConfig()
    {
        MainWindowConnectedState statusState = _workflowCoordinator.BuildConnectedState(_host.Config);
        MainViewModel vm = _host.ViewModel;
        vm.StatusText = statusState.StatusText;
        vm.RoutingText = statusState.RoutingText;
        _host.RefreshInputBackendStatus();
        vm.ActiveZoneText = statusState.ActiveZoneText;
        vm.ConfigSummaryText = statusState.ConfigSummaryText;
        vm.ValueText = statusState.ValueText;
        vm.PenButtonsText = statusState.PenButtonsText;
        vm.ModifiersText = statusState.ModifiersText;
    }

    /// <inheritdoc />
    public void OnZoneListSelectionChanged()
    {
        if (_host.IsLoadingUi)
        {
            return;
        }

        LoadSelectedZoneIntoEditor();
        _host.RefreshPadZoneOverlay();
    }
}
