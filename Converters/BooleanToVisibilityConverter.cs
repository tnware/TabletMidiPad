using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TabletMidiPad.Converters;

/// <summary>
/// Converts boolean to <see cref="Visibility"/> (true = Visible, false = Collapsed).
/// </summary>
internal sealed class BooleanToVisibilityConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool invert = parameter is string s && s.Equals("Invert", StringComparison.OrdinalIgnoreCase);
        bool flag = value is true;
        if (invert)
        {
            flag = !flag;
        }

        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility v)
        {
            return v == Visibility.Visible;
        }

        return false;
    }
}
