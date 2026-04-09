using TabletMidiPad.Services;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ToolbarConfigServiceTests
{
    private readonly ToolbarConfigService _sut = new();

    [Fact]
    public void TryApplyInputBackendMode_WhenUnchanged_ReturnsNoRebind()
    {
        var config = new AppConfig { InputBackendMode = "Auto" };

        ToolbarInputModeApplyResult r = _sut.TryApplyInputBackendMode(config, "Auto");

        Assert.False(r.ModeChanged);
        Assert.False(r.RequiresInputRebind);
    }

    [Fact]
    public void TryApplyInputBackendMode_WhenChanged_ReturnsRebind()
    {
        var config = new AppConfig { InputBackendMode = "Auto" };

        ToolbarInputModeApplyResult r = _sut.TryApplyInputBackendMode(config, "Wintab Only");

        Assert.True(r.ModeChanged);
        Assert.True(r.RequiresInputRebind);
        Assert.Equal("Wintab Only", config.InputBackendMode);
    }

    [Fact]
    public void GetNextCycledInputBackendModeLabel_RotatesModes()
    {
        var wintabOnly = new AppConfig { InputBackendMode = "Wintab Only" };
        Assert.Equal("Mouse", _sut.GetNextCycledInputBackendModeLabel(wintabOnly));

        var mouse = new AppConfig { InputBackendMode = "Stylus/Mouse Only" };
        Assert.Equal("Auto", _sut.GetNextCycledInputBackendModeLabel(mouse));

        var auto = new AppConfig { InputBackendMode = "Auto" };
        Assert.Equal("Wintab Only", _sut.GetNextCycledInputBackendModeLabel(auto));
    }
}
