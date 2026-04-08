using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

internal sealed class ZoneListViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IZoneActionWorkflowCoordinator _zoneActionCoordinator;
    private readonly IZoneEditorService _zoneEditorService;
    private int _selectedZoneIndex = -1;

    public ZoneListViewModel(
        ISettingsService settingsService,
        IZoneActionWorkflowCoordinator zoneActionCoordinator,
        IZoneEditorService zoneEditorService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _zoneActionCoordinator = zoneActionCoordinator ?? throw new ArgumentNullException(nameof(zoneActionCoordinator));
        _zoneEditorService = zoneEditorService ?? throw new ArgumentNullException(nameof(zoneEditorService));

        AddZoneCommand = new DelegateCommand(AddZone);
        RemoveZoneCommand = new DelegateCommand(RemoveZone);
        AutoLayoutCommand = new DelegateCommand(AutoLayout);

        _settingsService.SettingsUpdated += (s, e) => RefreshZones();
        RefreshZones();
    }

    public ObservableCollection<ZoneListItemViewModel> Zones { get; } = new();

    public int SelectedZoneIndex
    {
        get => _selectedZoneIndex;
        set
        {
            if (SetProperty(ref _selectedZoneIndex, value))
            {
                OnSelectedZoneChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? OnSelectedZoneChanged;

    public ICommand AddZoneCommand { get; }
    public ICommand RemoveZoneCommand { get; }
    public ICommand AutoLayoutCommand { get; }

    public void RefreshZones()
    {
        _zoneEditorService.RefreshZoneList(Zones, _settingsService.Config);
    }

    public void AddZone()
    {
        var result = _zoneActionCoordinator.AddZone(_settingsService.Config);
        if (result.Changed)
        {
            _settingsService.Save();
            RefreshZones();
            SelectedZoneIndex = result.SelectedZoneIndex;
        }
    }

    public void RemoveZone()
    {
        var result = _zoneActionCoordinator.RemoveZone(_settingsService.Config, SelectedZoneIndex);
        if (result.Changed)
        {
            _settingsService.Save();
            RefreshZones();
            SelectedZoneIndex = result.SelectedZoneIndex;
        }
    }

    public void AutoLayout()
    {
        var result = _zoneActionCoordinator.AutoLayout(_settingsService.Config, SelectedZoneIndex);
        if (result.Changed)
        {
            _settingsService.Save();
            RefreshZones();
            SelectedZoneIndex = result.SelectedZoneIndex;
        }
    }

    internal void NotifySelectedZoneIndexChanged()
    {
        OnPropertyChanged(nameof(SelectedZoneIndex));
    }
}
