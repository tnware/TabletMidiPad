using System.Windows;
using TabletMidiPad.Core.Interaction;

namespace TabletMidiPad.Services;

/// <summary>
/// Default input event-flow decisions for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class MainWindowInputEventFlowCoordinator : IMainWindowInputEventFlowCoordinator
{
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
