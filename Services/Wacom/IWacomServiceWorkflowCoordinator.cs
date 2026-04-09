namespace TabletMidiPad.Services.Wacom;

/// <summary>
/// Coordinates Wacom service startup/recovery workflow decisions for MainWindow.
/// </summary>
internal interface IWacomServiceWorkflowCoordinator
{
    /// <summary>
    /// Builds startup-in-progress presentation state.
    /// </summary>
    WacomServiceWorkflowState BuildStartingState();

    /// <summary>
    /// Executes service start and returns recovery/action decisions.
    /// </summary>
    WacomServiceStartResult Start(IWacomServiceController serviceController);
}

/// <summary>
/// Header/enabled state for Wacom service menu presentation.
/// </summary>
internal readonly record struct WacomServiceWorkflowState(
    string StatusHeader,
    bool StartEnabled);

/// <summary>
/// Result payload for Wacom service startup + recovery decisions.
/// </summary>
internal readonly record struct WacomServiceStartResult(
    bool Started,
    bool ShouldRebindInputBackend,
    string? FailureStatusHeader,
    WacomServiceStatus RefreshedStatus);
