namespace TabletMidiPad.Services;

public sealed class TabletService : ITabletService
{
    private readonly IInputMappingService _inputMappingService;

    public TabletService(IInputMappingService inputMappingService)
    {
        _inputMappingService = inputMappingService;
    }

    public string NormalizeBackendMode(string? backendMode)
        => _inputMappingService.NormalizeInputBackendMode(backendMode);

    public bool AllowsStylusMouseFallback(string? backendMode)
        => !string.Equals(NormalizeBackendMode(backendMode), "Wintab Only", System.StringComparison.Ordinal);
}
