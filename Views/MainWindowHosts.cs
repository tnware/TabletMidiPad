using System.Windows;
using System.Windows.Controls;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

public partial class MainWindow
{
    private sealed class PadInputHost : IPadInputControllerHost
    {
        private readonly MainWindow _w;

        public PadInputHost(MainWindow window) => _w = window;

        public Canvas Pad => _w.Pad;

        public IPadInputChromeSurface PadChrome => _w.PadSurface;

        public AppConfig Config => _w.Config;

        public MainViewModel ViewModel => _w._viewModel;

        public InteractionState InteractionState => _w._interactionState;

        public IPadInteractionOutcomeApplier OutcomeApplier => _w._interactionOutcomeApplier;

        public PadInputControllerState PadState => _w._padState;

        public int SelectedZoneIndex
        {
            get => _w.SelectedZoneIndex;
            set => _w.SelectedZoneIndex = value;
        }

        public bool IsLoadingUi => _w._viewModel.ZoneEditor.IsLoadingUi;

        public void ApplyInteractionResult(InteractionResult result) => _w.ApplyInteractionResult(result);

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void UpdateInputBackendStatus() => _w.UpdateInputBackendStatus();

        public void OnRebindCompletedRefreshRouting() => _w.UpdateUiForCurrentConfig();

        public void BeginPopulateTabletDeviceList()
            => _w.TabletDeviceComboBox.SelectionChanged -= _w.TabletDeviceComboBox_SelectionChanged;

        public void EndPopulateTabletDeviceList()
            => _w.TabletDeviceComboBox.SelectionChanged += _w.TabletDeviceComboBox_SelectionChanged;
    }

    private sealed class ZonePadHost : IZonePadControllerHost
    {
        private readonly MainWindow _w;

        public ZonePadHost(MainWindow window) => _w = window;

        public AppConfig Config => _w.Config;

        public int SelectedZoneIndex
        {
            get => _w.SelectedZoneIndex;
            set => _w.SelectedZoneIndex = value;
        }

        public double PadActualWidth => _w.Pad.ActualWidth;

        public double PadActualHeight => _w.Pad.ActualHeight;

        public void CommitCurrentZoneEdits() => _w._viewModel.ZoneEditor.CommitCurrentZoneEdits();

        public void LoadSelectedZoneIntoEditor() => _w._viewModel.ZoneEditor.LoadSelectedZoneIntoEditor();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void SetSettingsResult(string message, SettingsResultKind kind) => _w.SetSettingsResult(message, kind);

        public void ClearPadCursor() => _w.Pad.Cursor = null;
    }

    private sealed class SettingsSessionHost : ISettingsWorkflowHost
    {
        private readonly MainWindow _w;

        public SettingsSessionHost(MainWindow window) => _w = window;

        public Window OwnerWindow => _w;

        public AppConfig Config => _w.Config;

        public void ReplaceConfig(AppConfig config) => _w._runtimeConfig.Replace(config);

        public PadInputControllerState PadState => _w._padState;

        public void CommitCurrentZoneEdits() => _w._viewModel.ZoneEditor.CommitCurrentZoneEdits();

        public void SetSettingsResult(string message, SettingsResultKind kind) => _w.SetSettingsResult(message, kind);

        public void UpdateUiForCurrentConfig() => _w.UpdateUiForCurrentConfig();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void SyncToolbar() => _w.SyncToolbar();

        public void LoadConfigIntoUi(AppConfig config) => _w.LoadConfigIntoUi();
    }

    private sealed class StartupHost : IStartupShellHost
    {
        private readonly MainWindow _w;

        public StartupHost(MainWindow window) => _w = window;

        public AppConfig Config => _w.Config;

        public void ReplaceRuntimeConfig(AppConfig config) => _w._runtimeConfig.Replace(config);

        public void LoadConfigIntoUi(AppConfig config) => _w.LoadConfigIntoUi();

        public WacomServiceStatus GetWacomServiceStatus() => _w._wacomServiceController.GetStatus();

        public void ApplyWacomServiceMenuState(WacomServiceStatus status) => _w.RefreshWacomServiceStatus(status);

        public void InitializeTabletInput() => _w._padInputSession.InitializeTabletInput();

        public void ApplyTabletStackBlockedAtStartup(WacomServiceStatus wacom)
        {
            _w._padInputSession.ShutdownWintabBackendOnly();
            _w._padInputSession.RefreshTabletDeviceListOnly();
            InputBackendLifecycleResult blocked = _w._startupLifecycle.BuildTabletStackBlockedLifecycleResult(_w.Config, wacom);
            _w._padInputSession.ApplyTabletLifecycle(blocked, refreshInputBackendStatusText: true);
        }
    }
}
