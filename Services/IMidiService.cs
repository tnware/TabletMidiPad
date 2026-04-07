using System;
using System.Collections.Generic;

namespace TabletMidiPad.Services;

public interface IMidiService : IDisposable
{
    bool IsConnected { get; }
    void Initialize(string appPortName, string dawPortName, string associationId);
    void Shutdown();
    void SendCc(int channel, int cc, int value, bool force = false);
    void SendNote(int channel, int note, bool on, int velocity, bool force = false);
    void ClearLastSentValues();
    IEnumerable<string> GetAvailableOutputs();
}
