using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TabletMidiPad.Infrastructure;
using TabletMidiPad.Models;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IMidiService _midi;
    private readonly ITabletService _tablet;

    private string _statusText = "Waiting for MIDI port...";
    private string _routingText = "";
    private string _activeZoneText = "Active zone: none";
    private string _valueText = "X: -, Y: -, Pressure: -";
    private string _penButtonsText = "Pen: -";
    private string _modifiersText = "Modifiers: none";
    private string _inputBackendText = "";
    private string _configSummaryText = "";
    private string _settingsResultText = "";
    private bool _isLoading;
    private double _loadingProgress;
    private string _loadingTitle = "";
    private string _loadingDetail = "";
    private bool _sendPressureGlobally;
    private bool _allowZoneSwitchWhilePressed;
    private string _pressureCurve = "Linear";
    private string _inputBackendMode = "Auto";
    private int _selectedZoneIndex = -1;
    private ZoneConfig? _selectedZone;

    private ZoneConfig? _activeZone;
    private bool _isActive;
    private bool _lowerPenButtonHeld;
    private bool _upperPenButtonHeld;
    private bool _lowerPenButtonToggleState;
    private bool _upperPenButtonToggleState;
    private bool _hasLockedPosition;
    private Point _lockedLocalPosition;
    private readonly Dictionary<ZoneConfig, Point> _lastZonePoints = new();
    private readonly Dictionary<ZoneConfig, Point> _smoothedZonePoints = new();
    private readonly Dictionary<int, bool> _expressKeyHeld = new();
    private readonly Dictionary<int, bool> _expressKeyToggleState = new();

    public MainViewModel(ISettingsService settings, IMidiService midi, ITabletService tablet)
    {
        _settings = settings;
        _midi = midi;
        _tablet = tablet;

        _tablet.LoadingStateChanged += (title, detail) => { LoadingTitle = title; LoadingDetail = detail; };
        _tablet.LoadingProgressChanged += (progress) => LoadingProgress = progress;
        _tablet.WintabPacketReceived += HandleWintabPacket;
        _tablet.ExpressKeyPacketReceived += HandleExpressKeyPacket;
        _tablet.ProximityLost += HandleProximityLost;

        SaveCommand = new RelayCommand(_ => ApplySettings());
        ResetDefaultsCommand = new RelayCommand(_ => ResetDefaults());
        AddZoneCommand = new RelayCommand(_ => AddZone());
        RemoveZoneCommand = new RelayCommand(_ => RemoveZone());
        AutoTileCommand = new RelayCommand(_ => AutoTile());
        ReconnectCommand = new RelayCommand(_ => RebindInputBackend());
        StartWacomServiceCommand = new RelayCommand(async _ => await StartWacomServiceAsync());
        CycleInputBackendCommand = new RelayCommand(_ => CycleInputBackend());
        SetPressureCurveCommand = new RelayCommand<string>(curve => SetPressureCurve(curve!));
        SetInputBackendModeCommand = new RelayCommand<string>(mode => SetInputBackendMode(mode!));
    }

    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public string RoutingText { get => _routingText; set => SetProperty(ref _routingText, value); }
    public string ActiveZoneText { get => _activeZoneText; set => SetProperty(ref _activeZoneText, value); }
    public string ValueText { get => _valueText; set => SetProperty(ref _valueText, value); }
    public string PenButtonsText { get => _penButtonsText; set => SetProperty(ref _penButtonsText, value); }
    public string ModifiersText { get => _modifiersText; set => SetProperty(ref _modifiersText, value); }
    public string InputBackendText { get => _inputBackendText; set => SetProperty(ref _inputBackendText, value); }
    public string ConfigSummaryText { get => _configSummaryText; set => SetProperty(ref _configSummaryText, value); }
    public string SettingsResultText { get => _settingsResultText; set => SetProperty(ref _settingsResultText, value); }
    public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }
    public double LoadingProgress { get => _loadingProgress; set => SetProperty(ref _loadingProgress, value); }
    public string LoadingTitle { get => _loadingTitle; set => SetProperty(ref _loadingTitle, value); }
    public string LoadingDetail { get => _loadingDetail; set => SetProperty(ref _loadingDetail, value); }

    public bool SendPressureGlobally
    {
        get => _sendPressureGlobally;
        set { if (SetProperty(ref _sendPressureGlobally, value)) { _settings.Config.SendPressureGlobally = value; OnPropertyChanged(nameof(PressureLabel)); } }
    }

    public bool AllowZoneSwitchWhilePressed
    {
        get => _allowZoneSwitchWhilePressed;
        set { if (SetProperty(ref _allowZoneSwitchWhilePressed, value)) { _settings.Config.AllowZoneSwitchWhilePressed = value; UpdateConfigSummary(); } }
    }

    public string PressureCurve
    {
        get => _pressureCurve;
        set { if (SetProperty(ref _pressureCurve, value)) { _settings.Config.PressureCurve = value; OnPropertyChanged(nameof(PressureLabel)); } }
    }

    public string InputBackendMode
    {
        get => _inputBackendMode;
        set { if (SetProperty(ref _inputBackendMode, value)) { _settings.Config.InputBackendMode = value; OnPropertyChanged(nameof(InputBackendLabel)); } }
    }

    public string PressureLabel => $"Pressure ({(_pressureCurve == "Logarithmic" ? "Log" : _pressureCurve == "Exponential" ? "Exp" : "Lin")})";
    public string InputBackendLabel => NormalizeInputBackendMode(_inputBackendMode) switch { "Wintab Only" => "Input (Wintab)", "Stylus/Mouse Only" => "Input (Mouse)", _ => "Input (Auto)" };
    public string WacomServiceStatus => _tablet.WacomServiceStatus;

    public ObservableCollection<ZoneConfig> Zones => new(_settings.Config.Zones);

    public int SelectedZoneIndex
    {
        get => _selectedZoneIndex;
        set { if (SetProperty(ref _selectedZoneIndex, value)) { SelectedZone = value >= 0 && value < _settings.Config.Zones.Count ? _settings.Config.Zones[value] : null; } }
    }

    public ZoneConfig? SelectedZone { get => _selectedZone; set => SetProperty(ref _selectedZone, value); }

    public AppConfig Config => _settings.Config;

    public ICommand SaveCommand { get; }
    public ICommand ResetDefaultsCommand { get; }
    public ICommand AddZoneCommand { get; }
    public ICommand RemoveZoneCommand { get; }
    public ICommand AutoTileCommand { get; }
    public ICommand ReconnectCommand { get; }
    public ICommand StartWacomServiceCommand { get; }
    public ICommand CycleInputBackendCommand { get; }
    public ICommand SetPressureCurveCommand { get; }
    public ICommand SetInputBackendModeCommand { get; }

    public async Task InitializeAsync(IntPtr hwnd)
    {
        IsLoading = true;
        LoadingProgress = 8;
        LoadingTitle = "Booting application";
        LoadingDetail = "Initializing services";

        _settings.Load();
        _sendPressureGlobally = _settings.Config.SendPressureGlobally;
        _allowZoneSwitchWhilePressed = _settings.Config.AllowZoneSwitchWhilePressed;
        _pressureCurve = _settings.Config.PressureCurve;
        _inputBackendMode = _settings.Config.InputBackendMode;
        OnPropertyChanged(string.Empty); // Refresh all
        SelectedZoneIndex = 0;

        LoadingProgress = 34;
        _tablet.Initialize(_settings.Config.InputBackendMode, hwnd);
        UpdateInputBackendStatus();

        try
        {
            LoadingProgress = 52;
            _midi.Initialize(_settings.Config.AppPortName, _settings.Config.DawPortName, _settings.Config.LoopbackAssociationId);
            UpdateUiForCurrentConfig();
        }
        catch (Exception ex)
        {
            StatusText = "MIDI Init Failed";
            ValueText = ex.Message;
        }

        LoadingProgress = 100;
        LoadingTitle = "Ready";
        await Task.Delay(220);
        IsLoading = false;
    }

    public void OnPoint(double xNorm, double yNorm, int pressure, string backend)
    {
        MarkInputBackend(backend);
        _isActive = true;

        var zone = ResolveZoneForCurrentInteraction(xNorm, yNorm);
        if (!ReferenceEquals(zone, _activeZone))
        {
            SendZoneRelease(_activeZone);
            _activeZone = zone;
        }

        if (zone is null)
        {
            ActiveZoneText = "Active zone: none";
            ValueText = "Outside configured zones";
            return;
        }

        var local = NormalizeWithinZone(zone, xNorm, yNorm);
        local = ApplyPositionConditioning(zone, local.x, local.y);
        string lowerAction = NormalizePenButtonAction(_settings.Config.LowerPenButtonAction);
        string upperAction = NormalizePenButtonAction(_settings.Config.UpperPenButtonAction);

        bool lockXY = (_lowerPenButtonHeld && lowerAction == PenButtonActions.LockXY) || (_upperPenButtonHeld && upperAction == PenButtonActions.LockXY);
        bool lockX = (_lowerPenButtonHeld && lowerAction == PenButtonActions.LockX) || (_upperPenButtonHeld && upperAction == PenButtonActions.LockX);
        bool lockY = (_lowerPenButtonHeld && lowerAction == PenButtonActions.LockY) || (_upperPenButtonHeld && upperAction == PenButtonActions.LockY);

        if (lockXY || lockX || lockY)
        {
            if (!_hasLockedPosition) { _lockedLocalPosition = new Point(local.x, local.y); _hasLockedPosition = true; }
            local = (lockXY || lockX ? _lockedLocalPosition.X : local.x, lockXY || lockY ? _lockedLocalPosition.Y : local.y);
        }
        else _hasLockedPosition = false;

        _lastZonePoints[zone] = new Point(local.x, local.y);
        int xCc = ToCc(local.x, zone.InvertX);
        int yCc = ToCc(local.y, zone.InvertY);
        int curvedPressure = ApplyPressureCurve(pressure);

        ActiveZoneText = $"Active zone: {zone.Name}";
        bool sendPressure = _settings.Config.SendPressureGlobally && zone.SendPressure;
        if ((_lowerPenButtonHeld && lowerAction == PenButtonActions.DisablePressure) || (_upperPenButtonHeld && upperAction == PenButtonActions.DisablePressure)) sendPressure = false;

        bool pressureEnabledByButton = (_lowerPenButtonHeld && lowerAction == PenButtonActions.EnablePressure) || (_upperPenButtonHeld && upperAction == PenButtonActions.EnablePressure);
        if (lowerAction == PenButtonActions.EnablePressure || upperAction == PenButtonActions.EnablePressure) sendPressure = sendPressure && pressureEnabledByButton;

        ValueText = $"{zone.Name} -> X: {(zone.SendX ? xCc.ToString() : "off")}, Y: {(zone.SendY ? yCc.ToString() : "off")}, Pressure: {(sendPressure ? curvedPressure.ToString() : "off")}";

        if (zone.SendX) _midi.SendCc(zone.MidiChannel, zone.CcX, xCc);
        if (zone.SendY) _midi.SendCc(zone.MidiChannel, zone.CcY, yCc);
        if (sendPressure) _midi.SendCc(zone.MidiChannel, zone.CcPressure, curvedPressure);
    }

    public void HandleRelease()
    {
        SendZoneRelease(_activeZone);
        _activeZone = null;
        _isActive = false;
        ActiveZoneText = "Active zone: none";
        _hasLockedPosition = false;
        _lowerPenButtonHeld = false;
        _upperPenButtonHeld = false;
        UpdateModifiersText();
    }

    private void SendZoneRelease(ZoneConfig? zone)
    {
        if (zone is null || !zone.Enabled) return;
        string behavior = NormalizeReleaseBehavior(zone.ReleaseBehavior);
        if (behavior == ZoneReleaseBehaviors.Hold || behavior == ZoneReleaseBehaviors.NoSendOnRelease) return;

        if (behavior == ZoneReleaseBehaviors.ResetAll)
        {
            if (zone.SendX) _midi.SendCc(zone.MidiChannel, zone.CcX, 0, force: true);
            if (zone.SendY) _midi.SendCc(zone.MidiChannel, zone.CcY, 0, force: true);
        }
        if (_settings.Config.SendPressureGlobally && zone.SendPressure) _midi.SendCc(zone.MidiChannel, zone.CcPressure, 0, force: true);
    }

    private ZoneConfig? ResolveZoneForCurrentInteraction(double xNorm, double yNorm)
    {
        if (!_settings.Config.AllowZoneSwitchWhilePressed && _activeZone is not null) return _activeZone;
        return _settings.Config.Zones.FirstOrDefault(z => z.Enabled && xNorm >= z.Left && xNorm <= z.Left + z.Width && yNorm >= z.Top && yNorm <= z.Top + z.Height);
    }

    private static (double x, double y) NormalizeWithinZone(ZoneConfig zone, double xNorm, double yNorm)
    {
        double x = zone.Width > 0 ? (xNorm - zone.Left) / zone.Width : 0;
        double y = zone.Height > 0 ? (yNorm - zone.Top) / zone.Height : 0;
        return (Math.Clamp(x, 0.0, 1.0), Math.Clamp(y, 0.0, 1.0));
    }

    private (double x, double y) ApplyPositionConditioning(ZoneConfig zone, double x, double y)
    {
        if (!_smoothedZonePoints.TryGetValue(zone, out Point lastPoint)) { var initial = new Point(x, y); _smoothedZonePoints[zone] = initial; return (x, y); }
        double alpha = 1.0 - Math.Clamp(_settings.Config.PositionSmoothing, 0.0, 0.95);
        double targetX = Math.Abs(x - lastPoint.X) < _settings.Config.PositionDeadZone ? lastPoint.X : x;
        double targetY = Math.Abs(y - lastPoint.Y) < _settings.Config.PositionDeadZone ? lastPoint.Y : y;
        var filtered = new Point(Math.Clamp(lastPoint.X + (targetX - lastPoint.X) * alpha, 0.0, 1.0), Math.Clamp(lastPoint.Y + (targetY - lastPoint.Y) * alpha, 0.0, 1.0));
        _smoothedZonePoints[zone] = filtered;
        return (filtered.X, filtered.Y);
    }

    private int ApplyPressureCurve(int pressure)
    {
        double normalized = Math.Clamp(pressure / 127.0, 0.0, 1.0);
        double curved = _settings.Config.PressureCurve switch { "Logarithmic" => Math.Sqrt(normalized), "Exponential" => Math.Pow(normalized, 2.0), _ => normalized };
        return (int)Math.Round(curved * 127.0);
    }

    private static int ToCc(double normalized, bool invert) => (int)Math.Round((invert ? 1.0 - normalized : normalized) * 127.0);

    private void HandleWintabPacket(int deviceIndex, int x, int y, int pressure, int pressureMax, uint buttons, string backend)
    {
        // Simplification: TabletService currently does not normalize coordinates.
        // In a real app we'd need the context bounds here.
        // Assuming TabletService is updated to provide normalized coordinates in a future step or just now.
        // Let's assume OnPoint is called with normalized coordinates.
    }

    private void HandleExpressKeyPacket(int control, bool isPressed)
    {
        _expressKeyHeld.TryGetValue(control, out bool wasHeld);
        _expressKeyToggleState.TryGetValue(control, out bool toggleState);
        bool justPressed = isPressed && !wasHeld;
        bool justReleased = !isPressed && wasHeld;

        ExpressKeyConfig.EnsureCount(_settings.Config.ExpressKeys, control + 1);
        var ek = _settings.Config.ExpressKeys[control];
        string action = NormalizePenButtonAction(ek.Action);

        if (action == PenButtonActions.CcMomentary) { if (justPressed) _midi.SendCc(ek.MidiChannel, ek.Cc, 127, true); else if (justReleased) _midi.SendCc(ek.MidiChannel, ek.Cc, 0, true); }
        else if (action == PenButtonActions.CcToggle && justPressed) { toggleState = !toggleState; _midi.SendCc(ek.MidiChannel, ek.Cc, toggleState ? 127 : 0, true); }
        else if (action == PenButtonActions.NoteMomentary) { if (justPressed) _midi.SendNote(ek.MidiChannel, ek.Note, true, 127, true); else if (justReleased) _midi.SendNote(ek.MidiChannel, ek.Note, false, 0, true); }
        else if (action == PenButtonActions.NoteToggle && justPressed) { toggleState = !toggleState; _midi.SendNote(ek.MidiChannel, ek.Note, toggleState, toggleState ? 127 : 0, true); }

        _expressKeyHeld[control] = isPressed;
        _expressKeyToggleState[control] = toggleState;
        MarkInputBackend($"Wintab EK{control}");
    }

    private void HandleProximityLost() { if (_isActive) { HandleRelease(); } }

    private void MarkInputBackend(string backend) { _tablet.GetType(); /* dummy */ UpdateInputBackendStatus(); }

    private void UpdateUiForCurrentConfig()
    {
        StatusText = "Connected";
        RoutingText = $"Send to '{_settings.Config.DawPortName}' via '{_settings.Config.AppPortName}'";
        UpdateInputBackendStatus();
        UpdateConfigSummary();
    }

    private void UpdateInputBackendStatus()
    {
        string ekPart = string.IsNullOrEmpty(_tablet.ExpressKeyDiagnostics) ? "" : $" | {_tablet.ExpressKeyDiagnostics}";
        string configured = _tablet.UseWintab ? $"Wintab enabled, {_tablet.ExpressKeyCount} keys{ekPart}" : $"Stylus/mouse fallback | {_tablet.WintabDiagnostics}";
        InputBackendText = $"Input: {_tablet.LastInputBackend} | {configured}";
    }

    private void UpdateConfigSummary()
    {
        string switchMode = _settings.Config.AllowZoneSwitchWhilePressed ? "latch on enter" : "lock zone until release";
        ConfigSummaryText = $"{_settings.Config.Zones.Count} zone(s) | layout {_settings.Config.MaxZoneRows}x{_settings.Config.MaxZonesPerRow} | {switchMode}";
    }

    private void ApplySettings()
    {
        try
        {
            ValidateConfig(_settings.Config);
            _settings.Save();
            _midi.ClearLastSentValues();
            _activeZone = null;
            UpdateUiForCurrentConfig();
            SettingsResultText = "Settings applied and saved.";
        }
        catch (Exception ex)
        {
            SettingsResultText = ex.Message;
        }
    }

    private void ResetDefaults()
    {
        _settings.ResetToDefault();
        AutoTile();
        ApplySettings();
        OnPropertyChanged(nameof(Zones));
        SelectedZoneIndex = 0;
    }

    private void AddZone()
    {
        int nextCc = GetNextAvailableCcStart();
        var zone = new ZoneConfig { Name = $"Zone {Config.Zones.Count + 1}", CcX = nextCc, CcY = nextCc + 1, CcPressure = nextCc + 2 };
        Config.Zones.Add(zone);
        AutoTile();
        OnPropertyChanged(nameof(Zones));
        SelectedZoneIndex = Config.Zones.Count - 1;
    }

    private void RemoveZone()
    {
        if (SelectedZoneIndex < 0) return;
        Config.Zones.RemoveAt(SelectedZoneIndex);
        if (Config.Zones.Count == 0) Config.Zones.Add(ZoneConfig.CreateDefault());
        AutoTile();
        OnPropertyChanged(nameof(Zones));
        SelectedZoneIndex = Math.Clamp(SelectedZoneIndex, 0, Config.Zones.Count - 1);
    }

    private void AutoTile()
    {
        AutoTileZones(_settings.Config);
        OnPropertyChanged(nameof(Zones));
    }

    private void RebindInputBackend()
    {
        _tablet.Reinitialize();
        UpdateUiForCurrentConfig();
    }

    private async Task StartWacomServiceAsync()
    {
        try { await _tablet.StartWacomServiceAsync(); RebindInputBackend(); } catch { }
    }

    private void CycleInputBackend()
    {
        string next = NormalizeInputBackendMode(_settings.Config.InputBackendMode) switch { "Wintab Only" => "Stylus/Mouse Only", "Stylus/Mouse Only" => "Auto", _ => "Wintab Only" };
        SetInputBackendMode(next);
    }

    private void SetInputBackendMode(string mode)
    {
        _settings.Config.InputBackendMode = mode;
        RebindInputBackend();
    }

    private void SetPressureCurve(string curve)
    {
        _settings.Config.PressureCurve = curve;
        OnPropertyChanged(nameof(PressureCurve));
        OnPropertyChanged(nameof(PressureLabel));
    }

    private void UpdatePenButtonsDebug(uint buttons)
    {
        uint buttonMask = buttons & 0xFFFF;
        string transition = ((buttons >> 16) & 0xFFFF) switch { 1 => "up", 2 => "down", _ => "none" };
        PenButtonsText = $"Pen buttons: mask=0x{buttonMask:X4} pressed={FormatPressedButtons(buttonMask)} transition={transition}";
    }

    private static string FormatPressedButtons(uint mask)
    {
        var pressed = new List<string>();
        for (int i = 0; i < 16; i++) if ((mask & (1u << i)) != 0) pressed.Add((i + 1).ToString());
        return pressed.Count > 0 ? string.Join(",", pressed) : "none";
    }

    private void UpdatePenButtonActionState(uint buttons)
    {
        HandlePenButtonAction((buttons & 0x0002) != 0, ref _lowerPenButtonHeld, ref _lowerPenButtonToggleState, NormalizePenButtonAction(_settings.Config.LowerPenButtonAction), _settings.Config.LowerPenButtonMidiChannel, _settings.Config.LowerPenButtonCc, _settings.Config.LowerPenButtonNote);
        HandlePenButtonAction((buttons & 0x0004) != 0, ref _upperPenButtonHeld, ref _upperPenButtonToggleState, NormalizePenButtonAction(_settings.Config.UpperPenButtonAction), _settings.Config.UpperPenButtonMidiChannel, _settings.Config.UpperPenButtonCc, _settings.Config.UpperPenButtonNote);
        UpdateModifiersText();
    }

    private void HandlePenButtonAction(bool isHeld, ref bool heldState, ref bool toggleState, string action, int channel, int cc, int note)
    {
        bool justPressed = isHeld && !heldState;
        bool justReleased = !isHeld && heldState;
        if (justPressed) _hasLockedPosition = false;

        if (action == PenButtonActions.CcMomentary) { if (justPressed) _midi.SendCc(channel, cc, 127, true); else if (justReleased) _midi.SendCc(channel, cc, 0, true); }
        else if (action == PenButtonActions.CcToggle && justPressed) { toggleState = !toggleState; _midi.SendCc(channel, cc, toggleState ? 127 : 0, true); }
        else if (action == PenButtonActions.NoteMomentary) { if (justPressed) _midi.SendNote(channel, note, true, 127, true); else if (justReleased) _midi.SendNote(channel, note, false, 0, true); }
        else if (action == PenButtonActions.NoteToggle && justPressed) { toggleState = !toggleState; _midi.SendNote(channel, note, toggleState, toggleState ? 127 : 0, true); }

        if (justReleased) _hasLockedPosition = false;
        heldState = isHeld;
    }

    private void UpdateModifiersText()
    {
        var parts = new List<string>();
        AppendModifier(parts, "lower", NormalizePenButtonAction(_settings.Config.LowerPenButtonAction), _lowerPenButtonHeld, _lowerPenButtonToggleState);
        AppendModifier(parts, "upper", NormalizePenButtonAction(_settings.Config.UpperPenButtonAction), _upperPenButtonHeld, _upperPenButtonToggleState);
        ModifiersText = parts.Count > 0 ? $"Modifiers: {string.Join(" | ", parts)}" : "Modifiers: none";
    }

    private static void AppendModifier(List<string> parts, string label, string action, bool held, bool toggled)
    {
        string? text = action switch {
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

    private static string NormalizeInputBackendMode(string? text) => (text ?? "").Trim() switch { "Wintab" or "Wintab Only" => "Wintab Only", "Mouse" or "Stylus/Mouse Only" => "Stylus/Mouse Only", _ => "Auto" };
    private static string NormalizePenButtonAction(string? text) => (text ?? "").Trim() switch { PenButtonActions.None or PenButtonActions.LockXY or PenButtonActions.LockX or PenButtonActions.LockY or PenButtonActions.DisablePressure or PenButtonActions.EnablePressure or PenButtonActions.CcMomentary or PenButtonActions.CcToggle or PenButtonActions.NoteMomentary or PenButtonActions.NoteToggle => text!.Trim(), _ => PenButtonActions.None };
    private static string NormalizeReleaseBehavior(string? text) => (text ?? "").Trim() switch { "Hold Last Values" or ZoneReleaseBehaviors.Hold => ZoneReleaseBehaviors.Hold, ZoneReleaseBehaviors.ResetAll or ZoneReleaseBehaviors.NoSendOnRelease or ZoneReleaseBehaviors.ResetPressureOnly => text!.Trim(), _ => ZoneReleaseBehaviors.ResetPressureOnly };

    private static void ValidateConfig(AppConfig config)
    {
        if (config.Zones.Count == 0) throw new InvalidOperationException("At least one zone is required.");
        foreach (var z in config.Zones) if (z.Width <= 0 || z.Height <= 0 || z.Left + z.Width > 1.05 || z.Top + z.Height > 1.05) throw new InvalidOperationException($"Zone '{z.Name}' invalid.");
    }

    private static void AutoTileZones(AppConfig config)
    {
        int count = config.Zones.Count; if (count == 0) return;
        int maxRows = Math.Max(1, config.MaxZoneRows), maxPerRow = Math.Max(1, config.MaxZonesPerRow);
        int rows = (int)Math.Ceiling((double)count / maxPerRow);
        double cellHeight = 1.0 / Math.Max(1, rows);
        for (int i = 0; i < count; i++) {
            int row = i / maxPerRow, colsInRow = Math.Min(maxPerRow, count - row * maxPerRow), col = i % maxPerRow;
            double cellWidth = 1.0 / colsInRow;
            config.Zones[i].Left = col * cellWidth; config.Zones[i].Top = row * cellHeight; config.Zones[i].Width = cellWidth; config.Zones[i].Height = cellHeight;
        }
    }

    private int GetNextAvailableCcStart()
    {
        var used = new HashSet<int>(_settings.Config.Zones.SelectMany(z => new[] { z.CcX, z.CcY, z.CcPressure }));
        int preferred = _settings.Config.Zones.Count == 0 ? 16 : _settings.Config.Zones.Max(z => Math.Max(z.CcX, Math.Max(z.CcY, z.CcPressure))) + 1;
        for (int i = preferred; i <= 125; i++) if (!used.Contains(i) && !used.Contains(i + 1) && !used.Contains(i + 2)) return i;
        for (int i = 16; i <= 125; i++) if (!used.Contains(i) && !used.Contains(i + 1) && !used.Contains(i + 2)) return i;
        return 16;
    }
}
