using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Shell;

/// <summary>
/// Default composition factory for main-window workflow/controller objects.
/// </summary>
internal sealed class MainWindowWorkflowFactory : IMainWindowWorkflowFactory
{
    private readonly ISettingsWorkflowCoordinator _settingsWorkflowCoordinator;
    private readonly IPreferencesDialogService _preferencesDialogService;
    private readonly IZoneConfigurationService _zoneConfigurationService;
    private readonly IWacomServiceWorkflowCoordinator _wacomServiceWorkflowCoordinator;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowWorkflowFactory"/> class.
    /// </summary>
    public MainWindowWorkflowFactory(
        ISettingsWorkflowCoordinator settingsWorkflowCoordinator,
        IPreferencesDialogService preferencesDialogService,
        IZoneConfigurationService zoneConfigurationService,
        IWacomServiceWorkflowCoordinator wacomServiceWorkflowCoordinator)
    {
        _settingsWorkflowCoordinator = settingsWorkflowCoordinator ?? throw new ArgumentNullException(nameof(settingsWorkflowCoordinator));
        _preferencesDialogService = preferencesDialogService ?? throw new ArgumentNullException(nameof(preferencesDialogService));
        _zoneConfigurationService = zoneConfigurationService ?? throw new ArgumentNullException(nameof(zoneConfigurationService));
        _wacomServiceWorkflowCoordinator = wacomServiceWorkflowCoordinator ?? throw new ArgumentNullException(nameof(wacomServiceWorkflowCoordinator));
    }

    /// <inheritdoc />
    public MainWindowWorkflowSet Create(MainWindowWorkflowContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        ISettingsWorkflow settingsWorkflow = new SettingsWorkflow(
            context.SettingsWorkflowHost,
            _settingsWorkflowCoordinator,
            _preferencesDialogService,
            _zoneConfigurationService,
            context.RuntimeConfig,
            context.MidiEngine,
            context.PadInputController);

        IMainShellCommandController shellCommandController = new MainShellCommandController(
            context.MainShellCommandHost,
            settingsWorkflow,
            _wacomServiceWorkflowCoordinator,
            context.WacomServiceController,
            context.UiStateProjectionService,
            context.PadInputController);

        MainShellCommandsViewModel shellCommandsViewModel = new(shellCommandController);
        return new MainWindowWorkflowSet(settingsWorkflow, shellCommandController, shellCommandsViewModel);
    }
}
