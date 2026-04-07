using System;
using System.Collections.Generic;
using TabletMidiPad.Models;
using TabletMidiPad.Services;

namespace TabletMidiPad.ViewModels;

public sealed class PreferencesViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IMidiService _midiService;

    public AppConfig Config { get; private set; }

    public PreferencesViewModel(ISettingsService settingsService, IMidiService midiService)
    {
        _settingsService = settingsService;
        _midiService = midiService;
        Config = _settingsService.CloneConfig();
    }

    public void Save()
    {
        _settingsService.ApplyConfig(Config);
    }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Config.DawPortName)) return false;
        if (string.IsNullOrWhiteSpace(Config.AppPortName)) return false;
        if (!Guid.TryParse(Config.LoopbackAssociationId, out _)) return false;
        return true;
    }
}
