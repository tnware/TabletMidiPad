namespace TabletMidiPad.Services;

public interface IWacomService
{
    (string HeaderText, bool CanStart) GetStatus();
    void Start();
}
