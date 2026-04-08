namespace TabletMidiPad.ViewModels;

/// <summary>
/// View-model row for one ExpressKey mapping.
/// </summary>
internal sealed class ExpressKeyRowViewModel : ViewModelBase
{
    private string _action = PenButtonActions.None;
    private string _midiChannelText = "1";
    private string _ccText = "70";
    private string _noteText = "36";

    public int Index { get; init; }

    public string Action
    {
        get => _action;
        set => SetProperty(ref _action, value);
    }

    public string MidiChannelText
    {
        get => _midiChannelText;
        set => SetProperty(ref _midiChannelText, value);
    }

    public string CcText
    {
        get => _ccText;
        set => SetProperty(ref _ccText, value);
    }

    public string NoteText
    {
        get => _noteText;
        set => SetProperty(ref _noteText, value);
    }
}
