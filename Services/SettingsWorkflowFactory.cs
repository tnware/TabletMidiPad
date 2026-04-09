using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Default factory for <see cref="SettingsWorkflow"/>.
/// </summary>
internal sealed class SettingsWorkflowFactory : ISettingsWorkflowFactory
{
    private readonly IWorkflowCoordinator _workflowCoordinator;
    private readonly IPreferencesDialogService _preferencesDialogService;
    private readonly IZoneConfigurationService _zoneConfigurationService;
    private readonly IRuntimeConfigService _runtimeConfigService;
    private readonly IMidiEngine _midiEngine;

    public SettingsWorkflowFactory(
        IWorkflowCoordinator workflowCoordinator,
        IPreferencesDialogService preferencesDialogService,
        IZoneConfigurationService zoneConfigurationService,
        IRuntimeConfigService runtimeConfigService,
        IMidiEngine midiEngine)
    {
        _workflowCoordinator = workflowCoordinator ?? throw new ArgumentNullException(nameof(workflowCoordinator));
        _preferencesDialogService = preferencesDialogService ?? throw new ArgumentNullException(nameof(preferencesDialogService));
        _zoneConfigurationService = zoneConfigurationService ?? throw new ArgumentNullException(nameof(zoneConfigurationService));
        _runtimeConfigService = runtimeConfigService ?? throw new ArgumentNullException(nameof(runtimeConfigService));
        _midiEngine = midiEngine ?? throw new ArgumentNullException(nameof(midiEngine));
    }

    public ISettingsWorkflow Create(ISettingsWorkflowHost host, IPadInputController padInputController)
    {
        return new SettingsWorkflow(
            host,
            _workflowCoordinator,
            _preferencesDialogService,
            _zoneConfigurationService,
            _runtimeConfigService,
            _midiEngine,
            padInputController);
    }
}

