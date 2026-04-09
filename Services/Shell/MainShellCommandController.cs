using System;
using System.Threading.Tasks;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Default shell-level command orchestration for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class MainShellCommandController : IMainShellCommandController
{
    private readonly IMainShellCommandHost _host;
    private readonly ISettingsWorkflow _settingsWorkflow;
    private readonly IWacomServiceWorkflowCoordinator _wacomServiceWorkflowCoordinator;
    private readonly IWacomServiceController _wacomServiceController;
    private readonly IUiStateProjectionService _uiStateProjectionService;
    private readonly IPadInputController _padInputController;

    public MainShellCommandController(
        IMainShellCommandHost host,
        ISettingsWorkflow settingsWorkflow,
        IWacomServiceWorkflowCoordinator wacomServiceWorkflowCoordinator,
        IWacomServiceController wacomServiceController,
        IUiStateProjectionService uiStateProjectionService,
        IPadInputController padInputController)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _settingsWorkflow = settingsWorkflow ?? throw new ArgumentNullException(nameof(settingsWorkflow));
        _wacomServiceWorkflowCoordinator = wacomServiceWorkflowCoordinator ?? throw new ArgumentNullException(nameof(wacomServiceWorkflowCoordinator));
        _wacomServiceController = wacomServiceController ?? throw new ArgumentNullException(nameof(wacomServiceController));
        _uiStateProjectionService = uiStateProjectionService ?? throw new ArgumentNullException(nameof(uiStateProjectionService));
        _padInputController = padInputController ?? throw new ArgumentNullException(nameof(padInputController));
    }

    public void SaveSettings() => _settingsWorkflow.ApplySettings();

    public void ResetDefaults() => _settingsWorkflow.ResetToDefaultsAndApply();

    public void OpenPreferences() => _settingsWorkflow.RunPreferencesFlow();

    public void Exit() => _host.CloseShell();

    public void RefreshWacomStatus()
    {
        ApplyWacomServiceMenuState(_wacomServiceController.GetStatus());
    }

    public void ReconnectPen()
    {
        _host.CommitCurrentZoneEdits();
        _padInputController.RebindInputBackend();
        _host.SetSettingsResult("Input backend reconnected.", SettingsResultKind.Success);
    }

    public void StartWacomService()
    {
        _ = StartWacomServiceAsync();
    }

    public void ShowAbout() => _host.ShowAboutDialog();

    private async Task StartWacomServiceAsync()
    {
        WacomServiceWorkflowState startingState = _wacomServiceWorkflowCoordinator.BuildStartingState();
        _host.SetWacomServiceState(startingState.StatusHeader, startingState.StartEnabled);

        WacomServiceStartResult result = await Task.Run(() => _wacomServiceWorkflowCoordinator.Start(_wacomServiceController));
        if (result.Started)
        {
            ApplyWacomServiceMenuState(result.RefreshedStatus);
            if (result.ShouldRebindInputBackend)
            {
                _padInputController.RebindInputBackend();
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(result.FailureStatusHeader))
        {
            _host.SetWacomServiceStatusHeader(result.FailureStatusHeader);
        }

        ApplyWacomServiceMenuState(result.RefreshedStatus);
    }

    private void ApplyWacomServiceMenuState(WacomServiceStatus status)
    {
        WacomServiceMenuState menuState = _uiStateProjectionService.BuildWacomServiceMenuState(status);
        _host.SetWacomServiceState(menuState.Header, menuState.StartEnabled);
    }
}
