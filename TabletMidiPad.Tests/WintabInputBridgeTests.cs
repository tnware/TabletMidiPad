using TabletMidiPad.Core.Interaction;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class WintabInputBridgeTests
{
    private readonly WintabInputBridge _sut = new(new WintabEventAdapter(), new InputBackendCoordinator());

    [Fact]
    public void TryBuildPacketBridgeInvalidPadReturnsFalse()
    {
        var packet = new WintabPacketEventArgs(
            ContextHandle: 0,
            DeviceIndex: 0,
            XNorm: 0.5,
            YNorm: 0.5,
            Pressure127: 0,
            IsHovering: true,
            Buttons: 0,
            PacketDiagnostics: "d");

        bool ok = _sut.TryBuildPacketBridge(packet, padWidth: 0, padHeight: 100, isActiveBefore: false, out WintabPacketBridgeOutcome outcome);

        Assert.False(ok);
        Assert.Equal(default, outcome);
    }

    [Fact]
    public void BuildExpressKeyBridgeMatchesEncodedInput()
    {
        var expressKey = new WintabExpressKeyEventArgs(Control: 0, IsPressed: true, Diagnostics: "x");
        var keys = new List<ExpressKeyConfig> { new() { Action = PenButtonActions.None } };

        WintabExpressKeyBridgeOutcome outcome = _sut.BuildExpressKeyBridge(expressKey, keys);

        Assert.Equal(1, outcome.RequiredExpressKeyCount);
        Assert.Equal(InteractionBackend.ExpressKey, outcome.EngineInput.Backend);
        Assert.Contains("pressed", outcome.ExpressKeyDiagnostics, StringComparison.Ordinal);
        Assert.Equal("Wintab EK0", outcome.BackendLabel);
    }
}
