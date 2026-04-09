namespace TabletMidiPad.Services.Input;

/// <summary>
/// Creates <see cref="IPadInputController"/> instances for a view host.
/// </summary>
internal interface IPadInputControllerFactory
{
    IPadInputController Create(IPadInputControllerHost host);
}

