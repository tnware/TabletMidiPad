namespace TabletMidiPad.Services;

/// <summary>
/// Creates <see cref="ISettingsWorkflow"/> instances for a view host.
/// </summary>
internal interface ISettingsWorkflowFactory
{
    ISettingsWorkflow Create(ISettingsWorkflowHost host, IPadInputController padInputController);
}

