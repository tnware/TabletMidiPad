using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace TabletMidiPad;

public partial class PreferencesWindow : Window
{
    public bool Accepted { get; private set; }

    public string DawPortName => DawPortNameTextBox.Text.Trim();
    public string AppPortName => AppPortNameTextBox.Text.Trim();
    public string AssociationId => AssociationIdTextBox.Text.Trim();
    public int MousePressureValue { get; private set; }
    public double PositionDeadZone { get; private set; }
    public double PositionSmoothing { get; private set; }
    public string PressureCurve => PressureCurveComboBox.Text;
    public string InputBackendMode => InputBackendModeComboBox.Text;
    public string LowerPenButtonAction => LowerPenButtonActionComboBox.Text;
    public int LowerPenButtonMidiChannel { get; private set; }
    public int LowerPenButtonCc { get; private set; }
    public int LowerPenButtonNote { get; private set; }
    public string UpperPenButtonAction => UpperPenButtonActionComboBox.Text;
    public int UpperPenButtonMidiChannel { get; private set; }
    public int UpperPenButtonCc { get; private set; }
    public int UpperPenButtonNote { get; private set; }
    public bool SendPressureGlobally => SendPressureGloballyCheckBox.IsChecked == true;
    public bool AllowZoneSwitchWhilePressed => AllowZoneSwitchCheckBox.IsChecked == true;

    private readonly int _expressKeyCount;
    private readonly List<(ComboBox Action, TextBox Channel, TextBox Cc, TextBox Note)> _expressKeyRows = new();

    public PreferencesWindow(int expressKeyCount)
    {
        _expressKeyCount = expressKeyCount;
        InitializeComponent();
        PopulatePressureCurveList();
        PopulateInputBackendModeList();
        PopulatePenButtonActionList();
        BuildExpressKeyRows();
    }

    public void LoadFrom(AppConfig config)
    {
        DawPortNameTextBox.Text = config.DawPortName;
        AppPortNameTextBox.Text = config.AppPortName;
        AssociationIdTextBox.Text = config.LoopbackAssociationId;
        MousePressureTextBox.Text = config.MousePressureValue.ToString();
        DeadZoneTextBox.Text = config.PositionDeadZone.ToString("0.###");
        SmoothingTextBox.Text = config.PositionSmoothing.ToString("0.##");
        PressureCurveComboBox.SelectedItem = config.PressureCurve;
        PressureCurveComboBox.Text = config.PressureCurve;
        InputBackendModeComboBox.SelectedItem = NormalizeInputBackendMode(config.InputBackendMode);
        InputBackendModeComboBox.Text = NormalizeInputBackendMode(config.InputBackendMode);
        LowerPenButtonActionComboBox.SelectedItem = NormalizePenButtonAction(config.LowerPenButtonAction);
        LowerPenButtonActionComboBox.Text = NormalizePenButtonAction(config.LowerPenButtonAction);
        LowerPenButtonMidiChannelTextBox.Text = config.LowerPenButtonMidiChannel.ToString();
        LowerPenButtonCcTextBox.Text = config.LowerPenButtonCc.ToString();
        LowerPenButtonNoteTextBox.Text = config.LowerPenButtonNote.ToString();
        UpperPenButtonActionComboBox.SelectedItem = NormalizePenButtonAction(config.UpperPenButtonAction);
        UpperPenButtonActionComboBox.Text = NormalizePenButtonAction(config.UpperPenButtonAction);
        UpperPenButtonMidiChannelTextBox.Text = config.UpperPenButtonMidiChannel.ToString();
        UpperPenButtonCcTextBox.Text = config.UpperPenButtonCc.ToString();
        UpperPenButtonNoteTextBox.Text = config.UpperPenButtonNote.ToString();
        SendPressureGloballyCheckBox.IsChecked = config.SendPressureGlobally;
        AllowZoneSwitchCheckBox.IsChecked = config.AllowZoneSwitchWhilePressed;

        ExpressKeyConfig.EnsureCount(config.ExpressKeys, _expressKeyCount);
        for (int i = 0; i < _expressKeyRows.Count && i < config.ExpressKeys.Count; i++)
        {
            var row = _expressKeyRows[i];
            var ek = config.ExpressKeys[i];
            row.Action.SelectedItem = NormalizePenButtonAction(ek.Action);
            row.Action.Text = NormalizePenButtonAction(ek.Action);
            row.Channel.Text = ek.MidiChannel.ToString();
            row.Cc.Text = ek.Cc.ToString();
            row.Note.Text = ek.Note.ToString();
        }
    }

