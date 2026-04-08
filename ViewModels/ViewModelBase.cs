using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TabletMidiPad.ViewModels;

/// <summary>
/// Base class for observable view models.
/// </summary>
internal abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises a property changed notification.
    /// </summary>
    /// <param name="propertyName">The changed property name.</param>
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets a field and raises change notification when value differs.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="field">Field storage reference.</param>
    /// <param name="value">New value.</param>
    /// <param name="propertyName">The changed property name.</param>
    /// <returns>True when the value changed.</returns>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
