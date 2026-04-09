using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Shell;

/// <summary>
/// Builds runtime workflow/controllers consumed by <see cref="Views.MainWindow"/>.
/// </summary>
internal interface IMainWindowWorkflowFactory
{
    /// <summary>
    /// Creates view-owned orchestration components and shell commands for the main window.
    /// </summary>
    MainWindowWorkflowSet Create(MainWindowWorkflowContext context);
}

/// <summary>
/// Dependencies supplied by <see cref="Views.MainWindow"/> that are needed to compose workflows.
/// </summary>
internal sealed record MainWindowWorkflowContext(
    ISettingsWorkflowHost SettingsWorkflowHost,
    IMainShellCommandHost MainShellCommandHost,
    IRuntimeConfigService RuntimeConfig,
    IMidiEngine MidiEngine,
    IWacomServiceController WacomServiceController,
    IUiStateProjectionService UiStateProjectionService,
    IPadInputController PadInputController);

/// <summary>
/// Aggregate of composed workflow/controller objects used by the main window.
/// </summary>
internal sealed record MainWindowWorkflowSet(
    ISettingsWorkflow SettingsWorkflow,
    IMainShellCommandController ShellCommandController,
    MainShellCommandsViewModel ShellCommandsViewModel);