    public List<ExpressKeyConfig> GetExpressKeyConfigs()
    {
        var list = new List<ExpressKeyConfig>();
        for (int i = 0; i < _expressKeyRows.Count; i++)
        {
            var row = _expressKeyRows[i];
            list.Add(new ExpressKeyConfig
            {
                Action = NormalizePenButtonAction(row.Action.Text),
                MidiChannel = int.TryParse(row.Channel.Text, out int ch) ? Math.Clamp(ch, 1, 16) : 1,
                Cc = int.TryParse(row.Cc.Text, out int cc) ? Math.Clamp(cc, 0, 127) : 70 + i,
                Note = int.TryParse(row.Note.Text, out int note) ? Math.Clamp(note, 0, 127) : 36 + i
            });
        }
        return list;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Validate();
            Accepted = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Validate()
    {
        RequireText(DawPortNameTextBox.Text, "DAW Input Port");
        RequireText(AppPortNameTextBox.Text, "Internal App Port");
        RequireGuidText(AssociationIdTextBox.Text, "Association Id");
        MousePressureValue = ReadInt(MousePressureTextBox.Text, 0, 127, "Mouse Pressure Fallback");
        PositionDeadZone = ReadDouble(DeadZoneTextBox.Text, 0.0, 0.1, "Position Dead Zone");
        PositionSmoothing = ReadDouble(SmoothingTextBox.Text, 0.0, 0.95, "XY Smoothing");
        ReadPressureCurve(PressureCurveComboBox.Text);
        ReadInputBackendMode(InputBackendModeComboBox.Text);
        ReadPenButtonAction(LowerPenButtonActionComboBox.Text);
        ReadPenButtonAction(UpperPenButtonActionComboBox.Text);
        LowerPenButtonMidiChannel = ReadInt(LowerPenButtonMidiChannelTextBox.Text, 1, 16, "Lower Button MIDI Channel");
        LowerPenButtonCc = ReadInt(LowerPenButtonCcTextBox.Text, 0, 127, "Lower Button CC");
        LowerPenButtonNote = ReadInt(LowerPenButtonNoteTextBox.Text, 0, 127, "Lower Button Note");
        UpperPenButtonMidiChannel = ReadInt(UpperPenButtonMidiChannelTextBox.Text, 1, 16, "Upper Button MIDI Channel");
        UpperPenButtonCc = ReadInt(UpperPenButtonCcTextBox.Text, 0, 127, "Upper Button CC");
        UpperPenButtonNote = ReadInt(UpperPenButtonNoteTextBox.Text, 0, 127, "Upper Button Note");

        for (int i = 0; i < _expressKeyRows.Count; i++)
        {
            var row = _expressKeyRows[i];
            ReadPenButtonAction(row.Action.Text);
            ReadInt(row.Channel.Text, 1, 16, $"EK{i} MIDI Channel");
            ReadInt(row.Cc.Text, 0, 127, $"EK{i} CC");
            ReadInt(row.Note.Text, 0, 127, $"EK{i} Note");
        }
    }

    private void PopulatePressureCurveList()
    {
        PressureCurveComboBox.Items.Clear();
        PressureCurveComboBox.Items.Add("Linear");
        PressureCurveComboBox.Items.Add("Logarithmic");
        PressureCurveComboBox.Items.Add("Exponential");
    }

    private void PopulateInputBackendModeList()
    {
        InputBackendModeComboBox.Items.Clear();
        InputBackendModeComboBox.Items.Add("Auto");
        InputBackendModeComboBox.Items.Add("Wintab Only");
        InputBackendModeComboBox.Items.Add("Stylus/Mouse Only");
    }

    private void PopulatePenButtonActionList()
    {
        foreach (var comboBox in new[] { LowerPenButtonActionComboBox, UpperPenButtonActionComboBox })
        {
            PopulateActionComboBox(comboBox);
        }
    }

    private static void PopulateActionComboBox(ComboBox comboBox)
    {
        comboBox.Items.Clear();
        comboBox.Items.Add(PenButtonActions.None);
        comboBox.Items.Add(PenButtonActions.LockXY);
        comboBox.Items.Add(PenButtonActions.LockX);
        comboBox.Items.Add(PenButtonActions.LockY);
        comboBox.Items.Add(PenButtonActions.DisablePressure);
        comboBox.Items.Add(PenButtonActions.EnablePressure);
        comboBox.Items.Add(PenButtonActions.CcMomentary);
        comboBox.Items.Add(PenButtonActions.CcToggle);
        comboBox.Items.Add(PenButtonActions.NoteMomentary);
        comboBox.Items.Add(PenButtonActions.NoteToggle);
    }

    private void BuildExpressKeyRows()
    {
        if (_expressKeyCount == 0)
            return;

        ExpressKeysPanel.Children.Clear();
        _expressKeyRows.Clear();

        ExpressKeysPanel.Children.Add(new TextBlock
        {
            Text = $"Express Keys ({_expressKeyCount} detected)",
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#e9e9e9")),
            FontSize = 16,
            Margin = new Thickness(0, 22, 0, 8)
        });

        for (int i = 0; i < _expressKeyCount; i++)
        {
            var label = new TextBlock
            {
                Text = $"ExpressKey {i}",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#bcbcbc")),
                Margin = new Thickness(0, i == 0 ? 0 : 12, 0, 0)
            };

            var actionCombo = new ComboBox { Margin = new Thickness(0, 6, 0, 0) };
            PopulateActionComboBox(actionCombo);

            var channelBox = new TextBox { Margin = new Thickness(0, 6, 0, 0) };
            var ccBox = new TextBox { Margin = new Thickness(0, 6, 0, 0) };
            var noteBox = new TextBox { Margin = new Thickness(0, 6, 0, 0) };

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var chLabel = new StackPanel();
            chLabel.Children.Add(new TextBlock
            {
                Text = "MIDI Ch",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#bcbcbc"))
            });
            chLabel.Children.Add(channelBox);
            Grid.SetColumn(chLabel, 0);
            grid.Children.Add(chLabel);

            var ccLabel = new StackPanel();
            ccLabel.Children.Add(new TextBlock
            {
                Text = "CC",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#bcbcbc"))
            });
            ccLabel.Children.Add(ccBox);
            Grid.SetColumn(ccLabel, 2);
            grid.Children.Add(ccLabel);

            var noteLabel = new StackPanel();
            noteLabel.Children.Add(new TextBlock
            {
                Text = "Note",
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#bcbcbc"))
            });
            noteLabel.Children.Add(noteBox);
            Grid.SetColumn(noteLabel, 4);
            grid.Children.Add(noteLabel);

            ExpressKeysPanel.Children.Add(label);
            ExpressKeysPanel.Children.Add(actionCombo);
            ExpressKeysPanel.Children.Add(grid);

            _expressKeyRows.Add((actionCombo, channelBox, ccBox, noteBox));
        }
    }

    private static int ReadInt(string? text, int min, int max, string fieldName)
    {
        if (!int.TryParse(text, out int value))
            throw new InvalidOperationException($"{fieldName} must be a whole number.");
        if (value < min || value > max)
            throw new InvalidOperationException($"{fieldName} must be between {min} and {max}.");
        return value;
    }

    private static double ReadDouble(string? text, double min, double max, string fieldName)
    {
        if (!double.TryParse(text, out double value))
            throw new InvalidOperationException($"{fieldName} must be a number.");
        if (value < min || value > max)
            throw new InvalidOperationException($"{fieldName} must be between {min:0.###} and {max:0.###}.");
        return value;
    }

    private static string RequireText(string? text, string fieldName)
    {
        string value = (text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{fieldName} is required.");
        return value;
    }

    private static void RequireGuidText(string? text, string fieldName)
    {
        string value = RequireText(text, fieldName);
        if (!Guid.TryParse(value, out _))
            throw new InvalidOperationException($"{fieldName} must be a valid GUID.");
    }

    private static void ReadPressureCurve(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        if (value is not ("Linear" or "Logarithmic" or "Exponential"))
            throw new InvalidOperationException("Pressure Curve must be Linear, Logarithmic, or Exponential.");
    }

    private static void ReadInputBackendMode(string? text)
    {
        string value = NormalizeInputBackendMode(text);
        if (value is not ("Auto" or "Wintab Only" or "Stylus/Mouse Only"))
            throw new InvalidOperationException("Input Backend must be Auto, Wintab Only, or Stylus/Mouse Only.");
    }

    private static void ReadPenButtonAction(string? text)
    {
        _ = NormalizePenButtonAction(text);
    }

    private static string NormalizeInputBackendMode(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            "Wintab Only" => value,
            "Stylus/Mouse Only" => value,
            _ => "Auto"
        };
    }

    private static string NormalizePenButtonAction(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            PenButtonActions.None => value,
            PenButtonActions.LockXY => value,
            PenButtonActions.LockX => value,
            PenButtonActions.LockY => value,
            PenButtonActions.DisablePressure => value,
            PenButtonActions.EnablePressure => value,
            PenButtonActions.CcMomentary => value,
            PenButtonActions.CcToggle => value,
            PenButtonActions.NoteMomentary => value,
            PenButtonActions.NoteToggle => value,
            _ => PenButtonActions.None
        };
    }
}
