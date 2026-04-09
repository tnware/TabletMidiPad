using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainViewModelStatusProjectionTests
{
    [Fact]
    public void ApplyWacomServiceMenuState_UpdatesHeaderAndStartEnabled()
    {
        var vm = new MainViewModel();

        vm.ApplyWacomServiceMenuState(new WacomServiceMenuState("Service: running", StartEnabled: false));

        Assert.Equal("Service: running", vm.WacomServiceStatusHeader);
        Assert.False(vm.IsWacomServiceStartEnabled);
    }

    [Fact]
    public void ApplyConnectedState_UpdatesAllConnectedFields()
    {
        var vm = new MainViewModel();
        var state = new ConnectedState(
            StatusText: "Connected",
            RoutingText: "Route",
            ActiveZoneText: "Active zone: 1",
            ConfigSummaryText: "summary",
            ValueText: "value",
            PenButtonsText: "buttons",
            ModifiersText: "mods");

        vm.ApplyConnectedState(state);

        Assert.Equal("Connected", vm.StatusText);
        Assert.Equal("Route", vm.RoutingText);
        Assert.Equal("Active zone: 1", vm.ActiveZoneText);
        Assert.Equal("summary", vm.ConfigSummaryText);
        Assert.Equal("value", vm.ValueText);
        Assert.Equal("buttons", vm.PenButtonsText);
        Assert.Equal("mods", vm.ModifiersText);
    }
}
