using System;

namespace TabletMidiPad.Services;

/// <summary>
/// Default coordinator for input backend lifecycle/rebind/reset orchestration in MainWindow.
/// </summary>
internal sealed class MainWindowInputBackendCoordinator : IMainWindowInputBackendCoordinator
{
    /// <inheritdoc />
    public InputBackendLifecycleResult Initialize(
        string inputBackendMode,
        IWintabInputBackend backend,
        int selectedDeviceIndex,
        int currentExpressKeyCount)
    {
        string mode = NormalizeInputBackendMode(inputBackendMode);
        if (string.Equals(mode, "Stylus/Mouse Only", StringComparison.Ordinal))
        {
            backend.Shutdown();
            return new InputBackendLifecycleResult(
                UseWintab: false,
                ExpressKeyCount: 0,
                LastInputBackend: "Stylus/Mouse Fallback",
                WintabDiagnostics: "Manual stylus/mouse mode selected");
        }

        backend.Initialize(selectedDeviceIndex);
        bool useWintab = backend.IsActive;
        int expressKeyCount = Math.Max(currentExpressKeyCount, backend.ExpressKeyCount);
        string lastInputBackend = useWintab
            ? "Wintab"
            : string.Equals(mode, "Wintab Only", StringComparison.Ordinal)
                ? "Wintab unavailable"
                : "Stylus/Mouse Fallback";
        string diagnostics = useWintab ? "Wintab initialized" : "Wintab32.dll not available";

        return new InputBackendLifecycleResult(
            UseWintab: useWintab,
            ExpressKeyCount: expressKeyCount,
            LastInputBackend: lastInputBackend,
            WintabDiagnostics: diagnostics);
    }

    /// <inheritdoc />
    public InputBackendLifecycleResult Shutdown(IWintabInputBackend backend)
    {
        backend.Shutdown();
        return new InputBackendLifecycleResult(
            UseWintab: false,
            ExpressKeyCount: 0,
            LastInputBackend: "Stylus/Mouse Fallback",
            WintabDiagnostics: "Wintab shut down");
    }

    /// <inheritdoc />
    public InputBackendResetResult BuildResetState()
    {
        return new InputBackendResetResult(
            IsActive: false,
            LowerPenButtonHeld: false,
            UpperPenButtonHeld: false,
            HasLockedPosition: false,
            ActiveZoneText: "Active zone: none",
            ValueText: "X: -, Y: -, Pressure: -",
            PenButtonsText: "Pen buttons: -",
            ModifiersText: "Modifiers: none");
    }

    private static string NormalizeInputBackendMode(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        return value switch
        {
            "Wintab" => "Wintab Only",
            "Mouse" => "Stylus/Mouse Only",
            "Wintab Only" => value,
            "Stylus/Mouse Only" => value,
            _ => "Auto"
        };
    }
}
