namespace TabletMidiPad.Services;

/// <summary>
/// Main-window surface used by <see cref="IStartupLifecycleService.RunColdStartAsync"/> so cold-start
/// sequencing stays in the startup service while config, Wacom, tablet, and UI load remain in the view.
/// </summary>
internal interface IStartupShellHost
{
    /// <summary>
    /// Pushes the active runtime configuration into bound editors, zone list, toolbar, and overlay.
    /// </summary>
    void LoadConfigIntoUi();

    /// <summary>
    /// Reads Wacom Professional Service state for startup shell decisions.
    /// </summary>
    WacomServiceStatus GetWacomServiceStatus();

    /// <summary>
    /// Updates the Wacom service menu/header from the given status.
    /// </summary>
    void ApplyWacomServiceMenuState(WacomServiceStatus status);

    /// <summary>
    /// Runs normal tablet/Wintab initialization when the shell phase allows probing the stack.
    /// </summary>
    void InitializeTabletInput();

    /// <summary>
    /// Applies blocked tablet lifecycle when Wintab cannot be probed (e.g. Wacom service stopped in Auto/Wintab-only).
    /// </summary>
    void ApplyTabletStackBlockedAtStartup(WacomServiceStatus wacom);
}
