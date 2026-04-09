using System.Collections.ObjectModel;
using System.Windows;
using TabletMidiPad.Services.Shell;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Settings;

/// <summary>
/// View-owned surface for <see cref="ISettingsWorkflow"/> (dialog owner and VM refresh hooks).
/// </summary>
internal interface ISettingsWorkflowHost
    : ISettingsResultHost
{
    Window OwnerWindow { get; }

    PadInputControllerState PadState { get; }

    ObservableCollection<TabletDeviceListItemViewModel> TabletDevices { get; }

    TabletDeviceListItemViewModel? SelectedTabletDevice { get; set; }

    bool IsTabletDevicePickerEnabled { get; }

    void CommitCurrentZoneEdits();

    void UpdateUiForCurrentConfig();

    void RefreshPadZoneOverlay();

    void SyncToolbar();

    void LoadConfigIntoUi();

    void ReconnectPen();

    void SetMidiReconnectStatus(bool panelVisible, bool progressVisible, MidiStatusSeverity severity, string statusText);
}
