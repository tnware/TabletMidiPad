using System;
using System.Windows.Input;

namespace TabletMidiPad.ViewModels;

internal sealed class ZoneListViewModel : ViewModelBase
{
    private readonly IRuntimeConfigService _runtimeConfig;
    private readonly IZoneActionWorkflowCoordinator _zoneActions;
    private readonly ZoneEditorViewModel _zoneEditor;

    public ZoneListViewModel(
        IRuntimeConfigService runtimeConfig,
        IZoneActionWorkflowCoordinator zoneActions,
        ZoneEditorViewModel zoneEditor)
    {
        _runtimeConfig = runtimeConfig ?? throw new ArgumentNullException(nameof(runtimeConfig));
        _zoneActions = zoneActions ?? throw new ArgumentNullException(nameof(zoneActions));
        _zoneEditor = zoneEditor ?? throw new ArgumentNullException(nameof(zoneEditor));
        AddZoneCommand = new DelegateCommand(AddZone);
        RemoveZoneCommand = new DelegateCommand(RemoveZone);
        AutoLayoutCommand = new DelegateCommand(AutoLayoutZones);
    }

    public event Action? PadZoneOverlayRefreshRequested;

    public event Action<string, SettingsResultKind>? SettingsResultRequested;

    public ICommand AddZoneCommand { get; }

    public ICommand RemoveZoneCommand { get; }

    public ICommand AutoLayoutCommand { get; }

    private void AutoLayoutZones()
    {
        _zoneEditor.CommitCurrentZoneEdits();
        ZoneActionWorkflowResult result = _zoneActions.AutoLayout(_runtimeConfig.Current, _zoneEditor.SelectedZoneIndex);
        ApplyZoneActionResult(result);
    }

    private void AddZone()
    {
        _zoneEditor.CommitCurrentZoneEdits();
        ZoneActionWorkflowResult result = _zoneActions.AddZone(_runtimeConfig.Current);
        ApplyZoneActionResult(result);
    }

    private void RemoveZone()
    {
        ZoneActionWorkflowResult result = _zoneActions.RemoveZone(_runtimeConfig.Current, _zoneEditor.SelectedZoneIndex);
        if (!result.Changed)
        {
            return;
        }

        ApplyZoneActionResult(result);
    }

    private void ApplyZoneActionResult(ZoneActionWorkflowResult result)
    {
        _zoneEditor.RefreshZoneList();
        _zoneEditor.SelectedZoneIndex = result.SelectedZoneIndex;
        _zoneEditor.NotifySelectedZoneIndexChanged();
        _zoneEditor.LoadSelectedZoneIntoEditor();
        PadZoneOverlayRefreshRequested?.Invoke();
        SettingsResultRequested?.Invoke(result.StatusMessage, SettingsResultKind.Success);
    }
}
