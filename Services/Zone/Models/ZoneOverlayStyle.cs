namespace TabletMidiPad.Services.Zone.Models;

/// <summary>
/// Controls the interior decoration drawn inside each zone on the pad overlay.
/// </summary>
public enum ZoneOverlayStyle
{
    /// <summary>Solid fill with no interior markings.</summary>
    Plain,

    /// <summary>Grid lines that are bold at the zone center and fade to the edges.</summary>
    VignetteGrid,

    /// <summary>Regular array of dots across the zone interior.</summary>
    DotArray,

    /// <summary>X and Y axis lines crossing at the zone center, representing a 0,0 origin.</summary>
    XYGraph,

    /// <summary>X and Y axis lines anchored to the bottom-left corner, where origin is 0,0.</summary>
    XYGraphBottomLeft
}
