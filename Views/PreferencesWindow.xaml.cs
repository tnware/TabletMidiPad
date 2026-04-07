using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TabletMidiPad;

namespace TabletMidiPad.Views;

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
    private readonly List<(ComboBox Action, ComboBox Channel, TextBox Cc, TextBox Note)> _expressKeyRows = new();

    public PreferencesWindow(int expressKeyCount)
    {
        _expressKeyCount = expressKeyCount;
        InitializeComponent();
        WindowChromeTheme.ApplyDarkNonClientChrome(this);
        PopulatePressureCurveList();
        PopulateInputBackendModeList();
        PopulatePenButtonActionList();
        PopulateMidiChannelCombo(LowerPenButtonMidiChannelComboBox);
        PopulateMidiChannelCombo(UpperPenButtonMidiChannelComboBox);
        BuildExpressKeyRows();
        ConfigureNumericInputs();
    }

    private void ConfigureNumericInputs()
    {
        foreach (TextBox textBox in new[]
        {
            MousePressureTextBox,
            LowerPenButtonCcTextBox,
            LowerPenButtonNoteTextBox,
            UpperPenButtonCcTextBox,
            UpperPenButtonNoteTextBox
        })
        {
            NumericTextBoxBehavior.SetMode(textBox, NumericTextMode.Integer);
            textBox.HorizontalContentAlignment = HorizontalAlignment.Right;
        }

        foreach (TextBox textBox in new[]
        {
            DeadZoneTextBox,
            SmoothingTextBox
        })
        {
            NumericTextBoxBehavior.SetMode(textBox, NumericTextMode.Decimal);
            textBox.HorizontalContentAlignment = HorizontalAlignment.Right;
        }
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
        SetMidiChannelComboSelection(LowerPenButtonMidiChannelComboBox, config.LowerPenButtonMidiChannel);
        LowerPenButtonCcTextBox.Text = config.LowerPenButtonCc.ToString();
        LowerPenButtonNoteTextBox.Text = config.LowerPenButtonNote.ToString();
        UpperPenButtonActionComboBox.SelectedItem = NormalizePenButtonAction(config.UpperPenButtonAction);
        UpperPenButtonActionComboBox.Text = NormalizePenButtonAction(config.UpperPenButtonAction);
        SetMidiChannelComboSelection(UpperPenButtonMidiChannelComboBox, config.UpperPenButtonMidiChannel);
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
            SetMidiChannelComboSelection(row.Channel, ek.MidiChannel);
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
                MidiChannel = ReadMidiChannelFromCombo(row.Channel, $"ExpressKey {i + 1} MIDI Channel"),
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

    private void ComboBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        e.Handled = true;
    }

    private void MidiCcNoteStepper_Decrease_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RepeatButton { Tag: TextBox tb })
            AdjustMidiCcOrNote(tb, -1);
    }

    private void MidiCcNoteStepper_Increase_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RepeatButton { Tag: TextBox tb })
            AdjustMidiCcOrNote(tb, 1);
    }

    private static void AdjustMidiCcOrNote(TextBox textBox, int delta)
    {
        int current = int.TryParse(textBox.Text, out int v) ? v : 0;
        int next = Math.Clamp(current + delta, 0, 127);
        textBox.Text = next.ToString();
    }

    private static void PopulateMidiChannelCombo(ComboBox comboBox)
    {
        comboBox.Items.Clear();
        for (int channel = 1; channel <= 16; channel++)
            comboBox.Items.Add(channel);
    }

    private static void SetMidiChannelComboSelection(ComboBox comboBox, int channel)
    {
        int clamped = Math.Clamp(channel, 1, 16);
        comboBox.SelectedItem = clamped;
        comboBox.Text = clamped.ToString();
    }

    private static int ReadMidiChannelFromCombo(ComboBox comboBox, string fieldName)
    {
        if (comboBox.SelectedItem is int ch && ch is >= 1 and <= 16)
            return ch;

        if (int.TryParse(comboBox.Text?.Trim(), out int parsed))
            return ReadInt(parsed.ToString(), 1, 16, fieldName);

        throw new InvalidOperationException($"{fieldName} must be between 1 and 16.");
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
        LowerPenButtonMidiChannel = ReadMidiChannelFromCombo(LowerPenButtonMidiChannelComboBox, "Lower Button MIDI Channel");
        LowerPenButtonCc = ReadInt(LowerPenButtonCcTextBox.Text, 0, 127, "Lower Button CC");
        LowerPenButtonNote = ReadInt(LowerPenButtonNoteTextBox.Text, 0, 127, "Lower Button Note");
        UpperPenButtonMidiChannel = ReadMidiChannelFromCombo(UpperPenButtonMidiChannelComboBox, "Upper Button MIDI Channel");
        UpperPenButtonCc = ReadInt(UpperPenButtonCcTextBox.Text, 0, 127, "Upper Button CC");
        UpperPenButtonNote = ReadInt(UpperPenButtonNoteTextBox.Text, 0, 127, "Upper Button Note");

        for (int i = 0; i < _expressKeyRows.Count; i++)
        {
            var row = _expressKeyRows[i];
            ReadPenButtonAction(row.Action.Text);
            ReadMidiChannelFromCombo(row.Channel, $"ExpressKey {i + 1} MIDI Channel");
            ReadInt(row.Cc.Text, 0, 127, $"ExpressKey {i + 1} CC");
            ReadInt(row.Note.Text, 0, 127, $"ExpressKey {i + 1} Note");
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
            Foreground = Application.Current.Resources["TextPrimaryBrush"] as Brush ?? Brushes.White,
            FontSize = 16,
            Margin = new Thickness(0, 22, 0, 8)
        });

        for (int i = 0; i < _expressKeyCount; i++)
        {
            var label = new TextBlock
            {
                Text = $"ExpressKey {i + 1}",
                Foreground = Application.Current.Resources["TextPrimaryBrush"] as Brush ?? Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };

            var hint = new TextBlock
            {
                Text = "Action and MIDI mapping for this hardware key.",
                Foreground = Application.Current.Resources["TextMutedBrush"] as Brush ?? Brushes.Gray,
                Margin = new Thickness(0, 4, 0, 0)
            };

            var actionCombo = new ComboBox { Margin = new Thickness(0, 6, 0, 0) };
            PopulateActionComboBox(actionCombo);

            var channelCombo = new ComboBox { Margin = new Thickness(0, 6, 0, 0) };
            PopulateMidiChannelCombo(channelCombo);

            var ccBox = new TextBox { Margin = new Thickness(0, 0, 6, 0) };
            var noteBox = new TextBox { Margin = new Thickness(0, 0, 6, 0) };
            NumericTextBoxBehavior.SetMode(ccBox, NumericTextMode.Integer);
            NumericTextBoxBehavior.SetMode(noteBox, NumericTextMode.Integer);
            ccBox.HorizontalContentAlignment = HorizontalAlignment.Right;
            noteBox.HorizontalContentAlignment = HorizontalAlignment.Right;

            var stepperStyle = (Style)FindResource("NumericStepperRepeatStyle");

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
                Foreground = Application.Current.Resources["TextMutedBrush"] as Brush ?? Brushes.Gray
            });
            chLabel.Children.Add(channelCombo);
            Grid.SetColumn(chLabel, 0);
            grid.Children.Add(chLabel);

            var ccLabel = new StackPanel();
            ccLabel.Children.Add(new TextBlock
            {
                Text = "CC",
                Foreground = Application.Current.Resources["TextMutedBrush"] as Brush ?? Brushes.Gray
            });
            ccLabel.Children.Add(CreateMidiCcNoteStepperRow(ccBox, stepperStyle));
            Grid.SetColumn(ccLabel, 2);
            grid.Children.Add(ccLabel);

            var noteLabel = new StackPanel();
            noteLabel.Children.Add(new TextBlock
            {
                Text = "Note",
                Foreground = Application.Current.Resources["TextMutedBrush"] as Brush ?? Brushes.Gray
            });
            noteLabel.Children.Add(CreateMidiCcNoteStepperRow(noteBox, stepperStyle));
            Grid.SetColumn(noteLabel, 4);
            grid.Children.Add(noteLabel);

            var card = new Border
            {
                Background = Application.Current.Resources["PanelBrush"] as Brush ?? Brushes.Black,
                BorderBrush = Application.Current.Resources["PanelBorderBrush"] as Brush ?? Brushes.DimGray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16),
                Margin = new Thickness(0, i == 0 ? 0 : 14, 0, 0)
            };

            var stack = new StackPanel();
            stack.Children.Add(label);
            stack.Children.Add(hint);
            stack.Children.Add(actionCombo);
            stack.Children.Add(grid);
            card.Child = stack;

            ExpressKeysPanel.Children.Add(card);

            _expressKeyRows.Add((actionCombo, channelCombo, ccBox, noteBox));
        }
    }

    private Grid CreateMidiCcNoteStepperRow(TextBox valueBox, Style stepperStyle)
    {
        var grid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var down = new RepeatButton
        {
            Style = stepperStyle,
            Content = "−",
            Margin = new Thickness(0, 0, 6, 0),
            Tag = valueBox
        };
        down.Click += MidiCcNoteStepper_Decrease_Click;
        Grid.SetColumn(down, 0);
        grid.Children.Add(down);

        Grid.SetColumn(valueBox, 1);
        grid.Children.Add(valueBox);

        var up = new RepeatButton
        {
            Style = stepperStyle,
            Content = "+",
            Tag = valueBox
        };
        up.Click += MidiCcNoteStepper_Increase_Click;
        Grid.SetColumn(up, 2);
        grid.Children.Add(up);

        return grid;
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