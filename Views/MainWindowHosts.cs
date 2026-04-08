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

        public AppConfig Config => _w.SettingsServiceRef.Config;

        public MainViewModel ViewModel => _w.ViewModelRef;

        public int SelectedZoneIndex
        {
            get => _w.ViewModelRef.SelectedZoneIndex;
            set => _w.ViewModelRef.SelectedZoneIndex = value;
        }

        public bool IsLoadingUi
        {
            get => _w.IsLoadingUiRef;
            set { /* handled via internal state in MainWindow */ }
        }

        public void SyncToolbar() => _w.SyncToolbar();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void RefreshInputBackendStatus() => _w.UpdateInputBackendStatusRef();
    }

    private sealed class PadInputHost : IMainWindowPadInputSessionHost
    {
        private readonly MainWindow _w;

        public PadInputHost(MainWindow window) => _w = window;

        public Canvas Pad => _w.PadRef;

        public IPadInputChromeSurface PadChrome => _w.PadSurfaceRef;

        public AppConfig Config => _w.SettingsServiceRef.Config;

        public MainViewModel ViewModel => _w.ViewModelRef;

        public InteractionState InteractionState => _w.InteractionStateRef;

        public IPadInteractionOutcomeApplier OutcomeApplier => _w.OutcomeApplierRef;

        public PadInputSessionState PadState => _w.PadStateRef;

        public int SelectedZoneIndex
        {
            get => _w.ViewModelRef.SelectedZoneIndex;
            set => _w.ViewModelRef.SelectedZoneIndex = value;
        }

        public bool IsLoadingUi => _w.IsLoadingUiRef;

        public void ApplyInteractionResult(InteractionResult result) => _w.ApplyInteractionResultRef(result);

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void UpdateInputBackendStatus() => _w.UpdateInputBackendStatusRef();

        public void OnRebindCompletedRefreshRouting() => _w.ZoneEditorSessionRef.UpdateUiForCurrentConfig();

        public void BeginPopulateTabletDeviceList()
            => _w.BeginPopulateTabletDeviceListRef();

        public void EndPopulateTabletDeviceList()
            => _w.EndPopulateTabletDeviceListRef();
    }

    private sealed class ZonePadHost : IMainWindowZonePadSessionHost
    {
        private readonly MainWindow _w;

        public ZonePadHost(MainWindow window) => _w = window;

        public AppConfig Config => _w.SettingsServiceRef.Config;

        public int SelectedZoneIndex
        {
            get => _w.ViewModelRef.SelectedZoneIndex;
            set => _w.ViewModelRef.SelectedZoneIndex = value;
        }

        public double PadActualWidth => _w.PadRef.ActualWidth;

        public double PadActualHeight => _w.PadRef.ActualHeight;

        public void CommitCurrentZoneEdits() => _w.ZoneEditorSessionRef.CommitCurrentZoneEdits();

        public void LoadSelectedZoneIntoEditor() => _w.ZoneEditorSessionRef.LoadSelectedZoneIntoEditor();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void SetSettingsResult(string message, SettingsResultKind kind) => _w.SetSettingsResultRef(message, kind);

        public void ClearPadCursor() => _w.PadRef.Cursor = null;
    }

    private sealed class SettingsSessionHost : IMainWindowSettingsSessionHost
    {
        private readonly MainWindow _w;

        public SettingsSessionHost(MainWindow window) => _w = window;

        public Window OwnerWindow => _w;

        public AppConfig Config => _w.SettingsServiceRef.Config;

        public void ReplaceConfig(AppConfig config) => _w.SettingsServiceRef.UpdateConfig(config, false);

        public PadInputSessionState PadState => _w.PadStateRef;

        public void CommitCurrentZoneEdits() => _w.ZoneEditorSessionRef.CommitCurrentZoneEdits();

        public void SetSettingsResult(string message, SettingsResultKind kind) => _w.SetSettingsResultRef(message, kind);

        public void UpdateUiForCurrentConfig() => _w.ZoneEditorSessionRef.UpdateUiForCurrentConfig();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void SyncToolbar() => _w.SyncToolbar();

        public void LoadConfigIntoUi(AppConfig config) => _w.ZoneEditorSessionRef.LoadConfigIntoUi(config);
    }

    private sealed class ZoneListHost : IMainWindowZoneListSessionHost
    {
        private readonly MainWindow _w;

        public ZoneListHost(MainWindow window) => _w = window;

        public AppConfig Config => _w.SettingsServiceRef.Config;

        public int SelectedZoneIndex
        {
            get => _w.ViewModelRef.SelectedZoneIndex;
            set => _w.ViewModelRef.SelectedZoneIndex = value;
        }

        public void CommitCurrentZoneEdits() => _w.ZoneEditorSessionRef.CommitCurrentZoneEdits();

        public void RefreshZoneList() => _w.ZoneEditorSessionRef.RefreshZoneList();

        public void NotifySelectedZoneIndexChanged() => _w.ViewModelRef.NotifySelectedZoneIndexChanged();

        public void LoadSelectedZoneIntoEditor() => _w.ZoneEditorSessionRef.LoadSelectedZoneIntoEditor();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void SetSettingsResult(string message, SettingsResultKind kind) => _w.SetSettingsResultRef(message, kind);
    }

    private sealed class StartupHost : IMainWindowStartupHost
    {
        private readonly MainWindow _w;

        public StartupHost(MainWindow window) => _w = window;

        public AppConfig Config => _w.SettingsServiceRef.Config;

        public void ReplaceRuntimeConfig(AppConfig config) => _w.SettingsServiceRef.UpdateConfig(config, false);

        public void LoadConfigIntoUi(AppConfig config) => _w.ZoneEditorSessionRef.LoadConfigIntoUi(config);

        public WacomServiceStatus GetWacomServiceStatus() => _w._wacomServiceController.GetStatus();

        public void ApplyWacomServiceMenuState(WacomServiceStatus status) => _w.RefreshWacomServiceStatus(status);

        public void InitializeTabletInput() => _w.InitializeTabletInput();

        public void ApplyTabletStackBlockedAtStartup(WacomServiceStatus wacom)
        {
            _w.ShutdownWintabBackendOnly();
            _w.RefreshTabletDeviceListOnly();
            InputBackendLifecycleResult blocked = _w.BuildTabletStackBlockedLifecycleResult(_w.SettingsServiceRef.Config, wacom);
            _w.ApplyTabletLifecycle(blocked, true);
        }
    }
}
