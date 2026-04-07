using System;
using System.Windows;
using TabletMidiPad.Infrastructure;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Views;

public partial class PreferencesWindow : Window
{
    private PreferencesViewModel ViewModel => (PreferencesViewModel)DataContext;

    public PreferencesWindow(PreferencesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        WindowChromeTheme.ApplyDarkNonClientChrome(this);
        viewModel.RequestClose += () => Close();
    }

    private void ComboBox_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e) => e.Handled = true;

    private void MidiCcNoteStepper_Decrease_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.RepeatButton { Tag: System.Windows.Controls.TextBox tb })
        {
            if (int.TryParse(tb.Text, out int v)) tb.Text = Math.Clamp(v - 1, 0, 127).ToString();
        }
    }

    private void MidiCcNoteStepper_Increase_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Primitives.RepeatButton { Tag: System.Windows.Controls.TextBox tb })
        {
            if (int.TryParse(tb.Text, out int v)) tb.Text = Math.Clamp(v + 1, 0, 127).ToString();
        }
    }
}
