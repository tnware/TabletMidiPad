using System;
using System.Threading.Tasks;

namespace TabletMidiPad.Services;

public interface IMidiService : IDisposable
{
    bool IsConnected { get; }
    Task InitializeAsync(AppConfig config);
    void Restart(AppConfig config);
    void SendCcIfChanged(int channel, int cc, int value, bool force = false);
    void SendNoteIfChanged(int channel, int note, bool on, int velocity, bool force = false);
    void Shutdown();
}
