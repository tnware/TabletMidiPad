using System;
using System.Windows;
using TabletMidiPad.Core.Interaction;

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
        string mode = InputBackendModeNormalizer.Normalize(inputBackendMode);
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
            ActiveZoneText: "Active zone: none",
            ValueText: "X: -, Y: -, Pressure: -",
            PenButtonsText: "Pen buttons: -",
            ModifiersText: "Modifiers: none");
    }

    /// <inheritdoc />
    public bool IsStylusMouseFallbackSuppressed(bool useWintab, string inputBackendMode)
        => useWintab || InputBackendModeNormalizer.IsWintabOnlyMode(inputBackendMode);

    /// <inheritdoc />
    public WintabPacketFlowResult BuildWintabPacketFlow(bool wasActive, WintabPacketUpdate update)
    {
        if (update.IsHovering)
        {
            InteractionInput input = new(
                XNorm: update.XNorm,
                YNorm: update.YNorm,
                Pressure: 0,
                IsDown: false,
                IsHovering: true,
                PenButtons: update.PenButtons,
                Backend: InteractionBackend.Wintab);

            return new WintabPacketFlowResult(
                ReleaseBeforeTickIfWasActive: wasActive,
                IsActiveAfter: false,
                TickInput: input,
                PixelPosition: new Point(update.XPixel, update.YPixel),
                CursorOpacity: update.CursorOpacity);
        }

        InteractionInput downInput = new(
            XNorm: update.XNorm,
            YNorm: update.YNorm,
            Pressure: update.Pressure127,
            IsDown: true,
            IsHovering: false,
            PenButtons: update.PenButtons,
            Backend: InteractionBackend.Wintab);

        return new WintabPacketFlowResult(
            ReleaseBeforeTickIfWasActive: false,
            IsActiveAfter: update.IsDown,
            TickInput: downInput,
            PixelPosition: new Point(update.XPixel, update.YPixel),
            CursorOpacity: update.CursorOpacity);
    }

    /// <inheritdoc />
    public bool ShouldReleaseOnProximityLeave(bool isActive) => isActive;

}
