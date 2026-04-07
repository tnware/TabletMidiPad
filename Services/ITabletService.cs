namespace TabletMidiPad.Services;

public interface ITabletService
{
    string NormalizeBackendMode(string? backendMode);
    bool AllowsStylusMouseFallback(string? backendMode);
}
