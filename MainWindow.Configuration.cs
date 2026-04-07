using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace TabletMidiPad;

public partial class MainWindow
{
    private void ApplySettings()
    {
        try
        {
            CommitCurrentZoneEdits();
            _viewModel.Save();

            _activeZone = null;
            UpdateUiForCurrentConfig();
            DrawZoneOverlay();

            _viewModel.SettingsResultBrush = Brushes.DarkSeaGreen;
            _viewModel.SettingsResultText = "Settings applied and saved.";
        }
        catch (Exception ex)
        {
            _viewModel.SettingsResultBrush = Brushes.IndianRed;
            _viewModel.SettingsResultText = ex.Message;
        }
    }

    private void RestartMidi()
    {
        _midiService.Restart(_config);
        _activeZone = null;
        UpdateUiForCurrentConfig();
        DrawZoneOverlay();
    }

    private void LoadConfigIntoUi(AppConfig config)
    {
        _isLoadingUi = true;

        MaxZoneRowsTextBox.Text = config.MaxZoneRows.ToString();
        MaxZonesPerRowTextBox.Text = config.MaxZonesPerRow.ToString();
        RefreshZoneList();
        ZoneListBox.SelectedIndex = config.Zones.Count > 0 ? 0 : -1;
        _selectedZoneIndex = ZoneListBox.SelectedIndex;
        LoadSelectedZoneIntoEditor();
        SyncToolbar();

        _isLoadingUi = false;
        DrawZoneOverlay();
    }

    private void SyncToolbar()
    {
        SyncInputBackendSplitBorder();

        string curveShort = _config.PressureCurve switch
        {
            "Logarithmic" => "Log",
            "Exponential" => "Exp",
            _ => "Lin"
        };
        _viewModel.ToolbarPressureLabelText = $"Pressure ({curveShort})";

        foreach (var item in PressureCurveMenu.Items)
        {
            if (item is MenuItem mi)
            {
                bool active = string.Equals(mi.Header as string, _config.PressureCurve, StringComparison.Ordinal);
                mi.Icon = active ? new TextBlock { Text = "\u2713", Foreground = Brushes.White, FontSize = 11 } : null;
            }
        }

        string backendLabel = _inputMappingService.NormalizeInputBackendMode(_config.InputBackendMode) switch
        {
            "Wintab Only" => "Input (Wintab)",
            "Stylus/Mouse Only" => "Input (Mouse)",
            _ => "Input (Auto)"
        };

        _viewModel.ToolbarInputBackendLabelText = backendLabel;

        foreach (var item in InputBackendMenu.Items)
        {
            if (item is MenuItem mi)
            {
                bool active = (mi.Header as string) switch
                {
                    "Wintab" => string.Equals(_config.InputBackendMode, "Wintab Only", StringComparison.Ordinal),
                    "Mouse" => string.Equals(_config.InputBackendMode, "Stylus/Mouse Only", StringComparison.Ordinal),
                    _ => string.Equals(_inputMappingService.NormalizeInputBackendMode(_config.InputBackendMode), "Auto", StringComparison.Ordinal)
                };

                mi.Icon = active ? new TextBlock { Text = "\u2713", Foreground = Brushes.White, FontSize = 11 } : null;
            }
        }
    }

    private void RefreshZoneList()
    {
        ZoneListBox.Items.Clear();

        for (int i = 0; i < _config.Zones.Count; i++)
        {
            ZoneConfig zone = _config.Zones[i];
            string enabled = zone.Enabled ? "on" : "off";
            ZoneListBox.Items.Add($"{i + 1}. {zone.Name} [{enabled}]");
        }
    }

