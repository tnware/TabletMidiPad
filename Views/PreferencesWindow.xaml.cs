using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using TabletMidiPad.Services.Zone.Models;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

internal partial class PreferencesWindow : Window
{
    public bool Accepted { get; private set; }
    private readonly PreferencesViewModel _viewModel;
    private readonly IConfigValueParser _configValueParser;

    public string DawPortName => _viewModel.DawPortName.Trim();
    public string AppPortName => _viewModel.AppPortName.Trim();
    public string AssociationId => _viewModel.AssociationId.Trim();
    public int MousePressureValue { get; private set; }
    public double PositionDeadZone { get; private set; }
    public double PositionSmoothing { get; private set; }
    public string PressureCurve => _viewModel.PressureCurve;
    public string InputBackendMode => _viewModel.InputBackendMode;
    public string LowerPenButtonAction => _viewModel.LowerPenButtonAction;
    public int LowerPenButtonMidiChannel { get; private set; }
    public int LowerPenButtonCc { get; private set; }
    public int LowerPenButtonNote { get; private set; }
    public string UpperPenButtonAction => _viewModel.UpperPenButtonAction;
    public int UpperPenButtonMidiChannel { get; private set; }
    public int UpperPenButtonCc { get; private set; }
    public int UpperPenButtonNote { get; private set; }
    public bool SendPressureGlobally => _viewModel.SendPressureGlobally;
    public bool AllowZoneSwitchWhilePressed => _viewModel.AllowZoneSwitchWhilePressed;
    public ZoneOverlayStyle ZoneOverlayStyle { get; private set; } = ZoneOverlayStyle.Plain;

    private readonly int _expressKeyCount;

