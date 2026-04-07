namespace TabletMidiPad.Services;

public interface IMidiService
{
    void Initialize(string portName, string dawPortName, string associationId);
    void Shutdown();
    void SendCc(int channel, int cc, int value, bool force = false);
    void SendNote(int channel, int note, bool on, int velocity, bool force = false);
    string[] GetAvailablePorts();
}
