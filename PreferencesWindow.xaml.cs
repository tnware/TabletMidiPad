using System;
using System.Windows;

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

    public PreferencesWindow()
    {
        InitializeComponent();
        PopulatePressureCurveList();
        PopulateInputBackendModeList();
        PopulatePenButtonActionList();
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
