using System;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

internal sealed class MainViewModelFactory : IMainViewModelFactory
{
    private readonly IRuntimeConfigService _runtimeConfig;
    private readonly IZoneEditorService _zoneEditorService;
    private readonly IZoneActionWorkflowCoordinator _zoneActionWorkflowCoordinator;
    private readonly IToolbarConfigService _toolbarConfigService;
    private readonly IWorkflowCoordinator _workflowCoordinator;

    public MainViewModelFactory(
        IRuntimeConfigService runtimeConfig,
        IZoneEditorService zoneEditorService,
        IZoneActionWorkflowCoordinator zoneActionWorkflowCoordinator,
        IToolbarConfigService toolbarConfigService,
        IWorkflowCoordinator workflowCoordinator)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _zoneEditorService = zoneEditorService ?? throw new ArgumentNullException(nameof(zoneEditorService));
        _zoneActionWorkflowCoordinator = zoneActionWorkflowCoordinator ?? throw new ArgumentNullException(nameof(zoneActionWorkflowCoordinator));
        _toolbarConfigService = toolbarConfigService ?? throw new ArgumentNullException(nameof(toolbarConfigService));
        _workflowCoordinator = workflowCoordinator ?? throw new ArgumentNullException(nameof(workflowCoordinator));
    }

    public MainViewModel Create(MainViewModelActions actions)
    {
        ZoneEditorViewModel zoneEditor = new(_runtimeConfig, _zoneEditorService);
        ZoneListViewModel zoneList = new(_runtimeConfig, _zoneActionWorkflowCoordinator, zoneEditor);
        ToolbarViewModel toolbar = new(_runtimeConfig, _toolbarConfigService, _workflowCoordinator);
        return new MainViewModel(
            zoneList,
            zoneEditor,
            toolbar,
            actions.SaveSettings,
            actions.ResetDefaults,
            actions.OpenPreferences,
            actions.Exit,
            actions.RefreshWacomStatus,
            actions.ReconnectPen,
            actions.StartWacomService,
            actions.ShowAbout);
    }
}
