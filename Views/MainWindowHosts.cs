using System.Windows;
using System.Windows.Controls;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

public partial class MainWindow
{
    private sealed class PadInputViewHost : IPadInputControllerHost
    {
        private readonly MainWindow _w;

        public PadInputViewHost(MainWindow window)
        {
            _w = window;
        }

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

    private sealed class ZonePadViewHost : IZonePadControllerHost
    {
        private readonly MainWindow _w;

        public ZonePadViewHost(MainWindow window)
        {
            _w = window;
        }

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

    private sealed class SettingsWorkflowViewHost : ISettingsWorkflowHost
    {
        private readonly MainWindow _w;

        public SettingsWorkflowViewHost(MainWindow window)
        {
            _w = window;
        }

        public Window OwnerWindow => _w;

        public PadInputControllerState PadState => _w._padState;

        public void CommitCurrentZoneEdits() => _w._viewModel.ZoneEditor.CommitCurrentZoneEdits();

        public void SetSettingsResult(string message, SettingsResultKind kind) => _w.SetSettingsResult(message, kind);

        public void UpdateUiForCurrentConfig() => _w.UpdateUiForCurrentConfig();

        public void RefreshPadZoneOverlay() => _w.RefreshPadZoneOverlay();

        public void SyncToolbar() => _w.SyncToolbar();

        public void LoadConfigIntoUi() => _w.LoadConfigIntoUi();
    }

    private sealed class MainShellCommandHost : IMainShellCommandHost
    {
        private readonly MainWindow _w;

        public MainShellCommandHost(MainWindow window)
        {
            _w = window;
        }

        public void CommitCurrentZoneEdits() => _w._viewModel.ZoneEditor.CommitCurrentZoneEdits();

        public void SetSettingsResult(string message, SettingsResultKind kind) => _w.SetSettingsResult(message, kind);

        public void SetWacomServiceState(string statusHeader, bool startEnabled)
        {
            _w._viewModel.WacomServiceStatusHeader = statusHeader;
            _w._viewModel.IsWacomServiceStartEnabled = startEnabled;
        }

        public void SetWacomServiceStatusHeader(string statusHeader) => _w._viewModel.WacomServiceStatusHeader = statusHeader;

        public void CloseShell() => _w.Close();

        public void ShowAboutDialog()
        {
            MessageBox.Show(
                "Tablet MIDI Pad\n\nTurns a Wacom tablet into a MIDI XY controller with configurable zones.",
                "About Tablet MIDI Pad",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    private sealed class StartupViewHost : IStartupShellHost
    {
        private readonly MainWindow _w;

        public StartupViewHost(MainWindow window)
        {
            _w = window;
        }

        public void LoadConfigIntoUi() => _w.LoadConfigIntoUi();

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
