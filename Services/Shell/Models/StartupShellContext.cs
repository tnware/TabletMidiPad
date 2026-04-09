namespace TabletMidiPad.Services.Shell.Models;

/// <summary>
/// Callback bundle for the main window post-config shell phase (Wacom status + tablet stack init or skip).
/// </summary>
internal readonly record struct StartupShellContext(
    Func<WacomServiceStatus> GetWacomServiceStatus,
    Action<WacomServiceStatus> ApplyWacomServiceMenuState,
    AppConfig Config,
    Action InitializeTabletWhenReady,
    Action<WacomServiceStatus> ApplyTabletBlockedByWacomService);
