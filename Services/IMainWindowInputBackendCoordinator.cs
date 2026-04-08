namespace TabletMidiPad.Services;

/// <summary>
/// Coordinates main-window input backend lifecycle/rebind/reset orchestration.
/// </summary>
internal interface IMainWindowInputBackendCoordinator
{
    /// <summary>
    /// Initializes backend state for the selected input mode/device.
    /// </summary>
    InputBackendLifecycleResult Initialize(
        string inputBackendMode,
        IWintabInputBackend backend,
        int selectedDeviceIndex,
        int currentExpressKeyCount);

    /// <summary>
    /// Shuts down the backend and returns resulting lifecycle state.
    /// </summary>
    InputBackendLifecycleResult Shutdown(IWintabInputBackend backend);

    /// <summary>
    /// Produces reset-state labels/flags before a backend rebind.
    /// </summary>
    InputBackendResetResult BuildResetState();
}

/// <summary>
/// Backend lifecycle state payload for main-window orchestration.
/// </summary>
internal readonly record struct InputBackendLifecycleResult(
    bool UseWintab,
    int ExpressKeyCount,
    string LastInputBackend,
    string WintabDiagnostics);

/// <summary>
/// Active input state reset payload for main-window orchestration.
/// </summary>
internal readonly record struct InputBackendResetResult(
    bool IsActive,
    bool LowerPenButtonHeld,
    bool UpperPenButtonHeld,
    bool HasLockedPosition,
    string ActiveZoneText,
    string ValueText,
    string PenButtonsText,
    string ModifiersText);