    public PreferencesWindow(int expressKeyCount, IConfigValueParser configValueParser, PreferencesViewModel viewModel)
    {
        _expressKeyCount = expressKeyCount;
        _configValueParser = configValueParser ?? throw new ArgumentNullException(nameof(configValueParser));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        InitializeComponent();
        DataContext = _viewModel;
        WindowChromeTheme.ApplyDarkNonClientChrome(this);
        PopulatePressureCurveList();
        PopulateInputBackendModeList();
        PopulatePenButtonActionList();
        PopulateMidiChannelCombo(LowerPenButtonMidiChannelComboBox);
        PopulateMidiChannelCombo(UpperPenButtonMidiChannelComboBox);
        SetMidiChannelComboSelection(LowerPenButtonMidiChannelComboBox, 1);
        SetMidiChannelComboSelection(UpperPenButtonMidiChannelComboBox, 1);
        _viewModel.EnsureExpressKeyCount(_expressKeyCount);
        BuildExpressKeyRows();
        BuildStyleCards();
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

    private Brush ResolveThemeBrush(string key, Brush fallback)
        => TryFindResource(key) as Brush ?? Application.Current?.TryFindResource(key) as Brush ?? fallback;

    public void LoadFrom(AppConfig config)
    {
        _viewModel.DawPortName = config.DawPortName;
        _viewModel.AppPortName = config.AppPortName;
        _viewModel.AssociationId = config.LoopbackAssociationId;
        _viewModel.MousePressureText = config.MousePressureValue.ToString(CultureInfo.InvariantCulture);
        _viewModel.PositionDeadZoneText = config.PositionDeadZone.ToString("0.###", CultureInfo.InvariantCulture);
        _viewModel.PositionSmoothingText = config.PositionSmoothing.ToString("0.##", CultureInfo.InvariantCulture);
        _viewModel.PressureCurve = config.PressureCurve;
        _viewModel.InputBackendMode = InputBackendModeNormalizer.Normalize(config.InputBackendMode);
        _viewModel.LowerPenButtonAction = _configValueParser.NormalizePenButtonAction(config.LowerPenButtonAction);
        SetMidiChannelComboSelection(LowerPenButtonMidiChannelComboBox, config.LowerPenButtonMidiChannel);
        _viewModel.LowerPenButtonCcText = config.LowerPenButtonCc.ToString(CultureInfo.InvariantCulture);
        _viewModel.LowerPenButtonNoteText = config.LowerPenButtonNote.ToString(CultureInfo.InvariantCulture);
        _viewModel.UpperPenButtonAction = _configValueParser.NormalizePenButtonAction(config.UpperPenButtonAction);
        SetMidiChannelComboSelection(UpperPenButtonMidiChannelComboBox, config.UpperPenButtonMidiChannel);
        _viewModel.UpperPenButtonCcText = config.UpperPenButtonCc.ToString(CultureInfo.InvariantCulture);
        _viewModel.UpperPenButtonNoteText = config.UpperPenButtonNote.ToString(CultureInfo.InvariantCulture);
        _viewModel.SendPressureGlobally = config.SendPressureGlobally;
        _viewModel.AllowZoneSwitchWhilePressed = config.AllowZoneSwitchWhilePressed;
        SelectZoneOverlayStyle(config.ZoneOverlayStyle);

        ExpressKeyConfig.EnsureCount(config.ExpressKeys, _expressKeyCount);
        _viewModel.EnsureExpressKeyCount(_expressKeyCount);
        for (int i = 0; i < _viewModel.ExpressKeys.Count && i < config.ExpressKeys.Count; i++)
        {
            ExpressKeyConfig ek = config.ExpressKeys[i];
            ExpressKeyRowViewModel row = _viewModel.ExpressKeys[i];
            row.Action = _configValueParser.NormalizePenButtonAction(ek.Action);
            row.MidiChannelText = Math.Clamp(ek.MidiChannel, 1, 16).ToString(CultureInfo.InvariantCulture);
            row.CcText = Math.Clamp(ek.Cc, 0, 127).ToString(CultureInfo.InvariantCulture);
            row.NoteText = Math.Clamp(ek.Note, 0, 127).ToString(CultureInfo.InvariantCulture);
        }
    }

    public List<ExpressKeyConfig> GetExpressKeyConfigs()
    {
        var list = new List<ExpressKeyConfig>();
        for (int i = 0; i < _viewModel.ExpressKeys.Count; i++)
        {
            ExpressKeyRowViewModel row = _viewModel.ExpressKeys[i];
            list.Add(new ExpressKeyConfig
            {
                Action = _configValueParser.NormalizePenButtonAction(row.Action),
                MidiChannel = ReadMidiChannelText(row.MidiChannelText, $"ExpressKey {i + 1} MIDI Channel"),
                Cc = int.TryParse(row.CcText, out int cc) ? Math.Clamp(cc, 0, 127) : 70 + i,
                Note = int.TryParse(row.NoteText, out int note) ? Math.Clamp(note, 0, 127) : 36 + i
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
        {
            AdjustMidiCcOrNote(tb, -1);
        }
    }

    private void MidiCcNoteStepper_Increase_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RepeatButton { Tag: TextBox tb })
        {
            AdjustMidiCcOrNote(tb, 1);
        }
    }

    private static void AdjustMidiCcOrNote(TextBox textBox, int delta)
    {
        int current = int.TryParse(textBox.Text, out int v) ? v : 0;
        int next = Math.Clamp(current + delta, 0, 127);
        textBox.Text = next.ToString(CultureInfo.InvariantCulture);
    }

    private static void PopulateMidiChannelCombo(ComboBox comboBox)
    {
        comboBox.Items.Clear();
        for (int channel = 1; channel <= 16; channel++)
        {
            comboBox.Items.Add(channel);
        }
    }

    private static void SetMidiChannelComboSelection(ComboBox comboBox, int channel)
    {
        int clamped = Math.Clamp(channel, 1, 16);
        comboBox.SelectedItem = clamped;
        comboBox.Text = clamped.ToString(CultureInfo.InvariantCulture);
    }

    private int ReadMidiChannelFromCombo(ComboBox comboBox, string fieldName)
    {
        if (comboBox.SelectedItem is int ch && ch is >= 1 and <= 16)
        {
            return ch;
        }

        if (int.TryParse(comboBox.Text?.Trim(), out int parsed))
        {
            return _configValueParser.ReadInt(parsed.ToString(CultureInfo.InvariantCulture), 1, 16, fieldName);
        }

        // Treat empty/unset combo text as the default MIDI channel rather than hard-failing.
        return 1;
    }

    private int ReadMidiChannelText(string? text, string fieldName)
        => _configValueParser.ReadInt(text?.Trim(), 1, 16, fieldName);

    private void Validate()
    {
        _ = _configValueParser.RequireText(_viewModel.DawPortName, "DAW Input Port");
        _ = _configValueParser.RequireText(_viewModel.AppPortName, "Internal App Port");
        _ = _configValueParser.RequireGuidText(_viewModel.AssociationId, "Association Id");
        MousePressureValue = _configValueParser.ReadInt(_viewModel.MousePressureText, 0, 127, "Mouse Pressure Fallback");
        PositionDeadZone = _configValueParser.ReadDouble(_viewModel.PositionDeadZoneText, 0.0, 0.1, "Position Dead Zone");
        PositionSmoothing = _configValueParser.ReadDouble(_viewModel.PositionSmoothingText, 0.0, 0.95, "XY Smoothing");
        _ = _configValueParser.RequirePressureCurve(_viewModel.PressureCurve);
        _viewModel.InputBackendMode = InputBackendModeNormalizer.Normalize(_viewModel.InputBackendMode);
        _ = _configValueParser.NormalizePenButtonAction(_viewModel.LowerPenButtonAction);
        _ = _configValueParser.NormalizePenButtonAction(_viewModel.UpperPenButtonAction);
        LowerPenButtonMidiChannel = ReadMidiChannelFromCombo(LowerPenButtonMidiChannelComboBox, "Lower Button MIDI Channel");
        LowerPenButtonCc = _configValueParser.ReadInt(_viewModel.LowerPenButtonCcText, 0, 127, "Lower Button CC");
        LowerPenButtonNote = _configValueParser.ReadInt(_viewModel.LowerPenButtonNoteText, 0, 127, "Lower Button Note");
        UpperPenButtonMidiChannel = ReadMidiChannelFromCombo(UpperPenButtonMidiChannelComboBox, "Upper Button MIDI Channel");
        UpperPenButtonCc = _configValueParser.ReadInt(_viewModel.UpperPenButtonCcText, 0, 127, "Upper Button CC");
        UpperPenButtonNote = _configValueParser.ReadInt(_viewModel.UpperPenButtonNoteText, 0, 127, "Upper Button Note");

        for (int i = 0; i < _viewModel.ExpressKeys.Count; i++)
        {
            ExpressKeyRowViewModel row = _viewModel.ExpressKeys[i];
            _ = _configValueParser.NormalizePenButtonAction(row.Action);
            ReadMidiChannelText(row.MidiChannelText, $"ExpressKey {i + 1} MIDI Channel");
            _ = _configValueParser.ReadInt(row.CcText, 0, 127, $"ExpressKey {i + 1} CC");
            _ = _configValueParser.ReadInt(row.NoteText, 0, 127, $"ExpressKey {i + 1} Note");
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
        foreach (ComboBox? comboBox in new[] { LowerPenButtonActionComboBox, UpperPenButtonActionComboBox })
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
        {
            return;
        }

        ExpressKeysPanel.Children.Clear();
        _viewModel.EnsureExpressKeyCount(_expressKeyCount);

        ExpressKeysPanel.Children.Add(new TextBlock
        {
            Text = $"Express Keys ({_expressKeyCount} detected)",
            Foreground = ResolveThemeBrush("TextPrimaryBrush", Brushes.White),
            FontSize = 16,
            Margin = new Thickness(0, 22, 0, 8)
        });

        for (int i = 0; i < _expressKeyCount; i++)
        {
            ExpressKeyRowViewModel rowVm = _viewModel.ExpressKeys[i];
            var label = new TextBlock
            {
                Text = $"ExpressKey {i + 1}",
                Foreground = ResolveThemeBrush("TextPrimaryBrush", Brushes.White),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };

            var hint = new TextBlock
            {
                Text = "Action and MIDI mapping for this hardware key.",
                Foreground = ResolveThemeBrush("TextMutedBrush", Brushes.Gray),
                Margin = new Thickness(0, 4, 0, 0)
            };

            var actionCombo = new ComboBox { Margin = new Thickness(0, 6, 0, 0) };
            PopulateActionComboBox(actionCombo);
            actionCombo.SetBinding(ComboBox.TextProperty, new System.Windows.Data.Binding(nameof(ExpressKeyRowViewModel.Action))
            {
                Source = rowVm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });

            var channelCombo = new ComboBox { Margin = new Thickness(0, 6, 0, 0) };
            PopulateMidiChannelCombo(channelCombo);
            channelCombo.SetBinding(ComboBox.TextProperty, new System.Windows.Data.Binding(nameof(ExpressKeyRowViewModel.MidiChannelText))
            {
                Source = rowVm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });

            var ccBox = new TextBox { Margin = new Thickness(0, 0, 6, 0) };
            var noteBox = new TextBox { Margin = new Thickness(0, 0, 6, 0) };
            NumericTextBoxBehavior.SetMode(ccBox, NumericTextMode.Integer);
            NumericTextBoxBehavior.SetMode(noteBox, NumericTextMode.Integer);
            ccBox.HorizontalContentAlignment = HorizontalAlignment.Right;
            noteBox.HorizontalContentAlignment = HorizontalAlignment.Right;
            ccBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(ExpressKeyRowViewModel.CcText))
            {
                Source = rowVm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });
            noteBox.SetBinding(TextBox.TextProperty, new System.Windows.Data.Binding(nameof(ExpressKeyRowViewModel.NoteText))
            {
                Source = rowVm,
                Mode = System.Windows.Data.BindingMode.TwoWay,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged
            });

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
                Foreground = ResolveThemeBrush("TextMutedBrush", Brushes.Gray)
            });
            chLabel.Children.Add(channelCombo);
            Grid.SetColumn(chLabel, 0);
            grid.Children.Add(chLabel);

            var ccLabel = new StackPanel();
            ccLabel.Children.Add(new TextBlock
            {
                Text = "CC",
                Foreground = ResolveThemeBrush("TextMutedBrush", Brushes.Gray)
            });
            ccLabel.Children.Add(CreateMidiCcNoteStepperRow(ccBox, stepperStyle));
            Grid.SetColumn(ccLabel, 2);
            grid.Children.Add(ccLabel);

            var noteLabel = new StackPanel();
            noteLabel.Children.Add(new TextBlock
            {
                Text = "Note",
                Foreground = ResolveThemeBrush("TextMutedBrush", Brushes.Gray)
            });
            noteLabel.Children.Add(CreateMidiCcNoteStepperRow(noteBox, stepperStyle));
            Grid.SetColumn(noteLabel, 4);
            grid.Children.Add(noteLabel);

            var card = new Border
            {
                Background = ResolveThemeBrush("PanelBrush", Brushes.Black),
                BorderBrush = ResolveThemeBrush("PanelBorderBrush", Brushes.DimGray),
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
        }
    }

