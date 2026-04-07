using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IMidiService _midiService;
    private readonly IInputMappingService _inputMappingService;
    private readonly IWacomService _wacomService;

    private AppConfig _config = AppConfig.Default;

    public event EventHandler? OpenPreferencesRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler? ReconnectInputRequested;
    public event EventHandler? ShowAboutRequested;
    public event EventHandler? ZoneVisualStateChanged;
    public event EventHandler? InputBackendChanged;

    [ObservableProperty]
    private string _statusText = "Connected";

    [ObservableProperty]
    private string _routingText = string.Empty;

    [ObservableProperty]
    private string _inputBackendText = string.Empty;

    [ObservableProperty]
    private string _activeZoneText = "Active zone: none";

    [ObservableProperty]
    private string _valueText = "X: -, Y: -, Pressure: -";

    [ObservableProperty]
    private string _penButtonsText = "Pen buttons: -";

    [ObservableProperty]
    private string _modifiersText = "Modifiers: none";

    [ObservableProperty]
    private string _configSummaryText = string.Empty;

    [ObservableProperty]
    private string _settingsResultText = string.Empty;

    [ObservableProperty]
    private Brush _settingsResultBrush = Brushes.DarkSeaGreen;

    [ObservableProperty]
    private string _wacomServiceStatusText = "Service: checking...";

    [ObservableProperty]
    private bool _canStartWacomService;

    [ObservableProperty]
    private string _toolbarPressureLabelText = "Pressure (Lin)";

    [ObservableProperty]
    private string _toolbarInputBackendLabelText = "Input (Auto)";

    [ObservableProperty]
    private ObservableCollection<string> _zoneItems = new();

    [ObservableProperty]
    private int _selectedZoneIndex = -1;

    [ObservableProperty]
    private string _zoneName = string.Empty;

    [ObservableProperty]
    private bool _zoneEnabled;

    [ObservableProperty]
    private int _zoneMidiChannel = 1;

    [ObservableProperty]
    private int _zoneCcX = 16;

    [ObservableProperty]
    private int _zoneCcY = 17;

    [ObservableProperty]
    private int _zoneCcPressure = 18;

    [ObservableProperty]
    private double _zoneLeft;

    [ObservableProperty]
    private double _zoneTop;

    [ObservableProperty]
    private double _zoneWidth = 1;

    [ObservableProperty]
    private double _zoneHeight = 1;

    [ObservableProperty]
    private bool _zoneSendX = true;

    [ObservableProperty]
    private bool _zoneSendY = true;

    [ObservableProperty]
    private bool _zoneInvertX;

    [ObservableProperty]
    private bool _zoneInvertY = true;

    [ObservableProperty]
    private bool _zoneSendPressure = true;

    [ObservableProperty]
    private string _zoneReleaseBehavior = ZoneReleaseBehaviors.NoSendOnRelease;

    public static IReadOnlyList<int> AvailableChannels { get; } = new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

    public static IReadOnlyList<string> ReleaseBehaviors { get; } =
    [
        ZoneReleaseBehaviors.NoSendOnRelease,
        ZoneReleaseBehaviors.Hold,
        ZoneReleaseBehaviors.ResetPressureOnly,
        ZoneReleaseBehaviors.ResetAll
    ];

    public AppConfig Config
    {
        get => _config;
        private set
        {
            if (SetProperty(ref _config, value))
            {
                OnPropertyChanged(nameof(SendPressureGlobally));
                OnPropertyChanged(nameof(AllowZoneSwitchWhilePressed));
                RefreshZoneItems();
                RefreshConfigSummaryFromConfig();
                SyncToolbarLabels();
            }
        }
    }

    public bool SendPressureGlobally
    {
        get => Config.SendPressureGlobally;
        set
        {
            if (Config.SendPressureGlobally == value)
            {
                return;
            }

            Config.SendPressureGlobally = value;
            OnPropertyChanged();
        }
    }

    public bool AllowZoneSwitchWhilePressed
    {
        get => Config.AllowZoneSwitchWhilePressed;
        set
        {
            if (Config.AllowZoneSwitchWhilePressed == value)
            {
                return;
            }

            Config.AllowZoneSwitchWhilePressed = value;
            OnPropertyChanged();
            RefreshConfigSummaryFromConfig();
        }
    }

    public MainViewModel(
        ISettingsService settingsService,
        IMidiService midiService,
        IInputMappingService inputMappingService,
        IWacomService wacomService)
    {
        _settingsService = settingsService;
        _midiService = midiService;
        _inputMappingService = inputMappingService;
        _wacomService = wacomService;
    }

    [RelayCommand]
    private void SaveSettings()
    {
        _settingsService.Save(Config);
        SettingsResultBrush = Brushes.DarkSeaGreen;
        SettingsResultText = "Settings applied and saved.";
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        Config = AppConfig.Default;
        _settingsService.AutoTileZones(Config);
        SelectedZoneIndex = Config.Zones.Count > 0 ? 0 : -1;
        LoadSelectedZone();
        ZoneVisualStateChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenPreferences() => OpenPreferencesRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Exit() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void RefreshWacomStatus()
    {
        (string header, bool canStart) = _wacomService.GetStatus();
        WacomServiceStatusText = header;
        CanStartWacomService = canStart;
    }

    [RelayCommand]
    private void ReconnectInput() => ReconnectInputRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void StartWacomService()
    {
        _wacomService.Start();
        RefreshWacomStatus();
    }

    [RelayCommand]
    private void ShowAbout() => ShowAboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void AutoLayout()
    {
        _settingsService.AutoTileZones(Config);
        RefreshZoneItems();
        LoadSelectedZone();
        ZoneVisualStateChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void AddZone()
    {
        var zone = ZoneConfig.CreateDefault();
        zone.Name = $"Zone {Config.Zones.Count + 1}";
        zone.CcX = GetNextAvailableCcStart();
        zone.CcY = Math.Clamp(zone.CcX + 1, 0, 127);
        zone.CcPressure = Math.Clamp(zone.CcX + 2, 0, 127);
        Config.Zones.Add(zone);
        _settingsService.AutoTileZones(Config);
        RefreshZoneItems();
        SelectedZoneIndex = Config.Zones.Count - 1;
        LoadSelectedZone();
        ZoneVisualStateChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void RemoveZone()
    {
        if (Config.Zones.Count <= 1 || SelectedZoneIndex < 0 || SelectedZoneIndex >= Config.Zones.Count)
        {
            return;
        }

        Config.Zones.RemoveAt(SelectedZoneIndex);
        _settingsService.AutoTileZones(Config);
        RefreshZoneItems();
        SelectedZoneIndex = Math.Clamp(SelectedZoneIndex, 0, Config.Zones.Count - 1);
        LoadSelectedZone();
        ZoneVisualStateChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SetPressureCurve(string curve)
    {
        Config.PressureCurve = curve;
        SyncToolbarLabels();
    }

    [RelayCommand]
    private void SetInputBackendMode(string mode)
    {
        Config.InputBackendMode = _inputMappingService.NormalizeInputBackendMode(mode);
        SyncToolbarLabels();
        InputBackendChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Load()
    {
        Config = _settingsService.Load();
        SelectedZoneIndex = Config.Zones.Count > 0 ? 0 : -1;
        LoadSelectedZone();
    }

    public void Save() => _settingsService.Save(Config);

    public void ResetToDefaults() => Config = AppConfig.Default;

    public void AutoTileZones() => _settingsService.AutoTileZones(Config);

    public async Task InitializeAsync()
    {
        await Task.Yield();
        RefreshWacomStatus();
    }

    public void ShutdownMidi() => _midiService.Shutdown();

    public int GetNextAvailableCcStart()
    {
        var used = new HashSet<int>();
        foreach (ZoneConfig zone in Config.Zones)
        {
            used.Add(zone.CcX);
            used.Add(zone.CcY);
            used.Add(zone.CcPressure);
        }

        for (int cc = 0; cc <= 125; cc++)
        {
            if (!used.Contains(cc) && !used.Contains(cc + 1) && !used.Contains(cc + 2))
            {
                return cc;
            }
        }

        return 16;
    }

    partial void OnSelectedZoneIndexChanged(int value) => LoadSelectedZone();

    private void LoadSelectedZone()
    {
        if (SelectedZoneIndex < 0 || SelectedZoneIndex >= Config.Zones.Count)
        {
            return;
        }

        ZoneConfig zone = Config.Zones[SelectedZoneIndex];
        ZoneName = zone.Name;
        ZoneEnabled = zone.Enabled;
        ZoneMidiChannel = zone.MidiChannel;
        ZoneCcX = zone.CcX;
        ZoneCcY = zone.CcY;
        ZoneCcPressure = zone.CcPressure;
        ZoneLeft = zone.Left;
        ZoneTop = zone.Top;
        ZoneWidth = zone.Width;
        ZoneHeight = zone.Height;
        ZoneSendX = zone.SendX;
        ZoneSendY = zone.SendY;
        ZoneInvertX = zone.InvertX;
        ZoneInvertY = zone.InvertY;
        ZoneSendPressure = zone.SendPressure;
        ZoneReleaseBehavior = _inputMappingService.NormalizeReleaseBehavior(zone.ReleaseBehavior);
    }

    private void RefreshZoneItems()
    {
        ZoneItems.Clear();
        for (int i = 0; i < Config.Zones.Count; i++)
        {
            ZoneConfig zone = Config.Zones[i];
            string enabled = zone.Enabled ? "on" : "off";
            ZoneItems.Add($"{i + 1}. {zone.Name} [{enabled}]");
        }
    }

    private void SyncToolbarLabels()
    {
        string curveShort = Config.PressureCurve switch
        {
            "Logarithmic" => "Log",
            "Exponential" => "Exp",
            _ => "Lin"
        };

        ToolbarPressureLabelText = $"Pressure ({curveShort})";

        ToolbarInputBackendLabelText = _inputMappingService.NormalizeInputBackendMode(Config.InputBackendMode) switch
        {
            "Wintab Only" => "Input (Wintab)",
            "Stylus/Mouse Only" => "Input (Mouse)",
            _ => "Input (Auto)"
        };
    }

    private void RefreshConfigSummaryFromConfig()
    {
        string switchMode = Config.AllowZoneSwitchWhilePressed ? "latch on enter" : "lock zone until release";
        ConfigSummaryText = $"{Config.Zones.Count} zone(s) configured | layout {Config.MaxZoneRows} row(s) max / {Config.MaxZonesPerRow} per row | {switchMode}";
    }
}
