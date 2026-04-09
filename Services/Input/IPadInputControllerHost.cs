using System.Windows.Controls;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Input;

/// <summary>
/// View-owned surface required by <see cref="IPadInputController"/> (pad visuals, config, and VM sync).
/// </summary>
internal interface IPadInputControllerHost
{
    Canvas Pad { get; }

    IPadInputChromeSurface PadChrome { get; }

    AppConfig Config { get; }

    MainViewModel ViewModel { get; }

    InteractionState InteractionState { get; }

    IPadInteractionOutcomeApplier OutcomeApplier { get; }

    PadInputControllerState PadState { get; }

    int SelectedZoneIndex { get; set; }

    bool IsLoadingUi { get; }

    void ApplyInteractionResult(InteractionResult result);

    void RefreshPadZoneOverlay();

    void UpdateInputBackendStatus();

    /// <summary>
    /// Invoked after a full tablet rebind (e.g. refresh routing summary).
    /// </summary>
    void OnRebindCompletedRefreshRouting();

    void BeginPopulateTabletDeviceList();

    void EndPopulateTabletDeviceList();
}
