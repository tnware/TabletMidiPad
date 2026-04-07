using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TabletMidiPad.ViewModels;

public sealed class PreferencesViewModel : INotifyPropertyChanged
{
    private string _dawPortName = string.Empty;
    private string _appPortName = string.Empty;
    private string _associationId = string.Empty;

    public string DawPortName
    {
        get => _dawPortName;
        set => SetField(ref _dawPortName, value);
    }

    public string AppPortName
    {
        get => _appPortName;
        set => SetField(ref _appPortName, value);
    }

    public string AssociationId
    {
        get => _associationId;
        set => SetField(ref _associationId, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (string.Equals(field, value, System.StringComparison.Ordinal))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
