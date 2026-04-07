using NSubstitute;
using TabletMidiPad.Services;
using TabletMidiPad.ViewModels;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class MainViewModelTests
{
    private readonly ISettingsService _settings = Substitute.For<ISettingsService>();
    private readonly IMidiService _midi = Substitute.For<IMidiService>();
    private readonly IInputMappingService _mapping = Substitute.For<IInputMappingService>();
    private readonly IWacomService _wacom = Substitute.For<IWacomService>();

    [Fact]
    public void SaveSettings_CallsServiceAndSetsStatus()
    {
        var vm = Create();

        vm.SaveSettingsCommand.Execute(null);

        _settings.Received(1).Save(vm.Config);
        Assert.Equal("Settings applied and saved.", vm.SettingsResultText);
    }

    [Fact]
    public void ResetDefaults_ResetsConfig()
    {
        var vm = Create();
        vm.Config.Zones.Add(ZoneConfig.CreateDefault());

        vm.ResetDefaultsCommand.Execute(null);

        Assert.Single(vm.Config.Zones);
    }

    [Fact]
    public void AddAndRemoveZone_UpdatesZoneCount()
    {
        var vm = Create();
        int start = vm.Config.Zones.Count;

        vm.AddZoneCommand.Execute(null);
        Assert.Equal(start + 1, vm.Config.Zones.Count);

        vm.SelectedZoneIndex = vm.Config.Zones.Count - 1;
        vm.RemoveZoneCommand.Execute(null);
        Assert.Equal(start, vm.Config.Zones.Count);
    }

    [Fact]
    public void GetNextAvailableCcStart_ReturnsGap()
    {
        var vm = Create();
        vm.Config.Zones.Clear();
        vm.Config.Zones.Add(new ZoneConfig { CcX = 0, CcY = 1, CcPressure = 2 });

        Assert.Equal(3, vm.GetNextAvailableCcStart());
    }

    [Fact]
    public void RefreshWacomStatus_UpdatesProperties()
    {
        _wacom.GetStatus().Returns(("Service: running", false));
        var vm = Create();

        vm.RefreshWacomStatusCommand.Execute(null);

        Assert.Equal("Service: running", vm.WacomServiceStatusText);
        Assert.False(vm.CanStartWacomService);
    }

    [Fact]
    public void SetPressureCurve_UpdatesToolbarLabel()
    {
        var vm = Create();

        vm.SetPressureCurveCommand.Execute("Exponential");

        Assert.Equal("Exponential", vm.Config.PressureCurve);
        Assert.Equal("Pressure (Exp)", vm.ToolbarPressureLabelText);
    }

    [Fact]
    public void SetInputBackendMode_UpdatesConfigAndRaisesEvent()
    {
        _mapping.NormalizeInputBackendMode("Wintab").Returns("Wintab Only");
        var vm = Create();
        bool raised = false;
        vm.InputBackendChanged += (_, _) => raised = true;

        vm.SetInputBackendModeCommand.Execute("Wintab");

        Assert.Equal("Wintab Only", vm.Config.InputBackendMode);
        Assert.True(raised);
    }

    private MainViewModel Create()
    {
        _settings.Load().Returns(AppConfig.Default);
        _mapping.NormalizeInputBackendMode(Arg.Any<string?>()).Returns("Auto");
        _mapping.NormalizeReleaseBehavior(Arg.Any<string?>()).Returns(ZoneReleaseBehaviors.NoSendOnRelease);

        var vm = new MainViewModel(_settings, _midi, _mapping, _wacom);
        vm.Load();
        return vm;
    }
}
