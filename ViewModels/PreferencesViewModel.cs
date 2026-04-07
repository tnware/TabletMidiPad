using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using TabletMidiPad.Infrastructure;
using TabletMidiPad.Models;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

public sealed class ExpressKeyViewModel : ObservableObject
{
    private string _action;
    private int _midiChannel;
    private int _cc;
    private int _note;

    public ExpressKeyViewModel(ExpressKeyConfig config, int index)
    {
        _action = config.Action;
        _midiChannel = config.MidiChannel;
        _cc = config.Cc;
        _note = config.Note;
        Index = index;
    }

    public int Index { get; }
    public string Action { get => _action; set => SetProperty(ref _action, value); }
    public int MidiChannel { get => _midiChannel; set => SetProperty(ref _midiChannel, value); }
    public int Cc { get => _cc; set => SetProperty(ref _cc, value); }
    public int Note { get => _note; set => SetProperty(ref _note, value); }

    public ExpressKeyConfig ToConfig() => new() { Action = Action, MidiChannel = MidiChannel, Cc = Cc, Note = Note };
}

public sealed class PreferencesViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ITabletService _tablet;

    private string _dawPortName = "";
    private string _appPortName = "";
    private string _associationId = "";
    private int _mousePressureValue;
    private double _positionDeadZone;
    private double _positionSmoothing;
    private string _pressureCurve = "Linear";
    private string _inputBackendMode = "Auto";
    private string _lowerPenButtonAction = "None";
    private int _lowerPenButtonMidiChannel = 1;
    private int _lowerPenButtonCc = 64;
    private int _lowerPenButtonNote = 60;
    private string _upperPenButtonAction = "None";
    private int _upperPenButtonMidiChannel = 1;
    private int _upperPenButtonCc = 65;
    private int _upperPenButtonNote = 61;
    private bool _sendPressureGlobally;
    private bool _allowZoneSwitchWhilePressed;
    private string _errorText = "";

    public PreferencesViewModel(ISettingsService settings, ITabletService tablet)
    {
        _settings = settings;
        _tablet = tablet;

        MidiChannels = Enumerable.Range(1, 16).ToList();
        PressureCurves = new List<string> { "Linear", "Logarithmic", "Exponential" };
        InputBackendModes = new List<string> { "Auto", "Wintab Only", "Stylus/Mouse Only" };
        PenButtonActionsList = new List<string> {
            PenButtonActions.None, PenButtonActions.LockXY, PenButtonActions.LockX, PenButtonActions.LockY,
            PenButtonActions.DisablePressure, PenButtonActions.EnablePressure,
            PenButtonActions.CcMomentary, PenButtonActions.CcToggle,
            PenButtonActions.NoteMomentary, PenButtonActions.NoteToggle
        };

        LoadFromConfig();

        OkCommand = new RelayCommand(_ => Accept());
        CancelCommand = new RelayCommand(_ => Cancel());
    }

    public List<int> MidiChannels { get; }
    public List<string> PressureCurves { get; }
    public List<string> InputBackendModes { get; }
    public List<string> PenButtonActionsList { get; }

    public string DawPortName { get => _dawPortName; set => SetProperty(ref _dawPortName, value); }
    public string AppPortName { get => _appPortName; set => SetProperty(ref _appPortName, value); }
    public string AssociationId { get => _associationId; set => SetProperty(ref _associationId, value); }
    public int MousePressureValue { get => _mousePressureValue; set => SetProperty(ref _mousePressureValue, value); }
    public double PositionDeadZone { get => _positionDeadZone; set => SetProperty(ref _positionDeadZone, value); }
    public double PositionSmoothing { get => _positionSmoothing; set => SetProperty(ref _positionSmoothing, value); }
    public string PressureCurve { get => _pressureCurve; set => SetProperty(ref _pressureCurve, value); }
    public string InputBackendMode { get => _inputBackendMode; set => SetProperty(ref _inputBackendMode, value); }
    public string LowerPenButtonAction { get => _lowerPenButtonAction; set => SetProperty(ref _lowerPenButtonAction, value); }
    public int LowerPenButtonMidiChannel { get => _lowerPenButtonMidiChannel; set => SetProperty(ref _lowerPenButtonMidiChannel, value); }
    public int LowerPenButtonCc { get => _lowerPenButtonCc; set => SetProperty(ref _lowerPenButtonCc, value); }
    public int LowerPenButtonNote { get => _lowerPenButtonNote; set => SetProperty(ref _lowerPenButtonNote, value); }
    public string UpperPenButtonAction { get => _upperPenButtonAction; set => SetProperty(ref _upperPenButtonAction, value); }
    public int UpperPenButtonMidiChannel { get => _upperPenButtonMidiChannel; set => SetProperty(ref _upperPenButtonMidiChannel, value); }
    public int UpperPenButtonCc { get => _upperPenButtonCc; set => SetProperty(ref _upperPenButtonCc, value); }
    public int UpperPenButtonNote { get => _upperPenButtonNote; set => SetProperty(ref _upperPenButtonNote, value); }
    public bool SendPressureGlobally { get => _sendPressureGlobally; set => SetProperty(ref _sendPressureGlobally, value); }
    public bool AllowZoneSwitchWhilePressed { get => _allowZoneSwitchWhilePressed; set => SetProperty(ref _allowZoneSwitchWhilePressed, value); }
    public string ErrorText { get => _errorText; set => SetProperty(ref _errorText, value); }

    public ObservableCollection<ExpressKeyViewModel> ExpressKeys { get; } = new();

    public ICommand OkCommand { get; }
    public ICommand CancelCommand { get; }

    public event Action? RequestClose;
    public bool Accepted { get; private set; }

    private void LoadFromConfig()
    {
        var config = _settings.Config;
        DawPortName = config.DawPortName;
        AppPortName = config.AppPortName;
        AssociationId = config.LoopbackAssociationId;
        MousePressureValue = config.MousePressureValue;
        PositionDeadZone = config.PositionDeadZone;
        PositionSmoothing = config.PositionSmoothing;
        PressureCurve = config.PressureCurve;
        InputBackendMode = config.InputBackendMode;
        LowerPenButtonAction = config.LowerPenButtonAction;
        LowerPenButtonMidiChannel = config.LowerPenButtonMidiChannel;
        LowerPenButtonCc = config.LowerPenButtonCc;
        LowerPenButtonNote = config.LowerPenButtonNote;
        UpperPenButtonAction = config.UpperPenButtonAction;
        UpperPenButtonMidiChannel = config.UpperPenButtonMidiChannel;
        UpperPenButtonCc = config.UpperPenButtonCc;
        UpperPenButtonNote = config.UpperPenButtonNote;
        SendPressureGlobally = config.SendPressureGlobally;
        AllowZoneSwitchWhilePressed = config.AllowZoneSwitchWhilePressed;

        ExpressKeys.Clear();
        ExpressKeyConfig.EnsureCount(config.ExpressKeys, _tablet.ExpressKeyCount);
        for (int i = 0; i < config.ExpressKeys.Count; i++)
        {
            ExpressKeys.Add(new ExpressKeyViewModel(config.ExpressKeys[i], i));
        }
    }

    private void Accept()
    {
        try
        {
            Validate();
            SaveToConfig();
            Accepted = true;
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorText = ex.Message;
        }
    }

    private void Cancel() => RequestClose?.Invoke();

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(DawPortName)) throw new InvalidOperationException("DAW Input Port is required.");
        if (string.IsNullOrWhiteSpace(AppPortName)) throw new InvalidOperationException("Internal App Port is required.");
        if (!Guid.TryParse(AssociationId, out _)) throw new InvalidOperationException("Association Id must be a valid GUID.");
    }

    private void SaveToConfig()
    {
        var config = _settings.Config;
        config.DawPortName = DawPortName;
        config.AppPortName = AppPortName;
        config.LoopbackAssociationId = AssociationId;
        config.MousePressureValue = MousePressureValue;
        config.PositionDeadZone = PositionDeadZone;
        config.PositionSmoothing = PositionSmoothing;
        config.PressureCurve = PressureCurve;
        config.InputBackendMode = InputBackendMode;
        config.LowerPenButtonAction = LowerPenButtonAction;
        config.LowerPenButtonMidiChannel = LowerPenButtonMidiChannel;
        config.LowerPenButtonCc = LowerPenButtonCc;
        config.LowerPenButtonNote = LowerPenButtonNote;
        config.UpperPenButtonAction = UpperPenButtonAction;
        config.UpperPenButtonMidiChannel = UpperPenButtonMidiChannel;
        config.UpperPenButtonCc = UpperPenButtonCc;
        config.UpperPenButtonNote = UpperPenButtonNote;
        config.SendPressureGlobally = SendPressureGlobally;
        config.AllowZoneSwitchWhilePressed = AllowZoneSwitchWhilePressed;
        config.ExpressKeys = ExpressKeys.Select(ek => ek.ToConfig()).ToList();
        _settings.Save();
    }
}
