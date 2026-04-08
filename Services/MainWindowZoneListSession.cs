using System;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services;

/// <summary>
/// Default zone list orchestration for <see cref="Views.MainWindow"/>.
/// </summary>
internal sealed class MainWindowZoneListSession : IMainWindowZoneListSession
{
    private readonly IMainWindowZoneListSessionHost _host;
    private readonly IZoneActionWorkflowCoordinator _zoneActions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowZoneListSession"/> class.
    /// </summary>
    public MainWindowZoneListSession(
        IMainWindowZoneListSessionHost host,
        IZoneActionWorkflowCoordinator zoneActions)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _zoneActions = zoneActions ?? throw new ArgumentNullException(nameof(zoneActions));
    }

    /// <inheritdoc />
    public void AutoLayoutZones()
    {
        _host.CommitCurrentZoneEdits();
        ZoneActionWorkflowResult result = _zoneActions.AutoLayout(_host.Config, _host.SelectedZoneIndex);
        ApplyZoneActionResult(result);
    }

    /// <inheritdoc />
    public void AddZone()
    {
        _host.CommitCurrentZoneEdits();
        ZoneActionWorkflowResult result = _zoneActions.AddZone(_host.Config);
        ApplyZoneActionResult(result);
    }

    /// <inheritdoc />
    public void RemoveZone()
    {
        ZoneActionWorkflowResult result = _zoneActions.RemoveZone(_host.Config, _host.SelectedZoneIndex);
        if (!result.Changed)
        {
            return;
        }

        ApplyZoneActionResult(result);
    }

    private void ApplyZoneActionResult(ZoneActionWorkflowResult result)
    {
        _host.RefreshZoneList();
        _host.SelectedZoneIndex = result.SelectedZoneIndex;
        _host.NotifySelectedZoneIndexChanged();
        _host.LoadSelectedZoneIntoEditor();
        _host.RefreshPadZoneOverlay();
        _host.SetSettingsResult(result.StatusMessage, SettingsResultKind.Success);
    }
}
