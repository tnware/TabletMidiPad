using Xunit;

namespace TabletMidiPad.Tests;

public sealed class WintabEventAdapterTests
{
    [Fact]
    public void BuildExpressKeyUpdateUsesConfiguredActionAndEncodesPressedState()
    {
        var adapter = new WintabEventAdapter();
        var eventArgs = new WintabExpressKeyEventArgs(
            Control: 1,
            IsPressed: true,
            Diagnostics: "diag");
        ExpressKeyConfig[] configured = new[]
        {
            new ExpressKeyConfig { Action = PenButtonActions.None },
            new ExpressKeyConfig { Action = PenButtonActions.CcToggle },
        };

        WintabExpressKeyUpdate update = adapter.BuildExpressKeyUpdate(eventArgs, configured);

        Assert.Equal(1, update.Control);
        Assert.True(update.IsPressed);
        Assert.Equal(2, update.RequiredExpressKeyCount);
        Assert.Equal(0x0001_0001u, update.EncodedPenButtons);
        Assert.Equal(PenButtonActions.CcToggle, update.ActionLabel);
        Assert.Equal("Wintab EK1", update.BackendLabel);
        Assert.Equal("EK1: pressed [CC Toggle]", update.ExpressKeyDiagnostics);
    }

    [Fact]
    public void TryBuildPacketUpdateReturnsFalseWhenPadDimensionsInvalid()
    {
        var adapter = new WintabEventAdapter();
        var packet = new WintabPacketEventArgs(
            ContextHandle: 0,
            DeviceIndex: 0,
            XNorm: 0.5,
            YNorm: 0.5,
            Pressure127: 64,
            IsHovering: false,
            Buttons: 0,
            PacketDiagnostics: "diag");

        bool built = adapter.TryBuildPacketUpdate(packet, 0, 200, out WintabPacketUpdate update);

        Assert.False(built);
        Assert.Equal(default, update);
    }

    [Fact]
    public void TryBuildPacketUpdateMapsFieldsAndHoverOpacity()
    {
        var adapter = new WintabEventAdapter();
        var packet = new WintabPacketEventArgs(
            ContextHandle: 9,
            DeviceIndex: 2,
            XNorm: 0.25,
            YNorm: 0.75,
            Pressure127: 100,
            IsHovering: true,
            Buttons: 3,
            PacketDiagnostics: "pkt");

        bool built = adapter.TryBuildPacketUpdate(packet, 200, 100, out WintabPacketUpdate update);

        Assert.True(built);
        Assert.Equal(50, update.XPixel);
        Assert.Equal(75, update.YPixel);
        Assert.Equal(0.25, update.XNorm);
        Assert.Equal(0.75, update.YNorm);
        Assert.Equal(100, update.Pressure127);
        Assert.True(update.IsHovering);
        Assert.Equal((uint)3, update.PenButtons);
        Assert.Equal("Wintab D3", update.BackendLabel);
        Assert.Equal("pkt", update.PacketDiagnostics);
        Assert.Equal(0.35, update.CursorOpacity);
        Assert.False(update.IsDown);
    }
}
