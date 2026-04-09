namespace TabletMidiPad.ViewModels;

internal sealed class MainViewModelFactory : IMainViewModelFactory
{
    private readonly IRuntimeConfigService _runtimeConfig;
    private readonly IZoneEditorService _zoneEditorService;
    private readonly IZoneActionWorkflowCoordinator _zoneActionWorkflowCoordinator;
    private readonly IToolbarConfigService _toolbarConfigService;
    private readonly IUiStateProjectionService _uiStateProjectionService;
    private readonly IPageNavigationService _pageNavigationService;

    public MainViewModelFactory(
        IRuntimeConfigService runtimeConfig,
        IZoneEditorService zoneEditorService,
        IZoneActionWorkflowCoordinator zoneActionWorkflowCoordinator,
        IToolbarConfigService toolbarConfigService,
        IUiStateProjectionService uiStateProjectionService,
        IPageNavigationService pageNavigationService)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _zoneEditorService = zoneEditorService ?? throw new ArgumentNullException(nameof(zoneEditorService));
        _zoneActionWorkflowCoordinator = zoneActionWorkflowCoordinator ?? throw new ArgumentNullException(nameof(zoneActionWorkflowCoordinator));
        _toolbarConfigService = toolbarConfigService ?? throw new ArgumentNullException(nameof(toolbarConfigService));
        _uiStateProjectionService = uiStateProjectionService ?? throw new ArgumentNullException(nameof(uiStateProjectionService));
        _pageNavigationService = pageNavigationService ?? throw new ArgumentNullException(nameof(pageNavigationService));
    }

    public MainViewModel Create(MainShellCommandsViewModel shellCommands)
    {
        ZoneEditorViewModel zoneEditor = new(_runtimeConfig, _zoneEditorService, _pageNavigationService);
        ZoneListViewModel zoneList = new(_runtimeConfig, _zoneActionWorkflowCoordinator, _pageNavigationService, zoneEditor);
        ToolbarViewModel toolbar = new(_runtimeConfig, _toolbarConfigService, _uiStateProjectionService);
        return new MainViewModel(
            zoneList,
            zoneEditor,
            toolbar,
            shellCommands);
    }
}
