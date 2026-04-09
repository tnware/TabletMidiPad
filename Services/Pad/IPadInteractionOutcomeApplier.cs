using System.Windows;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Pad;

/// <summary>
/// Applies pad interaction engine output: MIDI sends, on-screen telemetry, and zone marker positions for the overlay.
/// </summary>
internal interface IPadInteractionOutcomeApplier
{
    /// <summary>
    /// Last known per-zone marker positions in pad-normalized space (for overlay rendering).
    /// </summary>
    IReadOnlyDictionary<ZoneConfig, Point> LastZoneMarkers { get; }

    /// <summary>
    /// Applies MIDI messages, updates interaction telemetry on the view model, and refreshes <see cref="LastZoneMarkers"/>.
    /// </summary>
    void Apply(
        InteractionResult result,
        InteractionState state,
        AppConfig config,
        MainViewModel viewModel);
}
