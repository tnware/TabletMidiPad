using System.Collections.ObjectModel;
using System.Globalization;

namespace TabletMidiPad.ViewModels;

/// <summary>
/// View-model surface for editable preferences fields.
/// </summary>
internal sealed class PreferencesViewModel : ViewModelBase
{
    private string _dawPortName = string.Empty;
    private string _appPortName = string.Empty;
    private string _associationId = string.Empty;
    private string _mousePressureText = "127";
    private string _positionDeadZoneText = "0.003";
    private string _positionSmoothingText = "0.35";
    private string _pressureCurve = "Linear";
    private string _inputBackendMode = "Auto";
    private string _lowerPenButtonAction = PenButtonActions.None;
    private string _lowerPenButtonMidiChannelText = "1";
    private string _lowerPenButtonCcText = "64";
    private string _lowerPenButtonNoteText = "60";
    private string _upperPenButtonAction = PenButtonActions.None;
    private string _upperPenButtonMidiChannelText = "1";
    private string _upperPenButtonCcText = "65";
    private string _upperPenButtonNoteText = "61";
    private bool _sendPressureGlobally = true;
    private bool _allowZoneSwitchWhilePressed = true;
    private bool _allowPageSwitchWhilePressed;
    private bool _wrapPageNavigation = true;
    private string _midiLifecycleStatusText = string.Empty;
    private bool _isMidiLifecycleBusy;

    public string DawPortName
    {
        get => _dawPortName;
        set => SetProperty(ref _dawPortName, value);
    }

    public string AppPortName
    {
        get => _appPortName;
        set => SetProperty(ref _appPortName, value);
    }

    public string AssociationId
    {
        get => _associationId;
        set => SetProperty(ref _associationId, value);
    }

    public string MousePressureText
    {
        get => _mousePressureText;
        set => SetProperty(ref _mousePressureText, value);
    }

    public string PositionDeadZoneText
    {
        get => _positionDeadZoneText;
        set => SetProperty(ref _positionDeadZoneText, value);
    }

    public string PositionSmoothingText
    {
        get => _positionSmoothingText;
        set => SetProperty(ref _positionSmoothingText, value);
    }

    public string PressureCurve
    {
        get => _pressureCurve;
        set => SetProperty(ref _pressureCurve, value);
    }

    public string InputBackendMode
    {
        get => _inputBackendMode;
        set => SetProperty(ref _inputBackendMode, value);
    }

    public string LowerPenButtonAction
    {
        get => _lowerPenButtonAction;
        set => SetProperty(ref _lowerPenButtonAction, value);
    }

    public string LowerPenButtonMidiChannelText
    {
        get => _lowerPenButtonMidiChannelText;
        set => SetProperty(ref _lowerPenButtonMidiChannelText, value);
    }

    public string LowerPenButtonCcText
    {
        get => _lowerPenButtonCcText;
        set => SetProperty(ref _lowerPenButtonCcText, value);
    }

    public string LowerPenButtonNoteText
    {
        get => _lowerPenButtonNoteText;
        set => SetProperty(ref _lowerPenButtonNoteText, value);
    }

    public string UpperPenButtonAction
    {
        get => _upperPenButtonAction;
        set => SetProperty(ref _upperPenButtonAction, value);
    }

    public string UpperPenButtonMidiChannelText
    {
        get => _upperPenButtonMidiChannelText;
        set => SetProperty(ref _upperPenButtonMidiChannelText, value);
    }

    public string UpperPenButtonCcText
    {
        get => _upperPenButtonCcText;
        set => SetProperty(ref _upperPenButtonCcText, value);
    }

    public string UpperPenButtonNoteText
    {
        get => _upperPenButtonNoteText;
        set => SetProperty(ref _upperPenButtonNoteText, value);
    }

    public bool SendPressureGlobally
    {
        get => _sendPressureGlobally;
        set => SetProperty(ref _sendPressureGlobally, value);
    }

    public bool AllowZoneSwitchWhilePressed
    {
        get => _allowZoneSwitchWhilePressed;
        set => SetProperty(ref _allowZoneSwitchWhilePressed, value);
    }

    public bool AllowPageSwitchWhilePressed
    {
        get => _allowPageSwitchWhilePressed;
        set => SetProperty(ref _allowPageSwitchWhilePressed, value);
    }

    public bool WrapPageNavigation
    {
        get => _wrapPageNavigation;
        set => SetProperty(ref _wrapPageNavigation, value);
    }

    public string MidiLifecycleStatusText
    {
        get => _midiLifecycleStatusText;
        set => SetProperty(ref _midiLifecycleStatusText, value);
    }

    public bool IsMidiLifecycleBusy
    {
        get => _isMidiLifecycleBusy;
        set => SetProperty(ref _isMidiLifecycleBusy, value);
    }

    public ObservableCollection<ExpressKeyRowViewModel> ExpressKeys { get; } = new();

    /// <summary>
    /// Ensures the express-key rows collection has at least the requested count.
    /// </summary>
    /// <param name="count">The minimum row count.</param>
    public void EnsureExpressKeyCount(int count)
    {
        while (ExpressKeys.Count < count)
        {
            int index = ExpressKeys.Count;
            ExpressKeys.Add(new ExpressKeyRowViewModel
            {
                Action = PenButtonActions.None,
                MidiChannelText = "1",
                CcText = Math.Clamp(70 + index, 0, 127).ToString(CultureInfo.InvariantCulture),
                NoteText = Math.Clamp(36 + index, 0, 127).ToString(CultureInfo.InvariantCulture)
            });
        }
    }
}
