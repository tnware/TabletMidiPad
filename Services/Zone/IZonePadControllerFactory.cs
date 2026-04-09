namespace TabletMidiPad.Services.Zone;

/// <summary>
/// Creates <see cref="IZonePadController"/> instances for a view host.
/// </summary>
internal interface IZonePadControllerFactory
{
    /// <summary>
    /// Creates a zone pad controller bound to the specified host.
    /// </summary>
    IZonePadController Create(IZonePadControllerHost host);
}
