using System.Windows.Controls;
using System.Windows.Media;
using TabletMidiPad.Services.Zone.Models;
using Xunit;

namespace TabletMidiPad.Tests;

public sealed class ZoneOverlayRendererTests
{
    [Fact]
    public void RenderScaleBreathZoneDrawsStaticNoteLabels()
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var sut = new ZoneOverlayRenderer();
                var canvas = new Canvas();
                var request = new ZoneOverlayRenderRequest(
                    PadWidth: 400,
                    PadHeight: 220,
                    Zones:
                    [
                        new ZoneOverlayZone(
                            Name: "S",
                            Enabled: true,
                            SendX: true,
                            SendY: true,
                            SendPressure: true,
                            OutputMode: ZoneOutputModes.ScaleBreath,
                            ScaleBreathRoot: 0,
                            ScaleBreathScale: ScaleBreathScaleNames.Major,
                            ScaleBreathOctave: 4,
                            ScaleBreathHoleCount: 4,
                            ScaleBreathRowSplit: 0.5,
                            ScaleGridRoot: 0,
                            ScaleGridScale: ScaleBreathScaleNames.Major,
                            ScaleGridOctave: 4,
                            ScaleGridRows: 4,
                            ScaleGridColumns: 4,
                            Left: 0,
                            Top: 0,
                            Width: 1,
                            Height: 1),
                    ],
                    SelectedZoneIndex: 0,
                    LastLocalByZoneIndex: new Dictionary<int, (double X, double Y)>(),
                    ZonePalette: [Colors.CornflowerBlue],
                    ColorWindow: Colors.Black,
                    TextPrimaryBrush: Brushes.White,
                    TextMutedBrush: Brushes.Gray,
                    TextSoftBrush: Brushes.DarkGray,
                    DangerBrush: Brushes.Red,
                    AccentBrushHover: Brushes.White,
                    SurfaceBrush: Brushes.Black,
                    OverlayStyle: ZoneOverlayStyle.Plain);

                sut.Render(canvas, request);

                var labels = canvas.Children.OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
                Assert.Contains("C3", labels);
                Assert.Contains("C4", labels);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (error is not null)
        {
            throw new Xunit.Sdk.XunitException($"STA render assertion failed: {error}");
        }
    }
}