    private void LoadSelectedZoneIntoEditor()
    {
        _isLoadingUi = true;

        if (_selectedZoneIndex < 0 || _selectedZoneIndex >= _config.Zones.Count)
        {
            ZoneNameTextBox.Text = string.Empty;
            ZoneEnabledCheckBox.IsChecked = false;
            ZoneChannelComboBox.SelectedIndex = -1;
            SetZoneSelectionSummary(null);
            ZoneDetailsExpander.IsEnabled = false;
            ZoneCcXTextBox.Text = string.Empty;
            ZoneCcYTextBox.Text = string.Empty;
            ZoneCcPressureTextBox.Text = string.Empty;
            ZoneLeftTextBox.Text = string.Empty;
            ZoneTopTextBox.Text = string.Empty;
            ZoneWidthTextBox.Text = string.Empty;
            ZoneHeightTextBox.Text = string.Empty;
            ZoneSendXCheckBox.IsChecked = false;
            ZoneSendYCheckBox.IsChecked = false;
            ZoneInvertXCheckBox.IsChecked = false;
            ZoneInvertYCheckBox.IsChecked = false;
            ZoneSendPressureCheckBox.IsChecked = false;
            ZoneReleaseBehaviorComboBox.SelectedIndex = -1;
            ZoneReleaseBehaviorComboBox.Text = string.Empty;
            _isLoadingUi = false;
            return;
        }

        ZoneConfig zone = _config.Zones[_selectedZoneIndex];
        ZoneDetailsExpander.IsEnabled = true;
        ZoneNameTextBox.Text = zone.Name;
        ZoneEnabledCheckBox.IsChecked = zone.Enabled;
        ZoneChannelComboBox.SelectedItem = zone.MidiChannel;
        ZoneChannelComboBox.Text = zone.MidiChannel.ToString();
        SetZoneSelectionSummary(zone);
        ZoneCcXTextBox.Text = zone.CcX.ToString();
        ZoneCcYTextBox.Text = zone.CcY.ToString();
        ZoneCcPressureTextBox.Text = zone.CcPressure.ToString();
        ZoneLeftTextBox.Text = zone.Left.ToString("0.###");
        ZoneTopTextBox.Text = zone.Top.ToString("0.###");
        ZoneWidthTextBox.Text = zone.Width.ToString("0.###");
        ZoneHeightTextBox.Text = zone.Height.ToString("0.###");
        ZoneSendXCheckBox.IsChecked = zone.SendX;
        ZoneSendYCheckBox.IsChecked = zone.SendY;
        ZoneInvertXCheckBox.IsChecked = zone.InvertX;
        ZoneInvertYCheckBox.IsChecked = zone.InvertY;
        ZoneSendPressureCheckBox.IsChecked = zone.SendPressure;
        ZoneReleaseBehaviorComboBox.SelectedItem = _inputMappingService.NormalizeReleaseBehavior(zone.ReleaseBehavior);
        ZoneReleaseBehaviorComboBox.Text = _inputMappingService.NormalizeReleaseBehavior(zone.ReleaseBehavior);

        _isLoadingUi = false;
    }

    private void CommitCurrentZoneEdits()
    {
        if (_selectedZoneIndex < 0 || _selectedZoneIndex >= _config.Zones.Count)
        {
            return;
        }

        ZoneConfig zone = _config.Zones[_selectedZoneIndex];
        _config.MaxZoneRows = ReadInt(MaxZoneRowsTextBox.Text, 1, 16, "Max Zone Rows");
        _config.MaxZonesPerRow = ReadInt(MaxZonesPerRowTextBox.Text, 1, 32, "Max Zones Per Row");
        zone.Name = RequireText(ZoneNameTextBox.Text, "Zone Name");
        zone.Enabled = ZoneEnabledCheckBox.IsChecked == true;
        zone.MidiChannel = ReadInt(ZoneChannelComboBox.Text, 1, 16, "Zone MIDI Channel");
        zone.CcX = ReadInt(ZoneCcXTextBox.Text, 0, 127, "Zone CC X");
        zone.CcY = ReadInt(ZoneCcYTextBox.Text, 0, 127, "Zone CC Y");
        zone.CcPressure = ReadInt(ZoneCcPressureTextBox.Text, 0, 127, "Zone Pressure CC");
        zone.Left = ReadDouble(ZoneLeftTextBox.Text, 0.0, 1.0, "Zone Left");
        zone.Top = ReadDouble(ZoneTopTextBox.Text, 0.0, 1.0, "Zone Top");
        zone.Width = ReadDouble(ZoneWidthTextBox.Text, 0.0, 1.0, "Zone Width");
        zone.Height = ReadDouble(ZoneHeightTextBox.Text, 0.0, 1.0, "Zone Height");
        zone.SendX = ZoneSendXCheckBox.IsChecked != false;
        zone.SendY = ZoneSendYCheckBox.IsChecked != false;
        zone.InvertX = ZoneInvertXCheckBox.IsChecked == true;
        zone.InvertY = ZoneInvertYCheckBox.IsChecked == true;
        zone.SendPressure = ZoneSendPressureCheckBox.IsChecked == true;
        zone.ReleaseBehavior = _inputMappingService.NormalizeReleaseBehavior(ZoneReleaseBehaviorComboBox.Text);
        zone.ResetPressureOnRelease = string.Equals(zone.ReleaseBehavior, ZoneReleaseBehaviors.ResetPressureOnly, StringComparison.Ordinal);
        SetZoneSelectionSummary(zone);

        RefreshZoneList();
        ZoneListBox.SelectedIndex = _selectedZoneIndex;
    }

