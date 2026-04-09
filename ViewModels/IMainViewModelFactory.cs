namespace TabletMidiPad.ViewModels;

internal interface IMainViewModelFactory
{
    MainViewModel Create(MainShellCommandsViewModel shellCommands);
}
