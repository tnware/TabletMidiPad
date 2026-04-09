using System;
using System.Windows.Input;

namespace TabletMidiPad.ViewModels;

internal sealed class ToolbarViewModel : ViewModelBase
{
    private readonly IRuntimeConfigService _runtimeConfig;
    private readonly IToolbarConfigService _toolbarSession;
    private readonly IUiStateProjectionService _uiStateProjectionService;
    private string _toolbarPressureLabelText = "Pressure (Lin)";
    private string _toolbarInputBackendLabelText = "Input (Auto)";
    private string _selectedPressureCurve = "Linear";
    private string _selectedInputBackendMode = "Auto";
    private bool _isPressureCurveLinearChecked = true;
    private bool _isPressureCurveLogarithmicChecked;
    private bool _isPressureCurveExponentialChecked;
    private bool _isInputBackendAutoChecked = true;
    private bool _isInputBackendWintabChecked;
    private bool _isInputBackendMouseChecked;
    private bool _sendPressureGlobally = true;
    private bool _allowZoneSwitchWhilePressed = true;
    private bool _isSyncing;

    public ToolbarViewModel(
        IRuntimeConfigService runtimeConfig,
        IToolbarConfigService toolbarSession,
        IUiStateProjectionService uiStateProjectionService)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _toolbarSession = toolbarSession ?? throw new ArgumentNullException(nameof(toolbarSession));
        _uiStateProjectionService = uiStateProjectionService ?? throw new ArgumentNullException(nameof(uiStateProjectionService));
        SelectPressureCurveLinearCommand = new DelegateCommand(() => SetPressureCurve("Linear"));
        SelectPressureCurveLogarithmicCommand = new DelegateCommand(() => SetPressureCurve("Logarithmic"));
        SelectPressureCurveExponentialCommand = new DelegateCommand(() => SetPressureCurve("Exponential"));
        SelectInputBackendAutoCommand = new DelegateCommand(() => SetInputBackendMode("Auto"));
        SelectInputBackendWintabCommand = new DelegateCommand(() => SetInputBackendMode("Wintab Only"));
        SelectInputBackendMouseCommand = new DelegateCommand(() => SetInputBackendMode("Mouse"));
        CycleInputBackendCommand = new DelegateCommand(CycleInputBackend);
    }

    public event Action? PressureChromeRefreshRequested;

    public event Action? RoutingStatusRefreshRequested;

    public event Action? InputBackendRebindRequested;

    public ICommand SelectPressureCurveLinearCommand { get; }

    public ICommand SelectPressureCurveLogarithmicCommand { get; }

    public ICommand SelectPressureCurveExponentialCommand { get; }

    public ICommand SelectInputBackendAutoCommand { get; }

    public ICommand SelectInputBackendWintabCommand { get; }

    public ICommand SelectInputBackendMouseCommand { get; }

    public ICommand CycleInputBackendCommand { get; }

    public string ToolbarPressureLabelText
    {
        get => _toolbarPressureLabelText;
        set => SetProperty(ref _toolbarPressureLabelText, value);
    }

    public string ToolbarInputBackendLabelText
    {
        get => _toolbarInputBackendLabelText;
        set => SetProperty(ref _toolbarInputBackendLabelText, value);
    }

    public string SelectedPressureCurve
    {
        get => _selectedPressureCurve;
        set => SetProperty(ref _selectedPressureCurve, value);
    }

    public string SelectedInputBackendMode
    {
        get => _selectedInputBackendMode;
        set => SetProperty(ref _selectedInputBackendMode, value);
    }

    public bool IsPressureCurveLinearChecked
    {
        get => _isPressureCurveLinearChecked;
        set => SetProperty(ref _isPressureCurveLinearChecked, value);
    }

    public bool IsPressureCurveLogarithmicChecked
    {
        get => _isPressureCurveLogarithmicChecked;
        set => SetProperty(ref _isPressureCurveLogarithmicChecked, value);
    }

    public bool IsPressureCurveExponentialChecked
    {
        get => _isPressureCurveExponentialChecked;
        set => SetProperty(ref _isPressureCurveExponentialChecked, value);
    }

    public bool IsInputBackendAutoChecked
    {
        get => _isInputBackendAutoChecked;
        set => SetProperty(ref _isInputBackendAutoChecked, value);
    }

    public bool IsInputBackendWintabChecked
    {
        get => _isInputBackendWintabChecked;
        set => SetProperty(ref _isInputBackendWintabChecked, value);
    }

    public bool IsInputBackendMouseChecked
    {
        get => _isInputBackendMouseChecked;
        set => SetProperty(ref _isInputBackendMouseChecked, value);
    }

    public bool SendPressureGlobally
    {
        get => _sendPressureGlobally;
        set
        {
            if (!SetProperty(ref _sendPressureGlobally, value) || _isSyncing)
            {
                return;
            }

            _runtimeConfig.Current.SendPressureGlobally = value;
            PressureChromeRefreshRequested?.Invoke();
        }
    }

    public bool AllowZoneSwitchWhilePressed
    {
        get => _allowZoneSwitchWhilePressed;
        set
        {
            if (!SetProperty(ref _allowZoneSwitchWhilePressed, value) || _isSyncing)
            {
                return;
            }

            _runtimeConfig.Current.AllowZoneSwitchWhilePressed = value;
            RoutingStatusRefreshRequested?.Invoke();
        }
    }

    public void SyncFromConfig()
    {
        _isSyncing = true;
        AppConfig config = _runtimeConfig.Current;
        SendPressureGlobally = config.SendPressureGlobally;
        AllowZoneSwitchWhilePressed = config.AllowZoneSwitchWhilePressed;
        ToolbarState toolbarState = _uiStateProjectionService.BuildToolbarState(config);
        ToolbarPressureLabelText = toolbarState.ToolbarPressureLabelText;
        SelectedPressureCurve = toolbarState.SelectedPressureCurve;
        ToolbarInputBackendLabelText = toolbarState.ToolbarInputBackendLabelText;
        SelectedInputBackendMode = toolbarState.SelectedInputBackendMode;
        IsPressureCurveLinearChecked = toolbarState.IsPressureCurveLinearChecked;
        IsPressureCurveLogarithmicChecked = toolbarState.IsPressureCurveLogarithmicChecked;
        IsPressureCurveExponentialChecked = toolbarState.IsPressureCurveExponentialChecked;
        IsInputBackendAutoChecked = toolbarState.IsInputBackendAutoChecked;
        IsInputBackendWintabChecked = toolbarState.IsInputBackendWintabChecked;
        IsInputBackendMouseChecked = toolbarState.IsInputBackendMouseChecked;
        _isSyncing = false;
        PressureChromeRefreshRequested?.Invoke();
    }

    private void SetPressureCurve(string curve)
    {
        _toolbarSession.ApplyPressureCurve(_runtimeConfig.Current, curve);
        SyncFromConfig();
    }

    private void SetInputBackendMode(string mode)
    {
        ToolbarInputModeApplyResult result = _toolbarSession.TryApplyInputBackendMode(_runtimeConfig.Current, mode);
        SyncFromConfig();
        if (result.RequiresInputRebind)
        {
            InputBackendRebindRequested?.Invoke();
        }
    }

    private void CycleInputBackend()
    {
        SetInputBackendMode(_toolbarSession.GetNextCycledInputBackendModeLabel(_runtimeConfig.Current));
    }
}
