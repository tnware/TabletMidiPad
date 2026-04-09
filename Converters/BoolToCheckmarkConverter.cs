using System.Globalization;
using System.Windows.Data;

namespace TabletMidiPad.Converters;

/// <summary>
/// Converts a boolean to a checkmark glyph string for menu icons.
/// </summary>
internal sealed class BoolToCheckmarkConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is true ? "✓" : string.Empty;
    }

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("BoolToCheckmarkConverter only supports one-way conversion.");
    }
}
