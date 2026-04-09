using System.Collections.ObjectModel;
using System.Windows;
using TabletMidiPad.ViewModels;
using TabletMidiPad.Views;

namespace TabletMidiPad.Services.Settings;

/// <summary>
/// WPF-backed implementation that opens the preferences dialog and
/// returns the resulting settings when accepted by the user.
/// </summary>
internal sealed class PreferencesDialogService : IPreferencesDialogService
{
    private readonly IConfigValueParser _configValueParser;
    private readonly Func<PreferencesViewModel> _preferencesViewModelFactory;
    private readonly IMidiEngine _midiEngine;
    private readonly IWacomServiceController _wacomServiceController;
    private readonly IWacomServiceWorkflowCoordinator _wacomServiceWorkflowCoordinator;
    private readonly IUiStateProjectionService _uiStateProjectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreferencesDialogService"/> class.
    /// </summary>
    public PreferencesDialogService(
        IConfigValueParser configValueParser,
        Func<PreferencesViewModel> preferencesViewModelFactory,
        IMidiEngine midiEngine,
        IWacomServiceController wacomServiceController,
        IWacomServiceWorkflowCoordinator wacomServiceWorkflowCoordinator,
        IUiStateProjectionService uiStateProjectionService)
    {
        _configValueParser = configValueParser ?? throw new ArgumentNullException(nameof(configValueParser));
        _preferencesViewModelFactory = preferencesViewModelFactory ?? throw new ArgumentNullException(nameof(preferencesViewModelFactory));
        _midiEngine = midiEngine ?? throw new ArgumentNullException(nameof(midiEngine));
        _wacomServiceController = wacomServiceController ?? throw new ArgumentNullException(nameof(wacomServiceController));
        _wacomServiceWorkflowCoordinator = wacomServiceWorkflowCoordinator ?? throw new ArgumentNullException(nameof(wacomServiceWorkflowCoordinator));
        _uiStateProjectionService = uiStateProjectionService ?? throw new ArgumentNullException(nameof(uiStateProjectionService));
    }

    /// <inheritdoc />
    public PreferencesDialogResult? ShowDialog(
        Window owner,
        AppConfig currentConfig,
        int expressKeyCount,
        ObservableCollection<TabletDeviceListItemViewModel> tabletDevices,
        TabletDeviceListItemViewModel? selectedTabletDevice,
        bool isTabletDevicePickerEnabled,
        Action reconnectPen,
        Action<TabletDeviceListItemViewModel?> setSelectedTabletDevice)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(currentConfig);
        ArgumentNullException.ThrowIfNull(tabletDevices);
        ArgumentNullException.ThrowIfNull(reconnectPen);
        ArgumentNullException.ThrowIfNull(setSelectedTabletDevice);

        ExpressKeyConfig.EnsureCount(currentConfig.ExpressKeys, expressKeyCount);

        PreferencesViewModel viewModel = _preferencesViewModelFactory();
        viewModel.WacomStatus = BuildWacomStatusViewModel(
            tabletDevices,
            selectedTabletDevice,
            isTabletDevicePickerEnabled,
            reconnectPen,
            setSelectedTabletDevice);

        var preferencesWindow = new PreferencesWindow(
            expressKeyCount,
            _configValueParser,
            viewModel,
            reconnectMidiAsync: async (config, onProgress) =>
            {
                await _midiEngine.RestartAsync(config, onProgress);
                _midiEngine.ClearCachedValues();
            },
            recreateLoopbackAsync: async (config, onProgress) =>
            {
                await _midiEngine.RecreateLoopbackPairAsync(config, onProgress);
                _midiEngine.ClearCachedValues();
            },
            removeLoopbackAsync: async (config, onProgress) =>
            {
                await _midiEngine.RemoveLoopbackPairAsync(config, onProgress);
                _midiEngine.ClearCachedValues();
            })
        {
            Owner = owner
        };
        preferencesWindow.LoadFrom(currentConfig);
        preferencesWindow.ShowDialog();

        if (!preferencesWindow.Accepted)
        {
            return null;
        }

