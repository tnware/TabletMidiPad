namespace TabletMidiPad.ViewModels;

internal interface IMainViewModelFactory
{
    MainViewModel Create(MainViewModelActions actions);
}