    private void SetZoneSelectionSummary(ZoneConfig? zone)
    {
        if (zone is null)
        {
            ZoneSelectionSummaryPrimaryText.Text = "No zone selected";
            ZoneSelectionSummarySecondaryText.Text = "Select a zone to edit its routing and bounds.";
            return;
        }

        string enabled = zone.Enabled ? "Enabled" : "Disabled";
        ZoneSelectionSummaryPrimaryText.Text =
            $"Channel {zone.MidiChannel}  •  CC X/Y/P {zone.CcX}/{zone.CcY}/{zone.CcPressure}  •  {enabled}";
        ZoneSelectionSummarySecondaryText.Text =
            $"Bounds {zone.Left:0.###}, {zone.Top:0.###}, {zone.Width:0.###}, {zone.Height:0.###}  •  " +
            $"Release {_inputMappingService.NormalizeReleaseBehavior(zone.ReleaseBehavior)}";
    }

    private void InitMidi()
    {
        try
        {
            _midiService.Restart(_config);
            _activeZone = null;
            UpdateUiForCurrentConfig();
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = "Windows MIDI Services init failed";
            _viewModel.RoutingText = string.Empty;
            _viewModel.InputBackendText = string.Empty;
            _viewModel.ActiveZoneText = string.Empty;
            _viewModel.ConfigSummaryText = string.Empty;
            _viewModel.ValueText = ex.Message;
            throw;
        }
    }

    private async Task InitializeMidiAsync()
    {
        try
        {
            MidiStatusText.Text = "Initializing MIDI routing...";
            await _midiService.InitializeAsync(_config);

            _activeZone = null;
            UpdateUiForCurrentConfig();
        }
        catch (Exception ex)
        {
            _viewModel.StatusText = "MIDI init failed";
            _viewModel.ValueText = ex.Message;
            throw;
        }
    }

    private void UpdateUiForCurrentConfig()
    {
        _viewModel.StatusText = "Connected";
        _viewModel.RoutingText = $"Send to '{_config.DawPortName}' via '{_config.AppPortName}'";
        UpdateInputBackendStatus();
        _viewModel.ActiveZoneText = "Active zone: none";
        string switchMode = _config.AllowZoneSwitchWhilePressed ? "latch on enter" : "lock zone until release";
        _viewModel.ConfigSummaryText = $"{_config.Zones.Count} zone(s) configured | layout {_config.MaxZoneRows} row(s) max / {_config.MaxZonesPerRow} per row | {switchMode}";
        _viewModel.ValueText = "X: -, Y: -, Pressure: -";
        _viewModel.PenButtonsText = "Pen buttons: -";
        _viewModel.ModifiersText = "Modifiers: none";
    }

    private void ShutdownMidi()
    {
        try
        {
            _midiService.Shutdown();
        }
        catch
        {
        }
    }
}
