namespace TabletMidiPad.Services;

/// <summary>
/// Creates <see cref="IZonePadController"/> instances for a view host.
/// </summary>
internal interface IZonePadControllerFactory
{
    IZonePadController Create(IZonePadControllerHost host);
}

