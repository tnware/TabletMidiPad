using Xunit;

namespace TabletMidiPad.Tests;

public sealed class WintabExpressKeyOverrideRegistryTests
{
    [Fact]
    public void ReleaseContextDisablesOnlyOverridesOwnedByThatContext()
    {
        var registry = new WintabExpressKeyOverrideRegistry();
        registry.Add(new WintabExpressKeyOverride(10, 1, 2, 3));
        registry.Add(new WintabExpressKeyOverride(20, 4, 5, 6));
        var released = new List<WintabExpressKeyOverride>();

        int count = registry.ReleaseContext(10, entry =>
        {
            released.Add(entry);
            return true;
        });

        Assert.Equal(1, count);
        Assert.Equal(new WintabExpressKeyOverride(10, 1, 2, 3), Assert.Single(released));
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void ReleaseContextForgetsOverrideEvenWhenDriverRejectsCleanup()
    {
        var registry = new WintabExpressKeyOverrideRegistry();
        registry.Add(new WintabExpressKeyOverride(10, 1, 2, 3));

        int count = registry.ReleaseContext(10, _ => false);

        Assert.Equal(0, count);
        Assert.Equal(0, registry.Count);
    }
}