    private static readonly (ZoneOverlayStyle Style, string Name, string Description)[] OverlayStyleOptions =
    [
        (ZoneOverlayStyle.Plain,        "Plain",        "Solid fill with no interior markings."),
        (ZoneOverlayStyle.VignetteGrid, "Vignette Grid", "Grid lines that fade toward the edges."),
        (ZoneOverlayStyle.DotArray,     "Dot Array",    "Regular dot pattern across the zone."),
        (ZoneOverlayStyle.XYGraph,      "XY Graph",     "Axis lines crossing at the zone center."),
    ];

    private readonly Dictionary<ZoneOverlayStyle, Border> _styleCards = new();

    private void BuildStyleCards()
    {
        StyleCardPanel.Children.Clear();
        _styleCards.Clear();

        Brush textPrimary = ResolveThemeBrush("TextPrimaryBrush", Brushes.White);
        Brush textMuted = ResolveThemeBrush("TextMutedBrush", Brushes.Gray);
        Brush panelBrush = ResolveThemeBrush("PanelBrush", Brushes.Black);
        Brush panelBorder = ResolveThemeBrush("PanelBorderBrush", Brushes.DimGray);
        Brush accentSurface = ResolveThemeBrush("AccentSurfaceBrush", Brushes.DarkGreen);
        Brush accentBorder = ResolveThemeBrush("AccentBrush", Brushes.LimeGreen);

        foreach ((ZoneOverlayStyle style, string name, string description) in OverlayStyleOptions)
        {
            var preview = new ZoneStylePreviewElement
            {
                OverlayStyle = style,
                Width = 140,
                Height = 88,
                Margin = new Thickness(0, 0, 0, 8)
            };

            var nameLabel = new TextBlock
            {
                Text = name,
                Foreground = textPrimary,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center
            };

            var descLabel = new TextBlock
            {
                Text = description,
                Foreground = textMuted,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };

            var inner = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            inner.Children.Add(preview);
            inner.Children.Add(nameLabel);
            inner.Children.Add(descLabel);

            var card = new Border
            {
                Background = panelBrush,
                BorderBrush = panelBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 12, 12),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = inner,
                Width = 172
            };

            ZoneOverlayStyle capturedStyle = style;
            card.MouseLeftButtonUp += (_, _) => SelectZoneOverlayStyle(capturedStyle);

            _styleCards[style] = card;
            StyleCardPanel.Children.Add(card);
        }

