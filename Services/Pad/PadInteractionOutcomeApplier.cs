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
    private readonly Dictionary<ZoneConfig, Point> _lastZonePoints = new();

    public PadInteractionOutcomeApplier(IMidiEngine midiEngine)
    {
        _midiEngine = midiEngine ?? throw new ArgumentNullException(nameof(midiEngine));
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<ZoneConfig, Point> LastZoneMarkers => _lastZonePoints;

    /// <inheritdoc />
    public void Apply(
        InteractionResult result,
        InteractionState state,
        AppConfig config,
        MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(config);
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

        _lastZonePoints.Clear();
        foreach ((int zoneIndex, (double X, double Y) local) in state.LastLocalByZone)
        {
            if (zoneIndex < 0 || zoneIndex >= config.Zones.Count)
            {
                continue;
            }

            _lastZonePoints[config.Zones[zoneIndex]] = new Point(local.X, local.Y);
        }
    }
}
