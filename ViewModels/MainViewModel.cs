using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using TabletMidiPad.Infrastructure;
using TabletMidiPad.Models;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IMidiService _midiService;
    private readonly ITabletService _tabletService;

    public AppConfig Config => _settingsService.Config;
    public ObservableCollection<ZoneConfig> Zones { get; }

    private string _statusText = "Waiting for MIDI port...";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    private string _routingText = string.Empty;
    public string RoutingText { get => _routingText; set => SetProperty(ref _routingText, value); }

    private string _inputBackendText = string.Empty;
    public string InputBackendText { get => _inputBackendText; set => SetProperty(ref _inputBackendText, value); }

    private string _activeZoneText = "Active zone: none";
    public string ActiveZoneText { get => _activeZoneText; set => SetProperty(ref _activeZoneText, value); }

    private string _valueText = "X: -, Y: -, Pressure: -";
    public string ValueText { get => _valueText; set => SetProperty(ref _valueText, value); }

    private string _penButtonsText = "Pen: -";
    public string PenButtonsText { get => _penButtonsText; set => SetProperty(ref _penButtonsText, value); }

    private string _modifiersText = "Mod: none";
    public string ModifiersText { get => _modifiersText; set => SetProperty(ref _modifiersText, value); }

    private bool _isMidiActive;
    public bool IsMidiActive { get => _isMidiActive; set => SetProperty(ref _isMidiActive, value); }

    private ZoneConfig? _activeZone;
    private bool _lowerPenButtonHeld;
    private bool _upperPenButtonHeld;
    private bool _lowerPenButtonToggleState;
    private bool _upperPenButtonToggleState;
    private bool _hasLockedPosition;
    private Point _lockedLocalPosition;
    private readonly Dictionary<int, bool> _expressKeyHeld = new();
    private readonly Dictionary<int, bool> _expressKeyToggleState = new();
    private readonly Dictionary<ZoneConfig, Point> _smoothedZonePoints = new();

    public MainViewModel(ISettingsService settingsService, IMidiService midiService, ITabletService tabletService)
    {
        _settingsService = settingsService;
        _midiService = midiService;
        _tabletService = tabletService;

        Zones = new ObservableCollection<ZoneConfig>(_settingsService.Config.Zones);
        Zones.CollectionChanged += (s, e) => {
            if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
                foreach (ZoneConfig z in e.NewItems) Config.Zones.Add(z);
            if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
                foreach (ZoneConfig z in e.OldItems) Config.Zones.Remove(z);
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                Config.Zones.Clear();
                foreach (var z in Zones) Config.Zones.Add(z);
            }
        };

        _tabletService.PacketReceived += OnTabletPacketReceived;
        _tabletService.ExpressKeyReceived += OnExpressKeyReceived;
        _tabletService.ProximityChanged += OnProximityChanged;
    }

    public async Task InitializeAsync(IntPtr windowHandle)
    {
        _tabletService.InputBackendMode = Config.InputBackendMode;
        _tabletService.Initialize(windowHandle, null, WintabInterop.GetExtensionMask(WintabInterop.WTX_EXPKEYS2));
        await RestartMidiAsync();
    }

    public async Task RestartMidiAsync()
    {
        IsMidiActive = false;
        StatusText = "Connecting MIDI...";
        try
        {
            await Task.Run(() => _midiService.Initialize(Config.AppPortName, Config.DawPortName, Config.LoopbackAssociationId));
            StatusText = "Connected";
            RoutingText = $"Send to '{Config.DawPortName}' via '{Config.AppPortName}'";
            IsMidiActive = true;
        }
        catch (Exception ex)
        {
            StatusText = "MIDI init failed";
            RoutingText = ex.Message;
        }
        UpdateInputBackendStatus();
    }

    private void OnTabletPacketReceived(TabletPacket packet, TabletContextInfo context)
    {
        double xNorm = Normalize(packet.X, context.OutputMinX, context.OutputMaxX);
        double yNorm = Normalize(packet.Y, context.OutputMinY, context.OutputMaxY);
        int pressure = context.PressureMax > 0 ? (int)Math.Round(packet.NormalPressure * 127.0 / context.PressureMax) : 127;

        UpdatePenButtonActionState(packet.Buttons);
        ProcessInteraction(xNorm, yNorm, pressure);
    }

    private void ProcessInteraction(double xNorm, double yNorm, int pressure)
    {
        var zone = ResolveZone(xNorm, yNorm);
        if (!ReferenceEquals(zone, _activeZone))
        {
            SendZoneRelease(_activeZone);
            _activeZone = zone;
        }

        if (zone == null)
        {
            ActiveZoneText = "Active zone: none";
            ValueText = "Outside configured zones";
            return;
        }

        var local = NormalizeWithinZone(zone, xNorm, yNorm);
        local = ApplyPositionConditioning(zone, local.x, local.y);

        string lowerAction = NormalizePenButtonAction(Config.LowerPenButtonAction);
        string upperAction = NormalizePenButtonAction(Config.UpperPenButtonAction);

        bool lockXY = (_lowerPenButtonHeld && lowerAction == PenButtonActions.LockXY) || (_upperPenButtonHeld && upperAction == PenButtonActions.LockXY);
        bool lockX = (_lowerPenButtonHeld && lowerAction == PenButtonActions.LockX) || (_upperPenButtonHeld && upperAction == PenButtonActions.LockX);
        bool lockY = (_lowerPenButtonHeld && lowerAction == PenButtonActions.LockY) || (_upperPenButtonHeld && upperAction == PenButtonActions.LockY);

        if (lockXY || lockX || lockY)
        {
            if (!_hasLockedPosition) { _lockedLocalPosition = new Point(local.x, local.y); _hasLockedPosition = true; }
            local = (lockXY || lockX ? _lockedLocalPosition.X : local.x, lockXY || lockY ? _lockedLocalPosition.Y : local.y);
        }
        else { _hasLockedPosition = false; }

        int x = ToCc(local.x, zone.InvertX);
        int y = ToCc(local.y, zone.InvertY);
        int curvedPressure = ApplyPressureCurve(pressure);

        ActiveZoneText = $"Active zone: {zone.Name}";
        bool sendPressure = Config.SendPressureGlobally && zone.SendPressure;

        if ((_lowerPenButtonHeld && lowerAction == PenButtonActions.DisablePressure) || (_upperPenButtonHeld && upperAction == PenButtonActions.DisablePressure)) sendPressure = false;

        bool pressureEnabledByButton = (_lowerPenButtonHeld && lowerAction == PenButtonActions.EnablePressure) || (_upperPenButtonHeld && upperAction == PenButtonActions.EnablePressure);
        if (lowerAction == PenButtonActions.EnablePressure || upperAction == PenButtonActions.EnablePressure) sendPressure = sendPressure && pressureEnabledByButton;

        if (zone.SendX) _midiService.SendCc(zone.MidiChannel, zone.CcX, x);
        if (zone.SendY) _midiService.SendCc(zone.MidiChannel, zone.CcY, y);
        if (sendPressure) _midiService.SendCc(zone.MidiChannel, zone.CcPressure, curvedPressure);

        ValueText = $"{zone.Name} -> X: {(zone.SendX ? x.ToString() : "off")}, Y: {(zone.SendY ? y.ToString() : "off")}, P: {(sendPressure ? curvedPressure.ToString() : "off")}";
    }

    private ZoneConfig? ResolveZone(double xNorm, double yNorm)
    {
        if (!Config.AllowZoneSwitchWhilePressed && _activeZone != null) return _activeZone;
        return Zones.FirstOrDefault(z => z.Enabled && xNorm >= z.Left && xNorm <= z.Left + z.Width && yNorm >= z.Top && yNorm <= z.Top + z.Height);
    }

    private void SendZoneRelease(ZoneConfig? zone)
    {
        if (zone == null || !zone.Enabled) return;
        string behavior = NormalizeReleaseBehavior(zone.ReleaseBehavior);
        if (behavior == ZoneReleaseBehaviors.Hold || behavior == ZoneReleaseBehaviors.NoSendOnRelease) return;

        if (behavior == ZoneReleaseBehaviors.ResetAll)
        {
            if (zone.SendX) _midiService.SendCc(zone.MidiChannel, zone.CcX, 0, true);
            if (zone.SendY) _midiService.SendCc(zone.MidiChannel, zone.CcY, 0, true);
        }
        if (Config.SendPressureGlobally && zone.SendPressure) _midiService.SendCc(zone.MidiChannel, zone.CcPressure, 0, true);
    }

    private void OnExpressKeyReceived(ExpressKeyPacket packet)
    {
        int control = packet.Control;
        bool isPressed = packet.IsPressed;
        _expressKeyHeld.TryGetValue(control, out bool wasHeld);
        _expressKeyToggleState.TryGetValue(control, out bool toggleState);

        bool justPressed = isPressed && !wasHeld;
        bool justReleased = !isPressed && wasHeld;

        if (control < Config.ExpressKeys.Count)
        {
            var keyConfig = Config.ExpressKeys[control];
            string action = NormalizePenButtonAction(keyConfig.Action);
            if (action == PenButtonActions.CcMomentary)
            {
                if (justPressed) _midiService.SendCc(keyConfig.MidiChannel, keyConfig.Cc, 127, true);
                else if (justReleased) _midiService.SendCc(keyConfig.MidiChannel, keyConfig.Cc, 0, true);
            }
            else if (action == PenButtonActions.CcToggle && justPressed)
            {
                toggleState = !toggleState;
                _midiService.SendCc(keyConfig.MidiChannel, keyConfig.Cc, toggleState ? 127 : 0, true);
            }
            else if (action == PenButtonActions.NoteMomentary)
            {
                if (justPressed) _midiService.SendNote(keyConfig.MidiChannel, keyConfig.Note, true, 127, true);
                else if (justReleased) _midiService.SendNote(keyConfig.MidiChannel, keyConfig.Note, false, 0, true);
            }
            else if (action == PenButtonActions.NoteToggle && justPressed)
            {
                toggleState = !toggleState;
                _midiService.SendNote(keyConfig.MidiChannel, keyConfig.Note, toggleState, toggleState ? 127 : 0, true);
            }
        }

        _expressKeyHeld[control] = isPressed;
        _expressKeyToggleState[control] = toggleState;
        UpdateInputBackendStatus();
    }

    private void OnProximityChanged(bool entering)
    {
        if (!entering && _activeZone != null)
        {
            SendZoneRelease(_activeZone);
            _activeZone = null;
            ActiveZoneText = "Active zone: none";
        }
    }

    private void UpdatePenButtonActionState(uint buttons)
    {
        HandlePenButtonAction((buttons & 0x0002) != 0, ref _lowerPenButtonHeld, ref _lowerPenButtonToggleState, Config.LowerPenButtonAction, Config.LowerPenButtonMidiChannel, Config.LowerPenButtonCc, Config.LowerPenButtonNote);
        HandlePenButtonAction((buttons & 0x0004) != 0, ref _upperPenButtonHeld, ref _upperPenButtonToggleState, Config.UpperPenButtonAction, Config.UpperPenButtonMidiChannel, Config.UpperPenButtonCc, Config.UpperPenButtonNote);
        UpdateModifiersText();
    }

    private void HandlePenButtonAction(bool isHeld, ref bool heldState, ref bool toggleState, string action, int channel, int cc, int note)
    {
        bool justPressed = isHeld && !heldState;
        bool justReleased = !isHeld && heldState;
        if (justPressed) _hasLockedPosition = false;

        action = NormalizePenButtonAction(action);
        if (action == PenButtonActions.CcMomentary)
        {
            if (justPressed) _midiService.SendCc(channel, cc, 127, true);
            else if (justReleased) _midiService.SendCc(channel, cc, 0, true);
        }
        else if (action == PenButtonActions.CcToggle && justPressed)
        {
            toggleState = !toggleState;
            _midiService.SendCc(channel, cc, toggleState ? 127 : 0, true);
        }
        else if (action == PenButtonActions.NoteMomentary)
        {
            if (justPressed) _midiService.SendNote(channel, note, true, 127, true);
            else if (justReleased) _midiService.SendNote(channel, note, false, 0, true);
        }
        else if (action == PenButtonActions.NoteToggle && justPressed)
        {
            toggleState = !toggleState;
            _midiService.SendNote(channel, note, toggleState, toggleState ? 127 : 0, true);
        }
        if (justReleased) _hasLockedPosition = false;
        heldState = isHeld;
    }

    private void UpdateModifiersText()
    {
        var parts = new List<string>();
        AppendModifier(parts, "lower", Config.LowerPenButtonAction, _lowerPenButtonHeld, _lowerPenButtonToggleState);
        AppendModifier(parts, "upper", Config.UpperPenButtonAction, _upperPenButtonHeld, _upperPenButtonToggleState);
        ModifiersText = parts.Count > 0 ? $"Modifiers: {string.Join(" | ", parts)}" : "Modifiers: none";
    }

    private void AppendModifier(List<string> parts, string label, string action, bool held, bool toggled)
    {
        action = NormalizePenButtonAction(action);
        string? text = action switch
        {
            PenButtonActions.LockXY when held => $"{label} XY lock",
            PenButtonActions.LockX when held => $"{label} X lock",
            PenButtonActions.LockY when held => $"{label} Y lock",
            PenButtonActions.DisablePressure when held => $"{label} pressure off",
            PenButtonActions.EnablePressure when held => $"{label} pressure on",
            PenButtonActions.CcToggle => $"{label} CC {(toggled ? "on" : "off")}",
            PenButtonActions.NoteToggle => $"{label} note {(toggled ? "on" : "off")}",
            _ => null
        };
        if (text != null) parts.Add(text);
    }

    private (double x, double y) ApplyPositionConditioning(ZoneConfig zone, double x, double y)
    {
        if (!_smoothedZonePoints.TryGetValue(zone, out Point lastPoint))
        {
            _smoothedZonePoints[zone] = new Point(x, y);
            return (x, y);
        }
        double alpha = 1.0 - Math.Clamp(Config.PositionSmoothing, 0, 0.95);
        double targetX = Math.Abs(x - lastPoint.X) < Config.PositionDeadZone ? lastPoint.X : x;
        double targetY = Math.Abs(y - lastPoint.Y) < Config.PositionDeadZone ? lastPoint.Y : y;
        var filtered = new Point(Math.Clamp(lastPoint.X + (targetX - lastPoint.X) * alpha, 0, 1), Math.Clamp(lastPoint.Y + (targetY - lastPoint.Y) * alpha, 0, 1));
        _smoothedZonePoints[zone] = filtered;
        return (filtered.X, filtered.Y);
    }

    private void UpdateInputBackendStatus() => InputBackendText = $"Input: {_tabletService.GetDiagnostics()}";
    private static double Normalize(int val, int min, int max) => max <= min ? 0 : Math.Clamp((val - min) / (double)(max - min), 0, 1);
    private static int ToCc(double norm, bool invert) => (int)Math.Round(Math.Clamp(invert ? 1 - norm : norm, 0, 1) * 127);
    private static (double x, double y) NormalizeWithinZone(ZoneConfig z, double x, double y) => (Math.Clamp((x - z.Left) / z.Width, 0, 1), Math.Clamp((y - z.Top) / z.Height, 0, 1));
    private int ApplyPressureCurve(int p) { double n = p / 127.0; double c = Config.PressureCurve switch { "Logarithmic" => Math.Sqrt(n), "Exponential" => Math.Pow(n, 2), _ => n }; return (int)Math.Round(c * 127); }
    private string NormalizePenButtonAction(string? a) => a ?? PenButtonActions.None;
    private string NormalizeReleaseBehavior(string? b) => b ?? ZoneReleaseBehaviors.NoSendOnRelease;

    public void LoadFromConfig()
    {
        Zones.Clear();
        foreach (var z in Config.Zones) Zones.Add(z);
        OnPropertyChanged(nameof(Config));
    }

    public void ResetInteraction()
    {
        SendZoneRelease(_activeZone);
        _activeZone = null;
        ActiveZoneText = "Active zone: none";
    }

    public void SaveSettings() => _settingsService.Save();
    public void ResetToDefaults() { _settingsService.ResetToDefaults(); LoadFromConfig(); }
}
