using System.Windows;
using TabletMidiPad.ViewModels;

namespace TabletMidiPad.Services.Zone;

/// <summary>
/// Default zone pick + edge-resize session for <see cref="Views.MainWindow"/> pad mouse input.
/// </summary>
internal sealed class ZonePadController : IZonePadController
{
    private readonly IZonePadControllerHost _host;
    private readonly IZoneResizeService _zoneResizeService;
    private bool _isResizingZone;
    private ZoneResizeSession? _zoneResizeSession;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZonePadController"/> class.
    /// </summary>
    public ZonePadController(IZonePadControllerHost host, IZoneResizeService zoneResizeService)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _zoneResizeService = zoneResizeService ?? throw new ArgumentNullException(nameof(zoneResizeService));
    }

    /// <inheritdoc />
    public bool TryHandleMouseDown(Point position)
    {
        if (_host.IsCanvasLocked)
        {
            return false;
        }

        SelectZoneAtPoint(position);
        (int Zone, ZoneResizeEdge Edge) = HitTestZoneEdge(position);
        if (Zone < 0)
        {
            return false;
        }

        StartZoneResize(Zone, Edge, position);
        return _isResizingZone;
    }

    /// <inheritdoc />
    public bool TryConsumeMouseMove(Point position, out ZonePadResizeCursorKind cursorKind)
    {
        if (_host.IsCanvasLocked)
        {
            cursorKind = ZonePadResizeCursorKind.None;
            return false;
        }

        if (_isResizingZone)
        {
            UpdateZoneResize(position);
            cursorKind = ZonePadResizeCursorKind.None;
            return true;
        }

        (int _, ZoneResizeEdge edge) = HitTestZoneEdge(position);
        cursorKind = ZonePadResizeCursors.FromResizeEdge(edge);
        return false;
    }

    /// <inheritdoc />
    public bool TryConsumeMouseUp()
    {
        if (!_isResizingZone)
        {
            return false;
        }

        FinishZoneResize();
        return true;
    }

    private (int zone, ZoneResizeEdge edge) HitTestZoneEdge(Point pos)
        => _zoneResizeService.HitTestZoneEdge(
            _host.Config.Zones,
            _host.SelectedZoneIndex,
            _host.PadActualWidth,
            _host.PadActualHeight,
            pos);

    private void SelectZoneAtPoint(Point pos)
    {
        int zoneIndex = _zoneResizeService.HitTestZoneBody(
            _host.Config.Zones,
            _host.PadActualWidth,
            _host.PadActualHeight,
            pos);
        if (zoneIndex < 0 || zoneIndex == _host.SelectedZoneIndex)
        {
            return;
        }

        _host.CommitCurrentZoneEdits();
        _host.SelectedZoneIndex = zoneIndex;
        _host.LoadSelectedZoneIntoEditor();
        _host.RefreshPadZoneOverlay();
    }

    private void StartZoneResize(int zoneIndex, ZoneResizeEdge edge, Point pos)
    {
        _zoneResizeSession = _zoneResizeService.StartResizeSession(_host.Config.Zones, zoneIndex, edge, pos);
        _isResizingZone = _zoneResizeSession is not null;
        if (!_isResizingZone)
        {
            return;
        }

        if (_host.SelectedZoneIndex != zoneIndex)
        {
            _host.CommitCurrentZoneEdits();
            _host.SelectedZoneIndex = zoneIndex;
            _host.LoadSelectedZoneIntoEditor();
        }
    }

    private void UpdateZoneResize(Point pos)
    {
        if (_zoneResizeSession is null)
        {
            return;
        }

        ZoneResizeUpdate? update = _zoneResizeService.ComputeResizeUpdate(
            _host.Config.Zones,
            _zoneResizeSession,
            _host.PadActualWidth,
            _host.PadActualHeight,
            pos);
        if (update is null)
        {
            return;
        }

        ZoneConfig zone = _host.Config.Zones[update.ZoneIndex];
        zone.Left = update.Left;
        zone.Top = update.Top;
        zone.Width = update.Width;
        zone.Height = update.Height;

        _host.LoadSelectedZoneIntoEditor();
        _host.RefreshPadZoneOverlay();
    }

    private void FinishZoneResize()
    {
        _isResizingZone = false;
        _zoneResizeSession = null;
        _host.ClearPadCursor();
        _host.SetSettingsResult("Zone resized. Click Apply Settings to save.", SettingsResultKind.Success);
    }
}
