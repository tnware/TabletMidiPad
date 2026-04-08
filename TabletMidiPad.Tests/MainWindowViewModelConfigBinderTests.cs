using System.ComponentModel;
using TabletMidiPad.ViewModels;
using TabletMidiPad.Views;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainWindowViewModelConfigBinderTests
{
    [Fact]
    public void OnPropertyChanged_WhenLoadingUi_IsNoOp()
    {
        var config = AppConfig.Default;
        var vm = new MainViewModel();
        int pressure = 0;
        int routing = 0;
        var sut = new MainWindowViewModelConfigBinder(
            () => true,
            () => config,
            vm,
            () => pressure++,
            () => routing++);

        vm.SendPressureGlobally = false;
        sut.OnPropertyChanged(vm, new PropertyChangedEventArgs(nameof(MainViewModel.SendPressureGlobally)));

        Assert.True(config.SendPressureGlobally);
        Assert.Equal(0, pressure);
        Assert.Equal(0, routing);
    }

    [Fact]
    public void OnPropertyChanged_SendPressureGlobally_WritesConfigAndSyncsBorder()
    {
        var config = AppConfig.Default;
        var vm = new MainViewModel { SendPressureGlobally = false };
        int pressure = 0;
        var sut = new MainWindowViewModelConfigBinder(
            () => false,
            () => config,
            vm,
            () => pressure++,
            () => throw new System.InvalidOperationException("routing should not run"));

        sut.OnPropertyChanged(vm, new PropertyChangedEventArgs(nameof(MainViewModel.SendPressureGlobally)));

        Assert.False(config.SendPressureGlobally);
        Assert.Equal(1, pressure);
    }

    [Fact]
    public void OnPropertyChanged_AllowZoneSwitchWhilePressed_WritesConfigAndRefreshesRoutingUi()
    {
        var config = AppConfig.Default;
        var vm = new MainViewModel { AllowZoneSwitchWhilePressed = false };
        int routing = 0;
        var sut = new MainWindowViewModelConfigBinder(
            () => false,
            () => config,
            vm,
            () => throw new System.InvalidOperationException("pressure border should not run"),
            () => routing++);

        sut.OnPropertyChanged(vm, new PropertyChangedEventArgs(nameof(MainViewModel.AllowZoneSwitchWhilePressed)));

        Assert.False(config.AllowZoneSwitchWhilePressed);
        Assert.Equal(1, routing);
    }
}