        ApplyStyleCardHighlight(panelBrush, panelBorder, accentSurface, accentBorder);
    }

    private void SelectZoneOverlayStyle(ZoneOverlayStyle style)
    {
        ZoneOverlayStyle = style;

        if (_styleCards.Count == 0)
        {
            return;
        }

        Brush panelBrush = ResolveThemeBrush("PanelBrush", Brushes.Black);
        Brush panelBorder = ResolveThemeBrush("PanelBorderBrush", Brushes.DimGray);
        Brush accentSurface = ResolveThemeBrush("AccentSurfaceBrush", Brushes.DarkGreen);
        Brush accentBorder = ResolveThemeBrush("AccentBrush", Brushes.LimeGreen);
        ApplyStyleCardHighlight(panelBrush, panelBorder, accentSurface, accentBorder);
    }

    private void ApplyStyleCardHighlight(Brush panelBrush, Brush panelBorder, Brush accentSurface, Brush accentBorder)
    {
        foreach ((ZoneOverlayStyle style, Border card) in _styleCards)
        {
            bool selected = style == ZoneOverlayStyle;
            card.Background = selected ? accentSurface : panelBrush;
            card.BorderBrush = selected ? accentBorder : panelBorder;
            card.BorderThickness = new Thickness(selected ? 2 : 1);
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
}
