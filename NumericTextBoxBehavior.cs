using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TabletMidiPad;

public enum NumericTextMode
{
    None,
    Integer,
    Decimal
}

public static class NumericTextBoxBehavior
{
    public static readonly DependencyProperty ModeProperty =
        DependencyProperty.RegisterAttached(
            "Mode",
            typeof(NumericTextMode),
            typeof(NumericTextBoxBehavior),
            new PropertyMetadata(NumericTextMode.None, OnModeChanged));

    public static NumericTextMode GetMode(DependencyObject obj) => (NumericTextMode)obj.GetValue(ModeProperty);

    public static void SetMode(DependencyObject obj, NumericTextMode value) => obj.SetValue(ModeProperty, value);

    private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox textBox)
        {
            return;
        }

        textBox.PreviewTextInput -= OnPreviewTextInput;
        DataObject.RemovePastingHandler(textBox, OnPaste);

        if ((NumericTextMode)e.NewValue == NumericTextMode.None)
        {
            return;
        }

        textBox.PreviewTextInput += OnPreviewTextInput;
        DataObject.AddPastingHandler(textBox, OnPaste);
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        e.Handled = !IsTextAllowed(textBox, e.Text);
    }

    private static void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        if (!e.DataObject.GetDataPresent(typeof(string)))
        {
            e.CancelCommand();
            return;
        }

        string pasted = (string)e.DataObject.GetData(typeof(string));
        if (!IsTextAllowed(textBox, pasted))
        {
            e.CancelCommand();
        }
    }

    private static bool IsTextAllowed(TextBox textBox, string newText)
    {
        NumericTextMode mode = GetMode(textBox);
        if (mode == NumericTextMode.None)
        {
            return true;
        }

        string separator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
        string next = GetProposedText(textBox, newText);

        if (string.IsNullOrEmpty(next))
        {
            return true;
        }

        if (mode == NumericTextMode.Integer)
        {
            foreach (char ch in next)
            {
                if (!char.IsDigit(ch))
                {
                    return false;
                }
            }

            return true;
        }

        bool separatorSeen = false;
        for (int i = 0; i < next.Length; i++)
        {
            char ch = next[i];
            if (char.IsDigit(ch))
            {
                continue;
            }

            if (!separatorSeen && next.AsSpan(i).StartsWith(separator, StringComparison.CurrentCulture))
            {
                separatorSeen = true;
                i += separator.Length - 1;
                continue;
            }

            return false;
        }

        return true;
    }

    private static string GetProposedText(TextBox textBox, string newText)
    {
        string current = textBox.Text ?? string.Empty;
        int selectionStart = textBox.SelectionStart;
        int selectionLength = textBox.SelectionLength;
        return current.Remove(selectionStart, selectionLength).Insert(selectionStart, newText);
    }
}
