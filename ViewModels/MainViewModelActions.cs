using System;

namespace TabletMidiPad.ViewModels;

internal readonly record struct MainViewModelActions(
    Action SaveSettings,
    Action ResetDefaults,
    Action OpenPreferences,
    Action Exit,
    Action RefreshWacomStatus,
    Action ReconnectPen,
    Action StartWacomService,
    Action ShowAbout);