        return new PreferencesDialogResult
        {
            DawPortName = preferencesWindow.DawPortName,
            AppPortName = preferencesWindow.AppPortName,
            AssociationId = preferencesWindow.AssociationId,
            MousePressureValue = preferencesWindow.MousePressureValue,
            PositionDeadZone = preferencesWindow.PositionDeadZone,
            PositionSmoothing = preferencesWindow.PositionSmoothing,
            PressureCurve = preferencesWindow.PressureCurve,
            InputBackendMode = preferencesWindow.InputBackendMode,
            LowerPenButtonAction = preferencesWindow.LowerPenButtonAction,
            LowerPenButtonMidiChannel = preferencesWindow.LowerPenButtonMidiChannel,
            LowerPenButtonCc = preferencesWindow.LowerPenButtonCc,
            LowerPenButtonNote = preferencesWindow.LowerPenButtonNote,
            UpperPenButtonAction = preferencesWindow.UpperPenButtonAction,
            UpperPenButtonMidiChannel = preferencesWindow.UpperPenButtonMidiChannel,
            UpperPenButtonCc = preferencesWindow.UpperPenButtonCc,
            UpperPenButtonNote = preferencesWindow.UpperPenButtonNote,
            SendPressureGlobally = preferencesWindow.SendPressureGlobally,
            AllowZoneSwitchWhilePressed = preferencesWindow.AllowZoneSwitchWhilePressed,
            AllowPageSwitchWhilePressed = preferencesWindow.AllowPageSwitchWhilePressed,
            DestroyVirtualMidiOnExit = preferencesWindow.DestroyVirtualMidiOnExit,
            WrapPageNavigation = preferencesWindow.WrapPageNavigation,
            ExpressKeys = preferencesWindow.GetExpressKeyConfigs(),
            ZoneOverlayStyle = preferencesWindow.ZoneOverlayStyle
        };
    }

    private WacomStatusViewModel BuildWacomStatusViewModel(
        ObservableCollection<TabletDeviceListItemViewModel> tabletDevices,
        TabletDeviceListItemViewModel? selectedTabletDevice,
        bool isTabletDevicePickerEnabled,
        Action reconnectPen,
        Action<TabletDeviceListItemViewModel?> setSelectedTabletDevice)
    {
        WacomStatusViewModel vm = null!;
        vm = new WacomStatusViewModel
        {
            TabletDevices = tabletDevices,
            SelectedTabletDevice = selectedTabletDevice,
            IsTabletDevicePickerEnabled = isTabletDevicePickerEnabled,
            OnSelectedTabletDeviceChanged = setSelectedTabletDevice,
            RefreshCommand = new DelegateCommand(() =>
            {
                WacomServiceStatus status = _wacomServiceController.GetStatus();
                WacomServiceMenuState state = _uiStateProjectionService.BuildWacomServiceMenuState(status);
                vm.StatusText = state.Header;
                vm.IsStartEnabled = state.StartEnabled;
                vm.IsTabletDevicePickerEnabled = isTabletDevicePickerEnabled || HasSelectableTabletDevice(vm.TabletDevices);
            }),
            ReconnectPenCommand = new DelegateCommand(() =>
            {
                reconnectPen();
                vm.IsTabletDevicePickerEnabled = isTabletDevicePickerEnabled || HasSelectableTabletDevice(vm.TabletDevices);
            }),
            StartServiceCommand = new DelegateCommand(() =>
            {
                WacomServiceWorkflowState starting = _wacomServiceWorkflowCoordinator.BuildStartingState();
                vm.StatusText = starting.StatusHeader;
                vm.IsStartEnabled = starting.StartEnabled;

                _ = Task.Run(() =>
                {
                    WacomServiceStartResult result = _wacomServiceWorkflowCoordinator.Start(_wacomServiceController);
                    WacomServiceMenuState state = _uiStateProjectionService.BuildWacomServiceMenuState(result.RefreshedStatus);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        vm.StatusText = state.Header;
                        vm.IsStartEnabled = state.StartEnabled;
                    });
                });
            })
        };

        // Populate initial status.
        WacomServiceStatus initial = _wacomServiceController.GetStatus();
        WacomServiceMenuState initialState = _uiStateProjectionService.BuildWacomServiceMenuState(initial);
        vm.StatusText = initialState.Header;
        vm.IsStartEnabled = initialState.StartEnabled;
        vm.IsTabletDevicePickerEnabled = isTabletDevicePickerEnabled || HasSelectableTabletDevice(vm.TabletDevices);

        return vm;
    }

    private static bool HasSelectableTabletDevice(IReadOnlyList<TabletDeviceListItemViewModel> devices)
        => devices.Count > 0 && devices[0].DeviceIndex >= 0;
}
