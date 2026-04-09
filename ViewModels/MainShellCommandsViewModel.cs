using System.Windows.Input;

namespace TabletMidiPad.ViewModels;

/// <summary>
/// Command surface for shell/app-level actions.
/// </summary>
internal sealed class MainShellCommandsViewModel
{
    public MainShellCommandsViewModel(IMainShellCommandController controller)
    {
        ArgumentNullException.ThrowIfNull(controller);
        SaveSettingsCommand = new DelegateCommand(controller.SaveSettings);
        ResetDefaultsCommand = new DelegateCommand(controller.ResetDefaults);
        OpenPreferencesCommand = new DelegateCommand(controller.OpenPreferences);
        ExitCommand = new DelegateCommand(controller.Exit);
        RefreshWacomStatusCommand = new DelegateCommand(controller.RefreshWacomStatus);
        ReconnectPenCommand = new DelegateCommand(controller.ReconnectPen);
        StartWacomServiceCommand = new DelegateCommand(controller.StartWacomService);
        AboutCommand = new DelegateCommand(controller.ShowAbout);
    }

    public ICommand SaveSettingsCommand { get; }

    public ICommand ResetDefaultsCommand { get; }

    public ICommand OpenPreferencesCommand { get; }

    public ICommand ExitCommand { get; }

    public ICommand RefreshWacomStatusCommand { get; }

    public ICommand ReconnectPenCommand { get; }

    public ICommand StartWacomServiceCommand { get; }

    public ICommand AboutCommand { get; }
}

