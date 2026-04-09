namespace TabletMidiPad.Services;

/// <summary>
/// Projects runtime/domain state into UI-facing status/label payloads.
/// </summary>
internal interface IUiStateProjectionService
{
    WacomServiceMenuState BuildWacomServiceMenuState(WacomServiceStatus status);

    ToolbarState BuildToolbarState(AppConfig config);

    ConnectedState BuildConnectedState(AppConfig config);
}
