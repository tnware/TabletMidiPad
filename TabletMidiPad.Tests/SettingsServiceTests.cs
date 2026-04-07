using Xunit;
using TabletMidiPad.Services;
using TabletMidiPad.Models;
using System.IO;

namespace TabletMidiPad.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void DefaultConfig_IsCreated_WhenFileMissing()
    {
        var service = new SettingsService();
        Assert.NotNull(service.Config);
        Assert.NotEmpty(service.Config.Zones);
    }
}
