using System.Windows;
using System.Windows.Controls;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

public partial class MainWindow
{
    private sealed class ZoneEditorHost : IMainWindowZoneEditorSessionHost
    {
        private readonly MainWindow _w;

        public ZoneEditorHost(MainWindow window) => _w = window;

        public AppConfig Config => _w._config;

        public MainViewModel ViewModel => _w._viewModel;

        public int SelectedZoneIndex
        {
            get => _w.SelectedZoneIndex;
            set => _w.SelectedZoneIndex = value;
        }

        public bool IsLoadingUi
        {
            get => _w._isLoadingUi;
            set => _w._isLoadingUi = value;
        }

        public void SyncToolbar() => _w.SyncToolbar();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void RefreshInputBackendStatus() => _w.UpdateInputBackendStatus();
    }

    private sealed class PadInputHost : IMainWindowPadInputSessionHost
    {
        private readonly MainWindow _w;

        public PadInputHost(MainWindow window) => _w = window;

        public Canvas Pad => _w.Pad;

        public IPadInputChromeSurface PadChrome => _w.PadSurface;

        public AppConfig Config => _w._config;

        public MainViewModel ViewModel => _w._viewModel;

        public InteractionState InteractionState => _w._interactionState;

        public IPadInteractionOutcomeApplier OutcomeApplier => _w._interactionOutcomeApplier;

        public PadInputSessionState PadState => _w._padState;

        public int SelectedZoneIndex
        {
            get => _w.SelectedZoneIndex;
            set => _w.SelectedZoneIndex = value;
        }

        public bool IsLoadingUi => _w._isLoadingUi;

        public void ApplyInteractionResult(InteractionResult result) => _w.ApplyInteractionResult(result);

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void UpdateInputBackendStatus() => _w.UpdateInputBackendStatus();

        public void OnRebindCompletedRefreshRouting() => _w._zoneEditorSession.UpdateUiForCurrentConfig();

        public void BeginPopulateTabletDeviceList()
            => _w.TabletDeviceComboBox.SelectionChanged -= _w.TabletDeviceComboBox_SelectionChanged;

        public void EndPopulateTabletDeviceList()
            => _w.TabletDeviceComboBox.SelectionChanged += _w.TabletDeviceComboBox_SelectionChanged;
    }

    private sealed class ZonePadHost : IMainWindowZonePadSessionHost
    {
        private readonly MainWindow _w;

        public ZonePadHost(MainWindow window) => _w = window;

        public AppConfig Config => _w._config;

        public int SelectedZoneIndex
        {
            get => _w.SelectedZoneIndex;
            set => _w.SelectedZoneIndex = value;
        }

        public double PadActualWidth => _w.Pad.ActualWidth;

        public double PadActualHeight => _w.Pad.ActualHeight;

        public void CommitCurrentZoneEdits() => _w._zoneEditorSession.CommitCurrentZoneEdits();

        public void LoadSelectedZoneIntoEditor() => _w._zoneEditorSession.LoadSelectedZoneIntoEditor();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void SetSettingsResult(string message, SettingsResultKind kind) => _w.SetSettingsResult(message, kind);

        public void ClearPadCursor() => _w.Pad.Cursor = null;
    }

    private sealed class SettingsSessionHost : IMainWindowSettingsSessionHost
    {
        private readonly MainWindow _w;

        public SettingsSessionHost(MainWindow window) => _w = window;

        public Window OwnerWindow => _w;

        public AppConfig Config => _w._config;

        public void ReplaceConfig(AppConfig config) => _w._config = config;

        public PadInputSessionState PadState => _w._padState;

        public void CommitCurrentZoneEdits() => _w._zoneEditorSession.CommitCurrentZoneEdits();

        public void SetSettingsResult(string message, SettingsResultKind kind) => _w.SetSettingsResult(message, kind);

        public void UpdateUiForCurrentConfig() => _w._zoneEditorSession.UpdateUiForCurrentConfig();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void SyncToolbar() => _w.SyncToolbar();

        public void LoadConfigIntoUi(AppConfig config) => _w._zoneEditorSession.LoadConfigIntoUi(config);
    }

    private sealed class ZoneListHost : IMainWindowZoneListSessionHost
    {
        private readonly MainWindow _w;

        public ZoneListHost(MainWindow window) => _w = window;

        public AppConfig Config => _w._config;

        public int SelectedZoneIndex
        {
            get => _w.SelectedZoneIndex;
            set => _w.SelectedZoneIndex = value;
        }

        public void CommitCurrentZoneEdits() => _w._zoneEditorSession.CommitCurrentZoneEdits();

        public void RefreshZoneList() => _w._zoneEditorSession.RefreshZoneList();

        public void NotifySelectedZoneIndexChanged() => _w._viewModel.NotifySelectedZoneIndexChanged();

        public void LoadSelectedZoneIntoEditor() => _w._zoneEditorSession.LoadSelectedZoneIntoEditor();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void SetSettingsResult(string message, SettingsResultKind kind) => _w.SetSettingsResult(message, kind);
    }

    private sealed class StartupHost : IMainWindowStartupHost
    {
        private readonly MainWindow _w;

        public StartupHost(MainWindow window) => _w = window;

        public AppConfig Config => _w._config;

        public void ReplaceRuntimeConfig(AppConfig config) => _w._config = config;

        public void LoadConfigIntoUi(AppConfig config) => _w._zoneEditorSession.LoadConfigIntoUi(config);

        public WacomServiceStatus GetWacomServiceStatus() => _w._wacomServiceController.GetStatus();

        public void ApplyWacomServiceMenuState(WacomServiceStatus status) => _w.RefreshWacomServiceStatus(status);

        public void InitializeTabletInput() => _w._padInputSession.InitializeTabletInput();

        public void ApplyTabletStackBlockedAtStartup(WacomServiceStatus wacom)
        {
            _w._padInputSession.ShutdownWintabBackendOnly();
            _w._padInputSession.RefreshTabletDeviceListOnly();
            InputBackendLifecycleResult blocked = _w._startupLifecycle.BuildTabletStackBlockedLifecycleResult(_w._config, wacom);
            _w._padInputSession.ApplyTabletLifecycle(blocked, refreshInputBackendStatusText: true);
        }
    }
}
