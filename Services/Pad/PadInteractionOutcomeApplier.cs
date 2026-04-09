using System.Linq;
using System.Windows;
using TabletMidiPad.Core.Interaction;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Pad;

/// <summary>
/// Default implementation of <see cref="IPadInteractionOutcomeApplier"/>.
/// </summary>
internal sealed class PadInteractionOutcomeApplier : IPadInteractionOutcomeApplier
{
    private readonly IMidiEngine _midiEngine;
    private readonly Dictionary<int, Dictionary<ZoneConfig, Point>> _lastZonePointsByPage = new();

    public PadInteractionOutcomeApplier(IMidiEngine midiEngine)
    {
        _midiEngine = midiEngine ?? throw new ArgumentNullException(nameof(midiEngine));
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<ZoneConfig, Point> GetLastZoneMarkers(int pageIndex)
    {
        return _lastZonePointsByPage.TryGetValue(pageIndex, out Dictionary<ZoneConfig, Point>? points)
            ? points
            : s_emptyPoints;
    }

    /// <inheritdoc />
    public void Apply(
        InteractionResult result,
        InteractionState state,
        int activePageIndex,
        IReadOnlyList<ZoneConfig> activeZones,
        MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(activeZones);
        ArgumentNullException.ThrowIfNull(viewModel);

        foreach (MidiMessage message in result.MidiMessages)
        {
            if (message.Kind == MidiMessageKind.ControlChange)
            {
                _midiEngine.SendCcIfChanged(message.Channel, message.Number, message.Value, force: message.Force);
            }
            else
            {
                bool on = message.Value > 0;
                _midiEngine.SendNoteIfChanged(message.Channel, message.Number, on, message.Value, force: message.Force);
            }
        }

        viewModel.ActiveZoneText = result.ActiveZoneLabel;
        viewModel.ValueText = result.ValueLabel;
        viewModel.ModifiersText = result.ModifiersLabel;

        Dictionary<ZoneConfig, Point> markers = GetOrCreatePageMarkers(activePageIndex);
        var activeZoneSet = new HashSet<ZoneConfig>(activeZones);
        foreach (ZoneConfig existingZone in markers.Keys.ToArray())
        {
            if (!activeZoneSet.Contains(existingZone))
            {
                markers.Remove(existingZone);
            }
        }

        int markerZoneIndex = result.ActiveZoneIndex;
        if (markerZoneIndex >= 0 &&
            markerZoneIndex < activeZones.Count &&
            state.LastLocalByZone.TryGetValue(markerZoneIndex, out (double X, double Y) local))
        {
            markers[activeZones[markerZoneIndex]] = new Point(local.X, local.Y);
        }
    }

    private static readonly IReadOnlyDictionary<ZoneConfig, Point> s_emptyPoints = new Dictionary<ZoneConfig, Point>();

    private Dictionary<ZoneConfig, Point> GetOrCreatePageMarkers(int pageIndex)
    {
        if (!_lastZonePointsByPage.TryGetValue(pageIndex, out Dictionary<ZoneConfig, Point>? markers))
        {
            markers = new Dictionary<ZoneConfig, Point>();
            _lastZonePointsByPage[pageIndex] = markers;
        }

        return markers;
    }
}
