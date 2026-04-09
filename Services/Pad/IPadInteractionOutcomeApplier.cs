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
    /// Gets last known per-zone marker positions for the requested page.
    /// </summary>
    IReadOnlyDictionary<ZoneConfig, Point> GetLastZoneMarkers(int pageIndex);

    /// <summary>
    /// Applies MIDI messages, updates interaction telemetry on the view model, and updates marker cache for the active page.
    /// </summary>
    void Apply(
        InteractionResult result,
        InteractionState state,
        int activePageIndex,
        IReadOnlyList<ZoneConfig> activeZones,
        MainViewModel viewModel);
}
